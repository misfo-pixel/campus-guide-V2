using System;
using System.IO;
using UnityEngine;

public class MicrophoneRecorder : MonoBehaviour
{
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private int maxRecordSeconds = 10;
    [SerializeField] private string microphoneDevice = null;

    private AudioClip recordedClip;
    private string activeDevice;
    private bool isRecording = false;

    public bool IsRecording => isRecording;

    public void StartRecording()
    {
        if (isRecording) return;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[MicrophoneRecorder] No microphone device found.");
            return;
        }

        activeDevice = string.IsNullOrEmpty(microphoneDevice) ? Microphone.devices[0] : microphoneDevice;
        recordedClip = Microphone.Start(activeDevice, false, maxRecordSeconds, sampleRate);
        isRecording = true;

        Debug.Log("[MicrophoneRecorder] Recording started. Device = " + activeDevice);
    }

    public byte[] StopRecordingAndGetWav()
    {
        if (!isRecording)
        {
            Debug.LogWarning("[MicrophoneRecorder] Stop called, but recorder is not active.");
            return null;
        }

        int position = Microphone.GetPosition(activeDevice);
        Microphone.End(activeDevice);
        isRecording = false;

        if (position <= 0 || recordedClip == null)
        {
            Debug.LogError("[MicrophoneRecorder] Invalid recording position.");
            return null;
        }

        float[] samples = new float[position * recordedClip.channels];
        recordedClip.GetData(samples, 0);

        byte[] wavBytes = ConvertAudioClipDataToWav(samples, recordedClip.channels, sampleRate);
        Debug.Log("[MicrophoneRecorder] Recording stopped. Samples = " + samples.Length);

        return wavBytes;
    }

    private byte[] ConvertAudioClipDataToWav(float[] samples, int channels, int hz)
    {
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];

        const float rescaleFactor = 32767f;
        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        using MemoryStream stream = new MemoryStream();
        using BinaryWriter writer = new BinaryWriter(stream);

        int byteRate = hz * channels * 2;
        int subChunk2 = bytesData.Length;
        int chunkSize = 36 + subChunk2;

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(chunkSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });

        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(hz);
        writer.Write(byteRate);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);

        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(subChunk2);
        writer.Write(bytesData);

        writer.Flush();
        return stream.ToArray();
    }
}