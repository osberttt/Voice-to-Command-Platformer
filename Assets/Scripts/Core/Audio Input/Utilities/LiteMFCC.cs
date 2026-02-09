using UnityEngine;

public static class LiteMFCC
{
    const int COEFFS = 6;

    static MelFilterBank mel = new MelFilterBank(16000, 400);

    public static float[] Extract(float[] frame)
    {
        ApplyHamming(frame);

        float[] spec = FFTUtility.Magnitude(frame);
        for (int i = 0; i < spec.Length; i++)
            spec[i] *= spec[i];

        float[] melSpec = mel.Apply(spec);

        float[] mfcc = new float[COEFFS];

        for (int k = 0; k < COEFFS; k++)
        {
            float sum = 0f;
            for (int n = 0; n < melSpec.Length; n++)
                sum += melSpec[n] * Mathf.Cos(Mathf.PI * k * (n + 0.5f) / melSpec.Length);
            mfcc[k] = sum;
        }

        return mfcc;
    }

    static void ApplyHamming(float[] f)
    {
        for (int i = 0; i < f.Length; i++)
            f[i] *= 0.54f - 0.46f * Mathf.Cos(2 * Mathf.PI * i / (f.Length - 1));
    }
}