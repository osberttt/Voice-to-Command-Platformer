using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class AudioInput : MonoBehaviour
{
    public int sampleRate = 16000;
    public float vadThreshold = 0.002f;
    public float silenceTime = 0.12f;

    const int WINDOW_SIZE = 160; // 10 ms @ 16kHz
    const int PRE_ROLL_WINDOWS = 3; // keep a little before speech

    AudioClip micClip;
    int lastSamplePos;

    int silenceSamples;
    int silenceSampleLimit;

    bool isSpeaking;

    List<float> currentSamples = new();
    Queue<float[]> preRoll = new();

    List<float> window = new();

    public Action<float[]> OnCommandFinished;

    void Start()
    {
        silenceSampleLimit = Mathf.RoundToInt(silenceTime * sampleRate);
        StartCoroutine(StartCo());
    }

    IEnumerator StartCo()
    {
        micClip = Microphone.Start(null, true, 1, sampleRate);

        while (Microphone.GetPosition(null) <= 0)
            yield return null;

        lastSamplePos = Microphone.GetPosition(null);
        Debug.Log("Mic started");
    }

    void Update()
    {
        if (micClip == null)
            return;

        int pos = Microphone.GetPosition(null);
        if (pos <= 0)
            return;

        int samplesToRead = pos - lastSamplePos;
        if (samplesToRead < 0)
            samplesToRead += micClip.samples;

        if (samplesToRead <= 0)
            return;

        float[] buffer = new float[samplesToRead];
        micClip.GetData(buffer, lastSamplePos);
        lastSamplePos = pos;

        foreach (float s in buffer)
        {
            window.Add(s);
            if (window.Count >= WINDOW_SIZE)
            {
                ProcessWindow(window);
                window.Clear();
            }
        }
    }

    void ProcessWindow(List<float> samples)
    {
        float rms = 0f;
        foreach (float s in samples)
            rms += s * s;

        rms = Mathf.Sqrt(rms / samples.Count);

        // Keep pre-roll
        preRoll.Enqueue(samples.ToArray());
        if (preRoll.Count > PRE_ROLL_WINDOWS)
            preRoll.Dequeue();

        if (rms > vadThreshold)
        {
            if (!isSpeaking)
            {
                isSpeaking = true;
                silenceSamples = 0;

                // prepend pre-roll
                foreach (var w in preRoll)
                    currentSamples.AddRange(w);

                Debug.Log("Speech started");
            }

            currentSamples.AddRange(samples);
        }
        else if (isSpeaking)
        {
            silenceSamples += samples.Count;

            if (silenceSamples < silenceSampleLimit)
            {
                // keep short silence inside word
                currentSamples.AddRange(samples);
            }
            else
            {
                EndCommand();
            }
        }
    }

    void EndCommand()
    {
        isSpeaking = false;
        silenceSamples = 0;
        preRoll.Clear();

        // Amplitude sanity check
        float max = 0f;
        foreach (float s in currentSamples)
            max = Mathf.Max(max, Mathf.Abs(s));

        if (currentSamples.Count > sampleRate * 0.08f && max > 0.01f)
        {
            Debug.Log($"Command finished | samples={currentSamples.Count}");
            OnCommandFinished?.Invoke(currentSamples.ToArray());
        }

        currentSamples.Clear();
    }
}
