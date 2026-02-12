using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Optimized voice template with centroid matching and L2 normalization
/// </summary>
[System.Serializable]
public class VoiceTemplateOptimized
{
    public List<MFCCFrame> windows = new List<MFCCFrame>();

    public float[] energyProfile = new float[3];
    public float[] deltaCoefficients = new float[6];
    public float[] centroid = new float[6];
    public float autoThreshold = 2.0f;

    public bool IsComplete => windows.Count >= 2;

    public void BuildFromRecording(List<float[]> frames)
    {
        if (frames.Count < 5) return;

        int onsetIndex = FindOnset(frames);
        List<int> selectedIndices = SelectDiscriminativeFrames(frames, onsetIndex);

        // Compute centroid from ALL voiced frames (robust average)
        centroid = new float[6];
        for (int i = 0; i < frames.Count; i++)
            for (int j = 0; j < 6; j++)
                centroid[j] += frames[i][j];
        for (int j = 0; j < 6; j++)
            centroid[j] /= frames.Count;
        centroid = NormalizeMFCC(centroid);

        // Store normalized selected frames
        windows.Clear();
        foreach (int idx in selectedIndices)
            windows.Add(new MFCCFrame(NormalizeMFCC(frames[idx])));

        // Compute deltas from normalized frames
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
        // Select frames around onset: onset-1 through onset+3 (up to 5 frames)
        List<int> indices = new List<int>();
        for (int offset = -1; offset <= 3; offset++)
        {
            int idx = onset + offset;
            if (idx >= 0 && idx < frames.Count)
                indices.Add(idx);
        }
        return indices;
    }

    void ComputeDeltas(List<float[]> frames, List<int> indices)
    {
        if (indices.Count < 2) return;

        // Use normalized frames for consistent delta domain
        float[] norm0 = NormalizeMFCC(frames[indices[0]]);
        float[] norm1 = NormalizeMFCC(frames[indices[1]]);
        for (int i = 0; i < 6; i++)
            deltaCoefficients[i] = norm1[i] - norm0[i];
    }

    float ComputeEnergy(float[] mfcc)
    {
        float sum = 0f;
        for (int i = 0; i < mfcc.Length; i++)
            sum += Mathf.Abs(mfcc[i]);
        return sum;
    }

    public static float[] NormalizeMFCC(float[] mfcc)
    {
        float mag = 0f;
        for (int i = 0; i < mfcc.Length; i++)
            mag += mfcc[i] * mfcc[i];
        mag = Mathf.Sqrt(mag);

        if (mag < 1e-8f) return (float[])mfcc.Clone();

        float[] norm = new float[mfcc.Length];
        for (int i = 0; i < mfcc.Length; i++)
            norm[i] = mfcc[i] / mag;
        return norm;
    }
}
