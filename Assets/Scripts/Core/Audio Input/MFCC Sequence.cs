using System;

[Serializable]
public class MFCCSequence
{
    public int frames;
    public int coeffs;
    public float[] data; // flattened [frame][coeff]

    public MFCCSequence(float[][] mfcc)
    {
        frames = mfcc.Length;
        coeffs = mfcc[0].Length;

        data = new float[frames * coeffs];

        int idx = 0;
        for (int i = 0; i < frames; i++)
        for (int j = 0; j < coeffs; j++)
            data[idx++] = mfcc[i][j];
    }

    public float[][] ToMFCC()
    {
        float[][] mfcc = new float[frames][];
        int idx = 0;

        for (int i = 0; i < frames; i++)
        {
            mfcc[i] = new float[coeffs];
            for (int j = 0; j < coeffs; j++)
                mfcc[i][j] = data[idx++];
        }

        return mfcc;
    }
}