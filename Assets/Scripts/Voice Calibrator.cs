using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
public class VoiceCalibrator : MonoBehaviour
{
    public AudioInput audioInput;

    public List<float[][]> jumpTemplates = new();
    public List<float[][]> turnTemplates = new();


    enum Mode { Jump, Turn }
    Mode mode;
    int count;

    void Start()
    {
        audioInput.OnCommandFinished += OnSample;
        StartJump();
    }

    private void OnDisable()
    {
        audioInput.OnCommandFinished -= OnSample;
    }

    void StartJump()
    {
        mode = Mode.Jump;
        count = 0;
        Debug.Log("Say JUMP 3 times");
    }

    void StartTurn()
    {
        mode = Mode.Turn;
        count = 0;
        Debug.Log("Say TURN 3 times");
    }

    void OnSample(float[] samples)
    {
        var mfccSequence = MFCC.Extract(samples);

        Debug.Log($"Frames: {mfccSequence.Length}, Coeffs: {mfccSequence[0].Length}");

        if (mode == Mode.Jump)
            jumpTemplates.Add(mfccSequence);
        else
            turnTemplates.Add(mfccSequence);

        count++;
        Debug.Log($"{mode} {count}/3");

        if (count >= 1)
        {
            if (mode == Mode.Jump)
                StartTurn();
            else
            {
                Debug.Log("Calibration complete");
                Debug.Log(
                    $"Jump templates: {jumpTemplates.Count}, " +
                    $"Frames: {jumpTemplates[0].Length}, " +
                    $"Coeffs: {jumpTemplates[0][0].Length}"
                );
                SaveTemplates();
                audioInput.OnCommandFinished -= OnSample;
            }
        }
    }

    
    void SaveTemplates()
    {
        VoiceTemplateData data = new();

        foreach (var t in jumpTemplates)
            data.jump.Add(new MFCCSequence(t));

        foreach (var t in turnTemplates)
            data.turn.Add(new MFCCSequence(t));

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(
            Application.persistentDataPath,
            "voice_templates.json"
        );

        File.WriteAllText(path, json);
        Debug.Log($"Templates saved to {path}");
    }

}