using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VoiceTemplateLite
{
    public List<MFCCFrame> windows = new(); // size = 3

    public bool IsComplete => windows.Count == 3;

    public void Add(float[] mfcc)
    {
        if (windows.Count < 3)
            windows.Add(new MFCCFrame(mfcc));
    }
}