using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Optimized calibration with better template selection
/// </summary>
public class VoiceCalibrationOptimized : MonoBehaviour
{
    public AudioInputOptimized audioInput;
    public TextMeshProUGUI statusText;

    VoiceTemplateDataOptimized data = new VoiceTemplateDataOptimized();

    enum Mode { None, Jump, Turn }
    Mode mode = Mode.None;

    List<float[]> buffer = new();

    bool recording;
    bool speechStarted;

    float lastWindowTime;
    float recordingStartTime;

    // CRITICAL: Reduce silence time for faster response
    const float SILENCE_TIME = 0.08f; // Was 0.15s, now 80ms
    const float MIN_DURATION = 0.15f; // Minimum command duration
    const float MAX_DURATION = 0.6f;  // Maximum command duration

    void OnEnable()
    {
        audioInput.OnWindow += OnWindow;
    }

    void OnDisable()
    {
        audioInput.OnWindow -= OnWindow;
    }

    void Update()
    {
        if (!recording || !speechStarted)
            return;

        float elapsed = Time.time - recordingStartTime;

        // Auto-stop if too long (prevents endless recording)
        if (elapsed > MAX_DURATION)
        {
            FinishRecording();
            return;
        }

        if (Time.time - lastWindowTime > SILENCE_TIME)
        {
            FinishRecording();
        }
    }

    void OnWindow(float[] mfcc)
    {
        if (!recording) return;

        if (!speechStarted)
        {
            speechStarted = true;
            recordingStartTime = Time.time;
        }

        lastWindowTime = Time.time;
        buffer.Add((float[])mfcc.Clone());
    }

    public void RecordJump() => StartRecording(Mode.Jump);
    public void RecordTurn() => StartRecording(Mode.Turn);

    void StartRecording(Mode m)
    {
        mode = m;
        buffer.Clear();

        recording = true;
        speechStarted = false;
        lastWindowTime = Time.time;

        statusText.text = $"Say {m.ToString().ToUpper()} quickly!";
    }

    void FinishRecording()
    {
        recording = false;

        float duration = Time.time - recordingStartTime;

        if (duration < MIN_DURATION)
        {
            statusText.text = $"Too short ({duration * 1000:F0}ms) ❌";
            return;
        }

        if (buffer.Count < 3)
        {
            statusText.text = "Too few frames ❌";
            return;
        }

        VoiceTemplateOptimized template = new VoiceTemplateOptimized();
        template.BuildFromRecording(buffer);

        if (!template.IsComplete)
        {
            statusText.text = "Failed ❌";
            return;
        }

        if (mode == Mode.Jump)
        {
            data.jump = template;
        }
        else
        {
            data.turn = template;
        }

        statusText.text = $"{mode} calibrated ✔ ({duration * 1000:F0}ms, {buffer.Count} frames)";
        Debug.Log($"Template: {template.windows.Count} windows, onset detected");
    }

    public void Save()
    {
        // Auto-calibrate threshold if both templates are complete
        if (data.jump.IsComplete && data.turn.IsComplete)
        {
            float interDist = CentroidDistance(data.jump.centroid, data.turn.centroid);
            data.autoThreshold = interDist * 0.5f;
            data.jump.autoThreshold = data.autoThreshold;
            data.turn.autoThreshold = data.autoThreshold;
            Debug.Log($"Auto-threshold: {data.autoThreshold:F3} (inter-template distance: {interDist:F3})");
        }

        string path = Path.Combine(
            Application.persistentDataPath,
            "voice_templates_optimized.json"
        );

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
        statusText.text = $"Saved (threshold: {data.autoThreshold:F2})";

        Debug.Log($"Saved to: {path}");
    }

    float CentroidDistance(float[] a, float[] b)
    {
        float sum = 0f;
        for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++)
        {
            float d = a[i] - b[i];
            sum += d * d;
        }
        return Mathf.Sqrt(sum);
    }
}

[System.Serializable]
public class VoiceTemplateDataOptimized
{
    public VoiceTemplateOptimized jump = new VoiceTemplateOptimized();
    public VoiceTemplateOptimized turn = new VoiceTemplateOptimized();
    public float autoThreshold = 2.0f;
}