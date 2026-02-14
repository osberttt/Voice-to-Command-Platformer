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
    public float[] deltaCoefficients = new float[5];
    public float[] centroid = new float[5]; // c1-c5 only (skip c0 log energy)
    public float autoThreshold = 2.0f;

    public bool IsComplete => windows.Count >= 2;

    public void BuildFromRecording(List<float[]> frames)
    {
        if (frames.Count < 5) return;

        int onsetIndex = FindOnset(frames);
        List<int> selectedIndices = SelectDiscriminativeFrames(frames, onsetIndex);

        // Compute spectral centroid from c1-c5 (skip c0 = log energy, varies with volume not phonetics)
        centroid = new float[5];
        for (int i = 0; i < frames.Count; i++)
            for (int j = 0; j < 5; j++)
                centroid[j] += frames[i][j + 1];
        for (int j = 0; j < 5; j++)
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

        // Use spectral features (c1-c5) for consistent delta domain
        float[] spec0 = SpectralFeatures(frames[indices[0]]);
        float[] spec1 = SpectralFeatures(frames[indices[1]]);
        for (int i = 0; i < 5; i++)
            deltaCoefficients[i] = spec1[i] - spec0[i];
    }

    float ComputeEnergy(float[] mfcc)
    {
        float sum = 0f;
        for (int i = 0; i < mfcc.Length; i++)
            sum += Mathf.Abs(mfcc[i]);
        return sum;
    }

    /// <summary>
    /// Merge multiple recording templates into one robust template.
    /// Averages centroids and deltas; keeps onset frames from highest-energy recording.
    /// </summary>
    public static VoiceTemplateOptimized MergeTemplates(List<VoiceTemplateOptimized> templates)
    {
        if (templates == null || templates.Count == 0) return null;
        if (templates.Count == 1) return templates[0];

        var merged = new VoiceTemplateOptimized();

        // Average spectral centroids (c1-c5) across recordings, then re-normalize
        merged.centroid = new float[5];
        for (int t = 0; t < templates.Count; t++)
            for (int j = 0; j < 5; j++)
                merged.centroid[j] += templates[t].centroid[j];
        for (int j = 0; j < 5; j++)
            merged.centroid[j] /= templates.Count;
        merged.centroid = NormalizeMFCC(merged.centroid);

        // Average delta coefficients (c1-c5)
        merged.deltaCoefficients = new float[5];
        for (int t = 0; t < templates.Count; t++)
            for (int j = 0; j < 5; j++)
                merged.deltaCoefficients[j] += templates[t].deltaCoefficients[j];
        for (int j = 0; j < 5; j++)
            merged.deltaCoefficients[j] /= templates.Count;

        // Keep onset frames from the recording with highest total energy
        int bestIdx = 0;
        float bestEnergy = 0f;
        for (int t = 0; t < templates.Count; t++)
        {
            float e = 0f;
            for (int i = 0; i < templates[t].energyProfile.Length; i++)
                e += templates[t].energyProfile[i];
            if (e > bestEnergy) { bestEnergy = e; bestIdx = t; }
        }
        merged.windows = new List<MFCCFrame>(templates[bestIdx].windows);
        merged.energyProfile = (float[])templates[bestIdx].energyProfile.Clone();

        return merged;
    }

    /// <summary>
    /// Compute average distance from each recording's centroid to the merged centroid.
    /// Used for within-class variance estimation.
    /// </summary>
    public static float ComputeSpread(List<VoiceTemplateOptimized> templates, float[] mergedCentroid)
    {
        if (templates == null || templates.Count <= 1) return 0f;

        float totalDist = 0f;
        for (int t = 0; t < templates.Count; t++)
        {
            float sum = 0f;
            int dim = Mathf.Min(templates[t].centroid.Length, mergedCentroid.Length);
            for (int j = 0; j < dim; j++)
            {
                float d = templates[t].centroid[j] - mergedCentroid[j];
                sum += d * d;
            }
            totalDist += Mathf.Sqrt(sum);
        }
        return totalDist / templates.Count;
    }

    /// <summary>
    /// Extract spectral shape features: c1-c5 from raw MFCC, L2-normalized.
    /// Skips c0 (log energy) which varies with volume, not phonetics.
    /// </summary>
    public static float[] SpectralFeatures(float[] mfcc)
    {
        float[] spec = new float[5];
        for (int i = 0; i < 5; i++)
            spec[i] = mfcc[i + 1];
        return NormalizeMFCC(spec);
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
