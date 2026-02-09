using System;
using UnityEngine;

[System.Serializable]
public struct VoiceRecognitionMetrics
{
    public float speakStartTime;
    public float speakEndTime;
    public float invokeTime;
    public float totalLatency;
    public float processingLatency;
    public string command;
    
    // Frame-level tracking
    public int startFrame;
    public int endFrame;
    public int invokeFrame;
    public int frameLatency;

    public override string ToString()
    {
        return $"Command: {command}\n" +
               $"Start: {speakStartTime:F3}s (frame {startFrame})\n" +
               $"End: {speakEndTime:F3}s (frame {endFrame})\n" +
               $"Invoke: {invokeTime:F3}s (frame {invokeFrame})\n" +
               $"Total Latency: {totalLatency * 1000f:F1}ms ({frameLatency} frames)\n" +
               $"Processing Latency: {processingLatency * 1000f:F1}ms";
    }
}

public class VoiceEventArgs : EventArgs
{
    public VoiceRecognitionMetrics Metrics { get; set; }
}