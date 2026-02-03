using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class VoiceRecognizer : MonoBehaviour
{
    public PlayerMovement playerMovement;
    public AudioInput audioInput;

    [Header("Thresholds")]
    public float earlyThreshold = 6f;
    public float finalThreshold = 10f;
    public float confidenceMargin = 1.5f;

    List<float[][]> jumpTemplates = new();
    List<float[][]> turnTemplates = new();

    bool actionTriggered;

    void Start()
    {
        LoadTemplates();

        audioInput.OnSpeechProgress += RecognizePartial;
        audioInput.OnCommandFinished += RecognizeFinal;
    }

    void OnDisable()
    {
        audioInput.OnSpeechProgress -= RecognizePartial;
        audioInput.OnCommandFinished -= RecognizeFinal;
    }

    // =========================
    // LOAD
    // =========================
    void LoadTemplates()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "voice_templates.json"
        );

        if (!File.Exists(path))
        {
            Debug.LogError("Voice templates not found");
            return;
        }

        var data = JsonUtility.FromJson<VoiceTemplateData>(File.ReadAllText(path));

        jumpTemplates.Clear();
        turnTemplates.Clear();

        foreach (var t in data.jump)
            jumpTemplates.Add(t.ToMFCC());

        foreach (var t in data.turn)
            turnTemplates.Add(t.ToMFCC());

        Debug.Log($"Templates loaded — Jump {jumpTemplates.Count}, Turn {turnTemplates.Count}");
    }

    // =========================
    // EARLY (PREFIX)
    // =========================
    void RecognizePartial(float[] samples)
    {
        if (actionTriggered)
            return;

        float[][] input = MFCC.Extract(samples);
        if (input.Length < 3)
            return;

        float jumpDist = BestPrefix(input, jumpTemplates);
        float turnDist = BestPrefix(input, turnTemplates);

        if (jumpDist + confidenceMargin < turnDist && jumpDist < earlyThreshold)
        {
            TriggerJump(true);
        }
        else if (turnDist + confidenceMargin < jumpDist && turnDist < earlyThreshold)
        {
            TriggerTurn(true);
        }
    }

    // =========================
    // FINAL
    // =========================
    void RecognizeFinal(float[] samples)
    {
        actionTriggered = false;

        float[][] input = MFCC.Extract(samples);
        if (input.Length == 0)
            return;

        float jumpDist = BestFull(input, jumpTemplates);
        float turnDist = BestFull(input, turnTemplates);

        if (jumpDist + confidenceMargin < turnDist && jumpDist < finalThreshold)
            TriggerJump(false);
        else if (turnDist + confidenceMargin < jumpDist && turnDist < finalThreshold)
            TriggerTurn(false);
    }

    // =========================
    // HELPERS
    // =========================
    float BestPrefix(float[][] input, List<float[][]> templates)
    {
        float best = float.MaxValue;

        foreach (var t in templates)
        {
            int minPrefix = Mathf.Max(3, input.Length);
            float d = DTW.PrefixDistance(input, t, minPrefix);
            best = Mathf.Min(best, d);
        }

        return best;
    }

    float BestFull(float[][] input, List<float[][]> templates)
    {
        float best = float.MaxValue;

        foreach (var t in templates)
            best = Mathf.Min(best, DTW.Distance(input, t));

        return best;
    }

    void TriggerJump(bool early)
    {
        actionTriggered = true;
        playerMovement.RequestJump();
        Debug.Log(early ? "EARLY JUMP" : "FINAL JUMP");
    }

    void TriggerTurn(bool early)
    {
        actionTriggered = true;
        playerMovement.RequestTurn();
        Debug.Log(early ? "EARLY TURN" : "FINAL TURN");
    }
}
