using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// Optimized MFCC extraction using Job System
/// </summary>
public static class FastMFCC
{
    const int COEFFS = 6;
    const int FRAME_SIZE = 256; // Match AudioInputOptimized.FRAME_SIZE
    static MelFilterBank mel = new MelFilterBank(16000, FRAME_SIZE);

    // Pre-allocated arrays to avoid GC
    static float[] hammingWindow = new float[FRAME_SIZE];
    static bool initialized = false;

    static void Initialize()
    {
        if (initialized) return;

        // Pre-compute Hamming window sized to actual frame
        for (int i = 0; i < FRAME_SIZE; i++)
            hammingWindow[i] = 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * i / (FRAME_SIZE - 1f));

        initialized = true;
    }

    public static float[] Extract(float[] frame)
    {
        Initialize();

        // Apply Hamming
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

        // Log compression (standard MFCC step — mel bank floors at 1e-10)
        for (int n = 0; n < melSpec.Length; n++)
            melSpec[n] = Mathf.Log(melSpec[n]);

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
