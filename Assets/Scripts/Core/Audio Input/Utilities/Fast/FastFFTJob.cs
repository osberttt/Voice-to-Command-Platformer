using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

/// <summary>
/// Burst-compiled FFT for 10-20x speedup
/// </summary>
[BurstCompile]
public struct FastFFTJob : IJob
{
    [ReadOnly] public NativeArray<float> input;
    [WriteOnly] public NativeArray<float> magnitude;

    public void Execute()
    {
        int n = input.Length;
        int half = n / 2;

        // Use Cooley-Tukey FFT algorithm (O(n log n) instead of O(n²))
        NativeArray<float> real = new NativeArray<float>(n, Allocator.Temp);
        NativeArray<float> imag = new NativeArray<float>(n, Allocator.Temp);

        for (int i = 0; i < n; i++)
        {
            real[i] = input[i];
            imag[i] = 0f;
        }

        FFTCooleyTukey(real, imag, n);

        for (int k = 0; k < half; k++)
        {
            magnitude[k] = Mathf.Sqrt(real[k] * real[k] + imag[k] * imag[k]);
        }

        real.Dispose();
        imag.Dispose();
    }

    void FFTCooleyTukey(NativeArray<float> real, NativeArray<float> imag, int n)
    {
        if (n <= 1) return;

        // Bit reversal
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                float tempR = real[i];
                float tempI = imag[i];
                real[i] = real[j];
                imag[i] = imag[j];
                real[j] = tempR;
                imag[j] = tempI;
            }

            int k = n / 2;
            while (k <= j)
            {
                j -= k;
                k /= 2;
            }
            j += k;
        }

        // Cooley-Tukey
        for (int len = 2; len <= n; len *= 2)
        {
            float angle = -2f * Mathf.PI / len;
            float wlenR = Mathf.Cos(angle);
            float wlenI = Mathf.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                float wR = 1f;
                float wI = 0f;

                for (int k = 0; k < len / 2; k++)
                {
                    int idx1 = i + k;
                    int idx2 = i + k + len / 2;

                    float tR = wR * real[idx2] - wI * imag[idx2];
                    float tI = wR * imag[idx2] + wI * real[idx2];

                    real[idx2] = real[idx1] - tR;
                    imag[idx2] = imag[idx1] - tI;
                    real[idx1] = real[idx1] + tR;
                    imag[idx1] = imag[idx1] + tI;

                    float tempW = wR;
                    wR = wR * wlenR - wI * wlenI;
                    wI = tempW * wlenI + wI * wlenR;
                }
            }
        }
    }
}