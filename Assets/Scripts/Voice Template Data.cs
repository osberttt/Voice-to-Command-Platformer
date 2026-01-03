using System;
using System.Collections.Generic;

[Serializable]
public class VoiceTemplateData
{
    public List<MFCCSequence> jump = new();
    public List<MFCCSequence> turn = new();
}