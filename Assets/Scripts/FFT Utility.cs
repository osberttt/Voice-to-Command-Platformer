using UnityEngine;

public static class FFTUtility
{
    public static float[] Magnitude(float[] samples)
    {
        int n = samples.Length;
        int half = n / 2;

        float[] mag = new float[half];

        for (int k = 0; k < half; k++)
        {
            float re = 0f;
            float im = 0f;

            for (int t = 0; t < n; t++)
            {
                float angle = 2 * Mathf.PI * k * t / n;
                re += samples[t] * Mathf.Cos(angle);
                im -= samples[t] * Mathf.Sin(angle);
            }

            mag[k] = Mathf.Sqrt(re * re + im * im);
        }

        return mag;
    }
}