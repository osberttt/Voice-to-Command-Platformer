using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Voice recognizer with L2-normalized centroid matching and competitive discrimination
/// </summary>
public class VoiceRecognizerOptimized : MonoBehaviour
{
    public AudioInputOptimized audioInput;

    public UnityEvent onJumpDetected = new UnityEvent();
    public UnityEvent onTurnDetected = new UnityEvent();

    public event EventHandler<VoiceEventArgs> OnJumpMetrics;
    public event EventHandler<VoiceEventArgs> OnTurnMetrics;

    [Header("Recognition Settings")]
    public float frameThreshold = 12f; // Overridden by auto-threshold from templates
    public float cooldown = 0.15f;

    [Header("Optimization")]
    public int minFramesRequired = 2;
    public bool useDeltaFeatures = true;
    public float deltaWeight = 0.3f;

    [Header("Competitive Matching")]
    public float marginFactor = 0.8f; // Winner's dist must be < loser's dist * this

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public bool trackMetrics = true;

    private VoiceTemplateOptimized jump;
    private VoiceTemplateOptimized turn;

    const int MFCC_DIM = 6;

    private int jumpMatchCount;
    private int turnMatchCount;
    private float jumpStartTime;
    private float turnStartTime;

    private bool locked;
    private float unlockTime;

    // Metrics
    private List<float> recentLatencies = new List<float>(100);
    private int frameCounter;
    private float startTime;

    // Delta computation
    private float[] prevMFCC = new float[MFCC_DIM];
    private bool hasPrevious;

    void Awake()
    {
        Load();
    }

    void Start()
    {
        if (audioInput != null)
        {
            audioInput.OnWindow += OnWindow;
        }

        startTime = Time.time;
    }

    void OnDisable()
    {
        if (audioInput != null)
        {
            audioInput.OnWindow -= OnWindow;
        }
    }

    void Load()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "voice_templates_optimized.json"
        );

        if (!File.Exists(path))
        {
            if (enableDebugLogs)
                Debug.LogError("No optimized templates found");
            return;
        }

        var data = JsonUtility.FromJson<VoiceTemplateDataOptimized>(
            File.ReadAllText(path)
        );

        jump = data.jump;
        turn = data.turn;

        // Use auto-calibrated threshold if available
        if (data.autoThreshold > 0f)
        {
            frameThreshold = data.autoThreshold;
            if (enableDebugLogs)
                Debug.Log($"Using auto-threshold: {frameThreshold:F3}");
        }

        if (enableDebugLogs)
        {
            Debug.Log($"Loaded: jump={jump.windows.Count} frames, turn={turn.windows.Count} frames, threshold={frameThreshold:F3}");
        }
    }

    void OnWindow(float[] mfcc)
    {
        frameCounter++;

        if (locked && Time.time < unlockTime)
            return;

        // L2-normalize incoming MFCC for volume invariance
        float[] normMFCC = VoiceTemplateOptimized.NormalizeMFCC(mfcc);

        // Compute delta features from normalized vectors
        float[] delta = null;
        if (useDeltaFeatures && hasPrevious)
        {
            delta = new float[MFCC_DIM];
            for (int i = 0; i < MFCC_DIM; i++)
                delta[i] = normMFCC[i] - prevMFCC[i];
        }

        // Compute distances to BOTH templates (centroid-based)
        float jumpDist = ComputeTemplateDistance(jump, normMFCC, delta);
        float turnDist = ComputeTemplateDistance(turn, normMFCC, delta);

        // Competitive matching: only the closer template gets credit
        bool jumpCloser = jumpDist <= turnDist;

        if (jumpCloser)
        {
            bool hasMargin = jumpDist < turnDist * marginFactor;
            ProcessMatch(jump, jumpDist, hasMargin, ref jumpMatchCount, ref jumpStartTime, "JUMP", OnJumpFired);
            if (turnMatchCount > 0) turnMatchCount = 0;
        }
        else
        {
            bool hasMargin = turnDist < jumpDist * marginFactor;
            ProcessMatch(turn, turnDist, hasMargin, ref turnMatchCount, ref turnStartTime, "TURN", OnTurnFired);
            if (jumpMatchCount > 0) jumpMatchCount = 0;
        }

        // Store normalized frame for next delta
        Array.Copy(normMFCC, prevMFCC, MFCC_DIM);
        hasPrevious = true;
    }

    float ComputeTemplateDistance(VoiceTemplateOptimized template, float[] normMFCC, float[] delta)
    {
        if (template == null || !template.IsComplete)
            return float.MaxValue;

        // Distance to centroid (robust single-vector representation)
        float dist = FrameDistance(normMFCC, template.centroid);

        if (useDeltaFeatures && delta != null && template.deltaCoefficients != null)
        {
            float deltaDist = DeltaDistance(delta, template.deltaCoefficients);
            dist = dist * (1f - deltaWeight) + deltaDist * deltaWeight;
        }

        return dist;
    }

    void ProcessMatch(
        VoiceTemplateOptimized template,
        float dist,
        bool hasMargin,
        ref int matchCount,
        ref float matchStartTime,
        string commandName,
        Action<VoiceRecognitionMetrics> callback)
    {
        bool isMatch = dist < frameThreshold && hasMargin;

        if (isMatch)
        {
            if (matchCount == 0)
            {
                matchStartTime = Time.time;
                if (enableDebugLogs)
                    Debug.Log($"{commandName} START at {Time.time:F3}s (dist={dist:F3})");
            }

            matchCount++;

            if (matchCount >= minFramesRequired)
            {
                VoiceRecognitionMetrics metrics = new VoiceRecognitionMetrics
                {
                    speakStartTime = matchStartTime,
                    speakEndTime = Time.time,
                    invokeTime = Time.time,
                    command = commandName,
                    startFrame = frameCounter - matchCount,
                    endFrame = frameCounter,
                    invokeFrame = frameCounter
                };

                metrics.totalLatency = metrics.invokeTime - metrics.speakStartTime;
                metrics.processingLatency = 0f;
                metrics.frameLatency = matchCount;

                Fire(callback, metrics);
                matchCount = 0;
            }
        }
        else
        {
            if (matchCount > 0)
                matchCount = 0;
        }
    }

    float FrameDistance(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < MFCC_DIM; i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return Mathf.Sqrt(sum);
    }

    float DeltaDistance(float[] delta, float[] templateDelta)
    {
        float sum = 0f;
        for (int i = 0; i < MFCC_DIM; i++)
        {
            float d = delta[i] - templateDelta[i];
            sum += d * d;
        }
        return Mathf.Sqrt(sum);
    }

    void Fire(Action<VoiceRecognitionMetrics> callback, VoiceRecognitionMetrics metrics)
    {
        locked = true;
        unlockTime = Time.time + cooldown;

        if (trackMetrics)
        {
            recentLatencies.Add(metrics.totalLatency);
            if (recentLatencies.Count > 100)
                recentLatencies.RemoveAt(0);

            if (enableDebugLogs)
            {
                Debug.Log($"FIRED {metrics.command}! Latency: {metrics.totalLatency * 1000f:F1}ms");
            }
        }

        jumpMatchCount = 0;
        turnMatchCount = 0;

        callback(metrics);
    }

    void OnJumpFired(VoiceRecognitionMetrics metrics)
    {
        onJumpDetected?.Invoke();
        OnJumpMetrics?.Invoke(this, new VoiceEventArgs { Metrics = metrics });
    }

    void OnTurnFired(VoiceRecognitionMetrics metrics)
    {
        onTurnDetected?.Invoke();
        OnTurnMetrics?.Invoke(this, new VoiceEventArgs { Metrics = metrics });
    }

    // Public API
    public float GetAverageLatency()
    {
        if (recentLatencies.Count == 0) return 0f;
        float sum = 0f;
        foreach (var l in recentLatencies) sum += l;
        return sum / recentLatencies.Count;
    }

    public int GetSampleCount() => recentLatencies.Count;
    public int GetFrameCount() => frameCounter;
    public float GetUptime() => Time.time - startTime;
}
