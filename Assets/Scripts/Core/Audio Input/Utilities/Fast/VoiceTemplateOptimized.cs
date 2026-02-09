using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Optimized voice template with better feature selection
/// </summary>
[System.Serializable]
public class VoiceTemplateOptimized
{
    public List<MFCCFrame> windows = new List<MFCCFrame>();
    
    // Additional features for better discrimination
    public float[] energyProfile = new float[3];
    public float[] deltaCoefficients = new float[6]; // First derivatives
    
    public bool IsComplete => windows.Count >= 2; // Reduced from 3 for speed

    public void BuildFromRecording(List<float[]> frames)
    {
        if (frames.Count < 5) return;

        // Strategy 1: Find onset (rapid energy increase)
        int onsetIndex = FindOnset(frames);
        
        // Strategy 2: Select most discriminative frames
        List<int> selectedIndices = SelectDiscriminativeFrames(frames, onsetIndex);
        
        windows.Clear();
        foreach (int idx in selectedIndices)
        {
            windows.Add(new MFCCFrame(frames[idx]));
        }

        // Compute delta coefficients for better matching
        ComputeDeltas(frames, selectedIndices);
        
        // Store energy profile
        for (int i = 0; i < selectedIndices.Count && i < 3; i++)
        {
            energyProfile[i] = ComputeEnergy(frames[selectedIndices[i]]);
        }
    }

    int FindOnset(List<float[]> frames)
    {
        float[] energies = new float[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            energies[i] = ComputeEnergy(frames[i]);

        // Find maximum energy increase (onset detection)
        int onset = 0;
        float maxDelta = 0f;
        
        for (int i = 1; i < energies.Length; i++)
        {
            float delta = energies[i] - energies[i - 1];
            if (delta > maxDelta)
            {
                maxDelta = delta;
                onset = i;
            }
        }

        return Mathf.Clamp(onset, 1, frames.Count - 2);
    }

    List<int> SelectDiscriminativeFrames(List<float[]> frames, int onset)
    {
        // Select frames with highest variation (most discriminative)
        List<int> indices = new List<int>();
        
        // Always include onset
        indices.Add(onset);
        
        // Add frame right after onset (captures rising edge)
        if (onset + 1 < frames.Count)
            indices.Add(onset + 1);
        
        return indices;
    }

    void ComputeDeltas(List<float[]> frames, List<int> indices)
    {
        if (indices.Count < 2) return;

        for (int i = 0; i < 6; i++)
        {
            float delta = frames[indices[1]][i] - frames[indices[0]][i];
            deltaCoefficients[i] = delta;
        }
    }

    float ComputeEnergy(float[] mfcc)
    {
        float sum = 0f;
        for (int i = 0; i < mfcc.Length; i++)
            sum += Mathf.Abs(mfcc[i]);
        return sum;
    }
}