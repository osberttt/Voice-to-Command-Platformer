using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

/// <summary>
/// Records multiple repetitions of voice commands for accuracy testing
/// </summary>
public class VoiceTestRecorder : MonoBehaviour
{
    public AudioInputOptimized audioInput;
    public TextMeshProUGUI statusText;

    [Header("Recording Settings")]
    public int targetRepetitions = 5;
    public float silenceThreshold = 0.15f;
    public float minCommandDuration = 0.3f;

    private enum RecordingMode { None, Jump, Turn }
    private RecordingMode currentMode = RecordingMode.None;

    private List<float> recordedAudio = new List<float>();
    private List<CommandMarker> commandMarkers = new List<CommandMarker>();
    
    private bool isRecording;
    private float lastVoiceActivity;
    private float commandStartTime;
    private bool inCommand;
    private int detectedCommands;

    private AudioClip mic;
    private int recordStartPosition;

    [System.Serializable]
    private struct CommandMarker
    {
        public float startTime;
        public float endTime;
        public int startSample;
        public int endSample;
    }

    void Start()
    {
        if (audioInput != null)
        {
            audioInput.OnVoiceActivity += OnVoiceActivity;
        }
    }

    void OnDisable()
    {
        if (audioInput != null)
        {
            audioInput.OnVoiceActivity -= OnVoiceActivity;
        }
    }

    void Update()
    {
        if (!isRecording) return;

        // Capture audio samples
        CaptureAudioSamples();

        // Detect command boundaries
        if (Time.time - lastVoiceActivity > silenceThreshold)
        {
            if (inCommand)
            {
                // Command ended
                float duration = Time.time - commandStartTime;
                if (duration >= minCommandDuration)
                {
                    CommandMarker marker = new CommandMarker
                    {
                        startTime = commandStartTime,
                        endTime = Time.time,
                        startSample = (int)(commandStartTime * audioInput.sampleRate),
                        endSample = recordedAudio.Count
                    };
                    commandMarkers.Add(marker);
                    detectedCommands++;

                    UpdateStatus();

                    if (detectedCommands >= targetRepetitions)
                    {
                        FinishRecording();
                    }
                }

                inCommand = false;
            }
        }
    }

    void OnVoiceActivity(float energy)
    {
        if (!isRecording) return;

        if (energy > audioInput.vadThreshold)
        {
            lastVoiceActivity = Time.time;

            if (!inCommand)
            {
                // Command started
                inCommand = true;
                commandStartTime = Time.time;
            }
        }
    }

    void CaptureAudioSamples()
    {
        int currentPos = Microphone.GetPosition(null);
        int samplesToRead;

        if (currentPos < recordStartPosition)
        {
            // Wrapped around
            samplesToRead = mic.samples - recordStartPosition;
            float[] temp = new float[samplesToRead];
            mic.GetData(temp, recordStartPosition);
            recordedAudio.AddRange(temp);

            samplesToRead = currentPos;
            if (samplesToRead > 0)
            {
                temp = new float[samplesToRead];
                mic.GetData(temp, 0);
                recordedAudio.AddRange(temp);
            }

            recordStartPosition = currentPos;
        }
        else
        {
            samplesToRead = currentPos - recordStartPosition;
            if (samplesToRead > 0)
            {
                float[] temp = new float[samplesToRead];
                mic.GetData(temp, recordStartPosition);
                recordedAudio.AddRange(temp);
                recordStartPosition = currentPos;
            }
        }
    }

    // UI Methods
    public void StartRecordingJump()
    {
        StartRecording(RecordingMode.Jump);
    }

    public void StartRecordingTurn()
    {
        StartRecording(RecordingMode.Turn);
    }

    void StartRecording(RecordingMode mode)
    {
        currentMode = mode;
        recordedAudio.Clear();
        commandMarkers.Clear();
        detectedCommands = 0;
        inCommand = false;

        mic = Microphone.Start(null, true, 10, audioInput.sampleRate);
        while (Microphone.GetPosition(null) <= 0) { }
        recordStartPosition = Microphone.GetPosition(null);

        isRecording = true;
        lastVoiceActivity = Time.time;

        UpdateStatus();
    }

    void FinishRecording()
    {
        isRecording = false;
        Microphone.End(null);

        SaveRecording();
    }

    void SaveRecording()
    {
        string filename = $"test_{currentMode.ToString().ToLower()}_{System.DateTime.Now:yyyyMMdd_HHmmss}.wav";
        string path = Path.Combine(Application.persistentDataPath, filename);

        // Save as WAV
        WavUtility.Save(path, recordedAudio.ToArray(), audioInput.sampleRate);

        // Save markers
        string markerFilename = filename.Replace(".wav", "_markers.json");
        string markerPath = Path.Combine(Application.persistentDataPath, markerFilename);

        TestRecordingData testData = new TestRecordingData
        {
            command = currentMode.ToString(),
            sampleRate = audioInput.sampleRate,
            totalSamples = recordedAudio.Count,
            markers = new List<CommandMarker>(commandMarkers)
        };

        File.WriteAllText(markerPath, JsonUtility.ToJson(testData, true));

        statusText.text = $"Saved {detectedCommands} {currentMode} commands!\n{filename}";
        Debug.Log($"Test recording saved to: {path}");
    }

    void UpdateStatus()
    {
        statusText.text = $"Recording {currentMode}...\n{detectedCommands}/{targetRepetitions} detected";
    }

    [System.Serializable]
    private class TestRecordingData
    {
        public string command;
        public int sampleRate;
        public int totalSamples;
        public List<CommandMarker> markers;
    }
}