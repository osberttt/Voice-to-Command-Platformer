using UnityEngine;

public static class DTW
{
    public static float Distance(float[][] a, float[][] b)
    {
        int n = a.Length;
        int m = b.Length;

        float[,] dtw = new float[n + 1, m + 1];

        const float INF = 1e20f;

        for (int i = 0; i <= n; i++)
        for (int j = 0; j <= m; j++)
            dtw[i, j] = INF;

        dtw[0, 0] = 0f;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                float cost = FrameDistance(a[i - 1], b[j - 1]);

                dtw[i, j] = cost + Mathf.Min(
                    dtw[i - 1, j],     // insertion
                    Mathf.Min(
                        dtw[i, j - 1], // deletion
                        dtw[i - 1, j - 1] // match
                    )
                );
            }
        }

        // normalize by path length
        return dtw[n, m] / (n + m);
    }

    static float FrameDistance(float[] x, float[] y)
    {
        float sum = 0f;
        for (int i = 0; i < x.Length; i++)
        {
            float d = x[i] - y[i];
            sum += d * d;
        }
        return Mathf.Sqrt(sum);
    }
}