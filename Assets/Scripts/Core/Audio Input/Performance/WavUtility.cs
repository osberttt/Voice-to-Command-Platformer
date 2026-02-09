using System;
using System.IO;
using UnityEngine;

/// <summary>
/// WAV file utility for saving and loading audio
/// </summary>
public static class WavUtility
{
    public static void Save(string filepath, float[] audioData, int sampleRate, int channels = 1)
    {
        using (FileStream fileStream = new FileStream(filepath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fileStream))
        {
            int sampleCount = audioData.Length;
            int byteRate = sampleRate * channels * 2; // 16-bit audio

            // WAV header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + sampleCount * 2);
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' });

            // fmt chunk
            writer.Write(new char[4] { 'f', 'm', 't', ' ' });
            writer.Write(16); // chunk size
            writer.Write((short)1); // audio format (PCM)
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2)); // block align
            writer.Write((short)16); // bits per sample

            // data chunk
            writer.Write(new char[4] { 'd', 'a', 't', 'a' });
            writer.Write(sampleCount * 2);

            // Write audio data
            foreach (float sample in audioData)
            {
                short intSample = (short)(sample * 32767f);
                writer.Write(intSample);
            }
        }

        Debug.Log($"Saved WAV file: {filepath}");
    }

    public static float[] Load(string filepath, out int sampleRate)
    {
        sampleRate = 0;

        if (!File.Exists(filepath))
        {
            Debug.LogError($"File not found: {filepath}");
            return null;
        }

        using (FileStream fileStream = new FileStream(filepath, FileMode.Open))
        using (BinaryReader reader = new BinaryReader(fileStream))
        {
            // Read header
            char[] riff = reader.ReadChars(4);
            if (new string(riff) != "RIFF")
            {
                Debug.LogError("Invalid WAV file");
                return null;
            }

            reader.ReadInt32(); // file size
            char[] wave = reader.ReadChars(4);
            if (new string(wave) != "WAVE")
            {
                Debug.LogError("Invalid WAV file");
                return null;
            }

            // Read fmt chunk
            reader.ReadChars(4); // "fmt "
            int fmtChunkSize = reader.ReadInt32();
            reader.ReadInt16(); // audio format
            int channels = reader.ReadInt16();
            sampleRate = reader.ReadInt32();
            reader.ReadInt32(); // byte rate
            reader.ReadInt16(); // block align
            int bitsPerSample = reader.ReadInt16();

            // Skip extra fmt bytes
            if (fmtChunkSize > 16)
                reader.ReadBytes(fmtChunkSize - 16);

            // Find data chunk
            while (fileStream.Position < fileStream.Length)
            {
                char[] chunkId = reader.ReadChars(4);
                int chunkSize = reader.ReadInt32();

                if (new string(chunkId) == "data")
                {
                    // Read audio data
                    int sampleCount = chunkSize / (bitsPerSample / 8);
                    float[] audioData = new float[sampleCount];

                    for (int i = 0; i < sampleCount; i++)
                    {
                        if (bitsPerSample == 16)
                        {
                            short sample = reader.ReadInt16();
                            audioData[i] = sample / 32768f;
                        }
                        else
                        {
                            Debug.LogWarning("Only 16-bit WAV supported");
                            return null;
                        }
                    }

                    return audioData;
                }
                else
                {
                    // Skip this chunk
                    reader.ReadBytes(chunkSize);
                }
            }
        }

        return null;
    }
}