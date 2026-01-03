using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class VoiceRecognizer : MonoBehaviour
{
    public AudioInput audioInput;
    public float threshold = 1.2f;

    List<float[][]> jumpTemplates = new();
    List<float[][]> turnTemplates = new();

    void Start()
    {
        LoadTemplates();
        audioInput.OnCommandFinished += Recognize;
    }

    void OnDisable()
    {
        audioInput.OnCommandFinished -= Recognize;
    }

    void LoadTemplates()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "voice_templates.json"
        );

        if (!File.Exists(path))
        {
            Debug.LogError("Voice templates not found!");
            return;
        }

        string json = File.ReadAllText(path);
        VoiceTemplateData data = JsonUtility.FromJson<VoiceTemplateData>(json);

        jumpTemplates.Clear();
        turnTemplates.Clear();

        foreach (var seq in data.jump)
            jumpTemplates.Add(seq.ToMFCC());

        foreach (var seq in data.turn)
            turnTemplates.Add(seq.ToMFCC());

        Debug.Log(
            $"Loaded templates — Jump: {jumpTemplates.Count}, Turn: {turnTemplates.Count}"
        );
    }

    void Recognize(float[] samples)
    {
        if (jumpTemplates.Count == 0 || turnTemplates.Count == 0)
            return;

        float[][] input = MFCC.Extract(samples);

        float jumpDist = Best(input, jumpTemplates);
        float turnDist = Best(input, turnTemplates);

        Debug.Log($"DTW jump={jumpDist:F3}, turn={turnDist:F3}");

        if (jumpDist < turnDist && jumpDist < threshold)
            Debug.Log("JUMP");
        else if (turnDist < jumpDist && turnDist < threshold)
            Debug.Log("TURN");
        else
            Debug.Log("UNKNOWN");
    }

    float Best(float[][] input, List<float[][]> templates)
    {
        float best = float.MaxValue;

        foreach (var t in templates)
        {
            float d = DTW.Distance(input, t);
            if (d < best)
                best = d;
        }

        return best;
    }
}
