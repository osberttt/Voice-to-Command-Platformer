using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// Optimized MFCC extraction using Job System
/// </summary>
public static class FastMFCC
{
    const int COEFFS = 6;
    static MelFilterBank mel = new MelFilterBank(16000, 400);

    // Pre-allocated arrays to avoid GC
    static float[] hammingWindow = new float[400];
    static bool initialized = false;

    static void Initialize()
    {
        if (initialized) return;

        // Pre-compute Hamming window
        for (int i = 0; i < 400; i++)
            hammingWindow[i] = 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * i / 399f);

        initialized = true;
    }

    public static float[] Extract(float[] frame)
    {
        Initialize();

        // Apply Hamming (vectorized)
        for (int i = 0; i < frame.Length; i++)
            frame[i] *= hammingWindow[i];

        // Use Job System for FFT
        NativeArray<float> input = new NativeArray<float>(frame, Allocator.TempJob);
        NativeArray<float> magnitude = new NativeArray<float>(frame.Length / 2, Allocator.TempJob);

        FastFFTJob job = new FastFFTJob
        {
            input = input,
            magnitude = magnitude
        };

        job.Schedule().Complete();

        // Power spectrum
        float[] spec = new float[magnitude.Length];
        for (int i = 0; i < magnitude.Length; i++)
            spec[i] = magnitude[i] * magnitude[i];

        input.Dispose();
        magnitude.Dispose();

        // Mel filtering
        float[] melSpec = mel.Apply(spec);

        // DCT
        float[] mfcc = new float[COEFFS];
        float dctScale = Mathf.PI / melSpec.Length;

        for (int k = 0; k < COEFFS; k++)
        {
            float sum = 0f;
            for (int n = 0; n < melSpec.Length; n++)
                sum += melSpec[n] * Mathf.Cos(k * (n + 0.5f) * dctScale);
            mfcc[k] = sum;
        }

        return mfcc;
    }
}