using System.Collections.Generic;

[System.Serializable]
public class MFCCFrame
{
    public List<float> values;

    public MFCCFrame(float[] src)
    {
        values = new List<float>(src);
    }

    public float[] ToArray()
    {
        return values.ToArray();
    }
}