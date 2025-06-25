using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nurture.MCP.Editor
{
    public static class Wav
    {
        public static void Write(AudioClip clip, Stream stream)
        {
            if (clip == null)
            {
                throw new Exception("AudioClip is null.");
            }

            using (var writer = new BinaryWriter(stream))
            {
                // Get audio data
                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                // --- WAV HEADER ---
                // RIFF chunk descriptor
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                // chunkSize (file size - 8 bytes for RIFF and chunkSize) will be calculated later
                writer.Write(0); // Placeholder for chunk size
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                // "fmt " sub-chunk 1
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size for PCM
                writer.Write((ushort)1); // AudioFormat (1 for PCM)
                ushort numChannels = (ushort)clip.channels;
                writer.Write(numChannels);
                uint sampleRate = (uint)clip.frequency;
                writer.Write(sampleRate);
                ushort bitsPerSample = 16; // Using 16-bit PCM
                ushort blockAlign = (ushort)(numChannels * bitsPerSample / 8);
                uint byteRate = sampleRate * blockAlign;
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);

                // "data" sub-chunk 2
                writer.Write(Encoding.ASCII.GetBytes("data"));
                // Subchunk2Size (data size) will be calculated later
                writer.Write(0); // Placeholder for data size

                // --- AUDIO DATA ---
                // Convert float samples to 16-bit PCM samples
                int sampleCount = samples.Length;
                short[] intData = new short[sampleCount];
                // Scaling factor for 16-bit PCM
                float rescaleFactor = 32767; // To avoid -32768 which has no positive equivalent

                for (int i = 0; i < sampleCount; i++)
                {
                    intData[i] = (short)(samples[i] * rescaleFactor);
                }

                // Write samples to the memory stream
                byte[] byteData = new byte[sampleCount * sizeof(short)];
                Buffer.BlockCopy(intData, 0, byteData, 0, byteData.Length);
                writer.Write(byteData);

                // --- UPDATE HEADER SIZES ---
                // Go back and write the actual sizes
                writer.Seek(4, SeekOrigin.Begin); // Seek to chunkSize field
                uint fileSize = (uint)stream.Length;
                writer.Write(fileSize - 8);

                writer.Seek(40, SeekOrigin.Begin); // Seek to Subchunk2Size field (data size)
                uint dataSize = (uint)(sampleCount * sizeof(short));
                writer.Write(dataSize);
            }
        }
    }
}
