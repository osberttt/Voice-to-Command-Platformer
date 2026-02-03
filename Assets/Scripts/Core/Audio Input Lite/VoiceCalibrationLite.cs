using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.IO;

public class VoiceCalibrationLite : MonoBehaviour
{
    public AudioInputRealtime audio;
    public TextMeshProUGUI statusText;

    VoiceTemplateDataLite data = new();

    enum Mode { None, Jump, Turn }
    Mode mode = Mode.None;

    List<float[]> buffer = new();

    bool recording;
    bool speechStarted;

    float lastWindowTime;

    const float SILENCE_TIME = 0.15f; // 150 ms

    void OnEnable()
    {
        audio.OnWindow += OnWindow;
    }

    void OnDisable()
    {
        audio.OnWindow -= OnWindow;
    }

    void Update()
    {
        if (!recording || !speechStarted)
            return;

        if (Time.time - lastWindowTime > SILENCE_TIME)
        {
            FinishRecording();
        }
    }

    void OnWindow(float[] mfcc)
    {
        if (!recording) return;

        speechStarted = true;
        lastWindowTime = Time.time;
        buffer.Add(mfcc);
    }

    // ---------- UI ----------

    public void RecordJump() => StartRecording(Mode.Jump);
    public void RecordTurn() => StartRecording(Mode.Turn);

    void StartRecording(Mode m)
    {
        mode = m;
        buffer.Clear();

        recording = true;
        speechStarted = false;
        lastWindowTime = Time.time;

        statusText.text = $"Say {m.ToString().ToUpper()}";
    }

    // ---------- Finish ----------

    void FinishRecording()
    {
        recording = false;

        if (buffer.Count < 8)
        {
            statusText.text = "Too short ❌";
            return;
        }

        VoiceTemplateLite core = ExtractCore(buffer);

        if (!core.IsComplete)
        {
            statusText.text = "Failed ❌";
            return;
        }

        if (mode == Mode.Jump)
        {
            data.jump.Clear();
            foreach (var w in core.windows)
                data.jump.Add(w);

        }
        else
        {
            data.turn.Clear();
            foreach (var w in core.windows)
                data.turn.Add(w);

        }

        statusText.text = $"{mode} calibrated ✔";
    }

    // ---------- Core selection ----------

    VoiceTemplateLite ExtractCore(List<float[]> frames)
    {
        int peak = 0;
        float best = 0f;

        for (int i = 0; i < frames.Count; i++)
        {
            float energy = 0f;
            for (int j = 0; j < 6; j++)
                energy += Mathf.Abs(frames[i][j]);

            if (energy > best)
            {
                best = energy;
                peak = i;
            }
        }

        peak = Mathf.Clamp(peak, 1, frames.Count - 2);

        VoiceTemplateLite t = new();
        t.Add(frames[peak - 1]);
        t.Add(frames[peak]);
        t.Add(frames[peak + 1]);

        return t;
    }

    // ---------- Save ----------

    public void Save()
    {
        string path = Path.Combine(
            Application.persistentDataPath,
            "voice_templates_lite.json"
        );

        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        statusText.text = "Saved 💾";
    }
}
