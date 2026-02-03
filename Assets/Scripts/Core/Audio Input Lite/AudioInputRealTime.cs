using UnityEngine;
using System;
using System.Collections.Generic;

public class AudioInputRealtime : MonoBehaviour
{
    public int sampleRate = 16000;
    public float vadThreshold = 0.002f;

    const int FRAME_SIZE = 400;
    const int HOP_SIZE = 160;

    AudioClip mic;
    int lastPos;

    float[] ring = new float[FRAME_SIZE];
    int ringPos;

    public Action<float[]> OnWindow; // MFCC window

    void Start()
    {
        mic = Microphone.Start(null, true, 1, sampleRate);
        while (Microphone.GetPosition(null) <= 0) {}
        lastPos = Microphone.GetPosition(null);
    }

    void Update()
    {
        int pos = Microphone.GetPosition(null);
        int count = pos - lastPos;
        if (count < 0) count += mic.samples;
        if (count <= 0) return;

        float[] buffer = new float[count];
        mic.GetData(buffer, lastPos);
        lastPos = pos;

        foreach (float s in buffer)
        {
            ring[ringPos++] = s;

            if (ringPos >= FRAME_SIZE)
            {
                ProcessFrame(ring);
                ShiftRing();
            }
        }
    }

    void ShiftRing()
    {
        Array.Copy(ring, HOP_SIZE, ring, 0, FRAME_SIZE - HOP_SIZE);
        ringPos = FRAME_SIZE - HOP_SIZE;
    }

    void ProcessFrame(float[] frame)
    {
        float energy = 0f;
        foreach (float s in frame) energy += s * s;
        if (energy < vadThreshold) return;

        float[] mfcc = LiteMFCC.Extract(frame);
        OnWindow?.Invoke(mfcc);
    }
}
