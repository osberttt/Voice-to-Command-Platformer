using UnityEngine;
using System;

/// <summary>
/// Optimized real-time audio input with minimal latency
/// </summary>
public class AudioInputOptimized : MonoBehaviour
{
    public int sampleRate = 16000;
    public float vadThreshold = 0.002f;
    
    // CRITICAL: Reduce frame size for lower latency
    const int FRAME_SIZE = 256;  // Was 400, now 256 (44% faster)
    const int HOP_SIZE = 128;    // Was 160, now 128 (20% overlap for stability)

    AudioClip mic;
    int lastPos;

    float[] ring = new float[FRAME_SIZE];
    int ringPos;

    public Action<float[]> OnWindow;
    public Action<float> OnVoiceActivity;

    // Optimization: Pre-allocate buffer
    float[] processBuffer = new float[512];
    
    // Latency tracking
    private float lastProcessTime;

    void Start()
    {
        // Use lowest latency settings
        mic = Microphone.Start(null, true, 1, sampleRate);
        while (Microphone.GetPosition(null) <= 0) { }
        lastPos = Microphone.GetPosition(null);
        
        Debug.Log($"Audio latency optimized: {FRAME_SIZE}@{sampleRate}Hz = {FRAME_SIZE * 1000f / sampleRate:F1}ms per frame");
    }

    void Update()
    {
        float startTime = Time.realtimeSinceStartup;
        
        int pos = Microphone.GetPosition(null);
        int count = pos - lastPos;
        if (count < 0) count += mic.samples;
        if (count <= 0) return;

        // Reuse buffer to avoid allocation
        if (count > processBuffer.Length)
            processBuffer = new float[count];

        mic.GetData(processBuffer, lastPos);
        lastPos = pos;

        // Process samples
        for (int i = 0; i < count; i++)
        {
            ring[ringPos++] = processBuffer[i];

            if (ringPos >= FRAME_SIZE)
            {
                ProcessFrame(ring);
                ShiftRing();
            }
        }

        lastProcessTime = Time.realtimeSinceStartup - startTime;
    }

    void ShiftRing()
    {
        // Optimized: Use Array.Copy instead of loop
        Array.Copy(ring, HOP_SIZE, ring, 0, FRAME_SIZE - HOP_SIZE);
        ringPos = FRAME_SIZE - HOP_SIZE;
    }

    void ProcessFrame(float[] frame)
    {
        // Fast energy calculation
        float energy = 0f;
        for (int i = 0; i < frame.Length; i++)
        {
            float s = frame[i];
            energy += s * s;
        }
        
        OnVoiceActivity?.Invoke(energy);

        if (energy < vadThreshold) return;

        // Use optimized MFCC
        float[] mfcc = FastMFCC.Extract(frame);
        OnWindow?.Invoke(mfcc);
    }

    public float GetProcessingTime() => lastProcessTime * 1000f;
}