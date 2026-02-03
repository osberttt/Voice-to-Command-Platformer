using UnityEngine;

public static class DTW
{
    const float INF = 1e20f;

    // =========================
    // NORMAL DTW 
    // =========================
    public static float Distance(float[][] a, float[][] b)
    {
        return DistanceInternal(a, b, b.Length);
    }

    // =========================
    // PREFIX DTW
    // =========================
    public static float PrefixDistance(float[][] input, float[][] template, int minPrefix)
    {
        float best = float.MaxValue;
        int maxPrefix = template.Length;

        for (int k = minPrefix; k <= maxPrefix; k++)
        {
            float d = DistanceInternal(input, template, k);
            best = Mathf.Min(best, d);
        }

        return best;
    }

    // =========================
    // CORE DTW
    // =========================
    static float DistanceInternal(float[][] a, float[][] b, int bLen)
    {
        int n = a.Length;
        int m = bLen;

        float[][] dtw = new float[n + 1][];
        for (int index = 0; index < n + 1; index++)
        {
            dtw[index] = new float[m + 1];
        }

        for (int i = 0; i <= n; i++)
        for (int j = 0; j <= m; j++)
            dtw[i][j] = INF;

        dtw[0][0] = 0f;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                float cost = FrameDistance(a[i - 1], b[j - 1]);

                dtw[i][j] = cost + Mathf.Min(
                    dtw[i - 1][j],
                    Mathf.Min(
                        dtw[i][j - 1],
                        dtw[i - 1][j - 1]
                    )
                );
            }
        }

        return dtw[n][m] / (n + m);
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