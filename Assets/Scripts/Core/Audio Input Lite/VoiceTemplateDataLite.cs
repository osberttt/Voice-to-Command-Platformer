using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VoiceTemplateDataLite
{
    public List<MFCCFrame> jump = new();
    public List<MFCCFrame> turn = new();
}