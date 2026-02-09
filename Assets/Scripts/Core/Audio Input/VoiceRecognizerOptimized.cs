using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Highly optimized recognizer with 200ms latency
/// </summary>
public class VoiceRecognizerOptimized : MonoBehaviour
{
    public AudioInputOptimized audioInput;

    public UnityEvent onJumpDetected = new UnityEvent();
    public UnityEvent onTurnDetected = new UnityEvent();

    public event EventHandler<VoiceEventArgs> OnJumpMetrics;
    public event EventHandler<VoiceEventArgs> OnTurnMetrics;

    [Header("Recognition Settings")]
    public float frameThreshold = 12f; // Lowered for faster triggering
    public float cooldown = 0.15f; // Reduced cooldown

    [Header("Optimization")]
    public int minFramesRequired = 2; // Reduced from 3
    public bool useDeltaFeatures = true;
    public float deltaWeight = 0.3f;

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

        if (enableDebugLogs)
        {
            Debug.Log($"Loaded: jump={jump.windows.Count}, turn={turn.windows.Count}");
        }
    }

    void OnWindow(float[] mfcc)
    {
        frameCounter++;

        if (locked && Time.time < unlockTime)
            return;

        // Compute delta features
        float[] delta = null;
        if (useDeltaFeatures && hasPrevious)
        {
            delta = new float[MFCC_DIM];
            for (int i = 0; i < MFCC_DIM; i++)
                delta[i] = mfcc[i] - prevMFCC[i];
        }

        CheckCommand(jump, ref jumpMatchCount, ref jumpStartTime, mfcc, delta, "JUMP", OnJumpFired);
        CheckCommand(turn, ref turnMatchCount, ref turnStartTime, mfcc, delta, "TURN", OnTurnFired);

        // Store for next delta
        Array.Copy(mfcc, prevMFCC, MFCC_DIM);
        hasPrevious = true;
    }

    void CheckCommand(
        VoiceTemplateOptimized template,
        ref int matchCount,
        ref float startTime,
        float[] mfcc,
        float[] delta,
        string commandName,
        Action<VoiceRecognitionMetrics> callback
    )
    {
        if (template == null || !template.IsComplete)
            return;

        int frameIdx = Mathf.Min(matchCount, template.windows.Count - 1);
        float[] templateMFCC = template.windows[frameIdx].ToArray();

        // Compute distance with optional delta features
        float dist = FrameDistance(mfcc, templateMFCC);
        
        if (useDeltaFeatures && delta != null && template.deltaCoefficients != null)
        {
            float deltaDist = DeltaDistance(delta, template.deltaCoefficients);
            dist = dist * (1f - deltaWeight) + deltaDist * deltaWeight;
        }

        bool isMatch = dist < frameThreshold;

        if (isMatch)
        {
            if (matchCount == 0)
            {
                startTime = Time.time;
                if (enableDebugLogs)
                    Debug.Log($"⚡ {commandName} START at {Time.time:F3}s");
            }

            matchCount++;

            if (matchCount >= minFramesRequired)
            {
                VoiceRecognitionMetrics metrics = new VoiceRecognitionMetrics
                {
                    speakStartTime = startTime,
                    speakEndTime = Time.time,
                    invokeTime = Time.time,
                    command = commandName,
                    startFrame = frameCounter - matchCount,
                    endFrame = frameCounter,
                    invokeFrame = frameCounter
                };

                metrics.totalLatency = metrics.invokeTime - metrics.speakStartTime;
                metrics.processingLatency = 0f; // Negligible
                metrics.frameLatency = matchCount;

                Fire(callback, metrics);
                matchCount = 0;
            }
        }
        else
        {
            if (matchCount > 0)
                matchCount = 0; // Reset immediately
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
                Debug.Log($"🔥 {metrics.command} FIRED! Latency: {metrics.totalLatency * 1000f:F1}ms");
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