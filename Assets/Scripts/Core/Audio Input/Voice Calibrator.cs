using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine.UI;

public class VoiceCalibrator : MonoBehaviour
{
    public AudioInput audioInput;

    [Header("UI")]
    public TextMeshProUGUI instructionText;
    public Button startButton;
    public Button nextButton;

    enum Mode { None, Jump, Turn }
    Mode mode = Mode.None;

    const int TARGET_COUNT = 3;

    int currentIndex;
    bool waitingForNext;

    List<float[][]> collectedSamples = new();

    List<float[][]> jumpTemplates = new();
    List<float[][]> turnTemplates = new();

    void Start()
    {
        audioInput.OnCommandFinished += OnSample;

        startButton.onClick.AddListener(StartCalibration);
        nextButton.onClick.AddListener(OnNextPressed);

        nextButton.gameObject.SetActive(false);
        instructionText.text = "Voice calibration not started";

        audioInput.micEnabled = false;
    }

    void OnDisable()
    {
        audioInput.OnCommandFinished -= OnSample;
    }

    // =====================
    // FLOW
    // =====================

    void StartCalibration()
    {
        startButton.gameObject.SetActive(false);
        nextButton.gameObject.SetActive(true);
        StartJump();
    }

    void StartJump()
    {
        mode = Mode.Jump;
        BeginMode();
    }

    void StartTurn()
    {
        mode = Mode.Turn;
        BeginMode();
    }

    void BeginMode()
    {
        collectedSamples.Clear();
        currentIndex = 0;
        waitingForNext = false;

        EnableMic();
        UpdateInstruction();
    }

    void UpdateInstruction()
    {
        instructionText.text =
            $"Say \"{mode}\" {currentIndex}/{TARGET_COUNT}";
    }

    // =====================
    // AUDIO
    // =====================

    void OnSample(float[] samples)
    {
        if (waitingForNext || currentIndex >= TARGET_COUNT)
            return;

        DisableMic();

        float[][] mfcc = MFCC.Extract(samples);
        collectedSamples.Add(mfcc);

        currentIndex++;
        waitingForNext = true;

        UpdateInstruction();
        Debug.Log($"{mode} captured {currentIndex}/{TARGET_COUNT}");
    }

    // =====================
    // UI
    // =====================

    void OnNextPressed()
    {
        if (!waitingForNext)
        {
            Debug.Log("Speak first before pressing Next");
            return;
        }

        waitingForNext = false;

        if (currentIndex < TARGET_COUNT)
        {
            EnableMic();
        }
        else
        {
            FinishMode();
        }
    }

    // =====================
    // MIC CONTROL
    // =====================

    void EnableMic()
    {
        audioInput.micEnabled = true;
    }

    void DisableMic()
    {
        audioInput.micEnabled = false;
    }

    // =====================
    // PROCESSING
    // =====================

    void FinishMode()
    {
        if (mode == Mode.Jump)
        {
            jumpTemplates.AddRange(collectedSamples);
            StartTurn();
        }
        else
        {
            turnTemplates.AddRange(collectedSamples);
            SaveTemplates();

            instructionText.text = "Calibration complete";
            nextButton.gameObject.SetActive(false);
            DisableMic();
        }
    }

    // =====================
    // SAVE
    // =====================

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
        Debug.Log(
            $"Templates saved — Jump: {jumpTemplates.Count}, Turn: {turnTemplates.Count}"
        );
    }
}
