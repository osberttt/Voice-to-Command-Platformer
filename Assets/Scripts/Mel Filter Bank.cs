using UnityEngine;

public class MelFilterBank
{
    int numFilters = 26;
    int fftSize;
    float sampleRate;
    float[][] filters;

    public MelFilterBank(float sampleRate, int frameSize)
    {
        this.sampleRate = sampleRate;
        this.fftSize = frameSize;
        BuildFilters();
    }

    void BuildFilters()
    {
        int nfft = fftSize / 2;
        filters = new float[numFilters][];

        float fMin = 0;
        float fMax = sampleRate / 2;

        float melMin = HzToMel(fMin);
        float melMax = HzToMel(fMax);

        float[] melPoints = new float[numFilters + 2];
        for (int i = 0; i < melPoints.Length; i++)
            melPoints[i] = melMin + (melMax - melMin) * i / (numFilters + 1);

        float[] hzPoints = new float[melPoints.Length];
        for (int i = 0; i < hzPoints.Length; i++)
            hzPoints[i] = MelToHz(melPoints[i]);

        int[] bins = new int[hzPoints.Length];
        for (int i = 0; i < bins.Length; i++)
            bins[i] = Mathf.FloorToInt((fftSize + 1) * hzPoints[i] / sampleRate);

        for (int i = 0; i < numFilters; i++)
        {
            filters[i] = new float[nfft];

            for (int k = bins[i]; k < bins[i + 1]; k++)
                if (k >= 0 && k < nfft)
                    filters[i][k] = (float)(k - bins[i]) / (bins[i + 1] - bins[i]);

            for (int k = bins[i + 1]; k < bins[i + 2]; k++)
                if (k >= 0 && k < nfft)
                    filters[i][k] = (float)(bins[i + 2] - k) / (bins[i + 2] - bins[i + 1]);
        }
    }

    public float[] Apply(float[] spectrum)
    {
        float[] mel = new float[numFilters];

        for (int i = 0; i < numFilters; i++)
        {
            float sum = 0f;
            for (int k = 0; k < spectrum.Length; k++)
                sum += spectrum[k] * filters[i][k];

            mel[i] = Mathf.Max(sum, 1e-10f);
        }

        return mel;
    }

    static float HzToMel(float hz) =>
        2595f * Mathf.Log10(1 + hz / 700f);

    static float MelToHz(float mel) =>
        700f * (Mathf.Pow(10, mel / 2595f) - 1);
}
