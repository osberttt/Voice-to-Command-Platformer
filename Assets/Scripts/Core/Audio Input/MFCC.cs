using System.Collections.Generic;
using UnityEngine;

public static class MFCC
{
    const int numCoeffs = 13;
    const int frameSize = 400;
    const int hopSize = 160;
    const float EPS = 1e-6f;

    static MelFilterBank melBank = new MelFilterBank(16000, frameSize);

    public static float[][] Extract(float[] samples)
    {
        List<float[]> frames = new();

        for (int i = 0; i + frameSize <= samples.Length; i += hopSize)
        {
            float[] frame = new float[frameSize];
            System.Array.Copy(samples, i, frame, 0, frameSize);

            // Energy gate (CRITICAL)
            float energy = 0f;
            for (int j = 0; j < frame.Length; j++)
                energy += frame[j] * frame[j];

            if (energy < 1e-8f)
                continue;

            ApplyHamming(frame);

            float[] spectrum = FFTUtility.Magnitude(frame);

            // Power spectrum
            for (int k = 0; k < spectrum.Length; k++)
                spectrum[k] = spectrum[k] * spectrum[k];

            float[] mel = melBank.Apply(spectrum);

            // Log-mel
            for (int j = 0; j < mel.Length; j++)
                mel[j] = Mathf.Log(mel[j] + EPS);

            float[] mfcc = DCT(mel, numCoeffs);

            // NaN protection
            for (int j = 0; j < mfcc.Length; j++)
            {
                if (float.IsNaN(mfcc[j]) || float.IsInfinity(mfcc[j]))
                    mfcc[j] = 0f;
            }

            frames.Add(mfcc);
        }

        return frames.ToArray();
    }

    static void ApplyHamming(float[] f)
    {
        for (int i = 0; i < f.Length; i++)
            f[i] *= 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * i / (f.Length - 1));
    }

    static float[] DCT(float[] input, int coeffs)
    {
        float[] result = new float[coeffs];

        for (int k = 0; k < coeffs; k++)
        {
            float sum = 0f;
            for (int n = 0; n < coeffs; n++)
                sum += input[n] * Mathf.Cos(Mathf.PI * k * (n + 0.5f) / coeffs);

            result[k] = sum;
        }

        return result;
    }
}
