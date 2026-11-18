using UnityEngine;
using System.Globalization;

public enum WaveType { Sine, Triangle, Square, Saw }

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public abstract class MusicSynth : MonoBehaviour
{
    [Range(0f, 1f)] public float masterGain = 0.9f;

    protected float sr = 48000f;
    protected AudioSource source;
    protected double clock;           // tiempo en segundos (para LFOs)
    float[][] tables = new float[4][];
    volatile bool useTables;
    volatile bool output; 
    uint noiseState = 22222;
    bool ready;

    static readonly float[] WaveAmp = { 1f, 1f, 0.6f, 0.6f };

    public AudioSource Source { get { return source; } }
    public bool IsPlaying { get { return source != null && source.isPlaying; } }
    public bool IsOn { get { return output; } }

    protected virtual void Awake()
    {
        sr = AudioSettings.outputSampleRate;
        source = GetComponent<AudioSource>();
        source.clip = null; source.loop = false; source.playOnAwake = false;
        source.spatialBlend = 0f; source.volume = 0f;
        source.Stop();
        output = false;
        InitSynth();
        ready = true;
    }

    public void Begin()
    {
        output = true;
        if (source.isPlaying) return;
        ResetState();
        source.Play();
    }

    public void End()
    {
        output = false;
        if (source.isPlaying) source.Stop();
    }

    // Carga las 4 wavetables (.txt). Si use = false o un slot esta vacio, se usa la formula matematica.
    public void SetWaveTables(TextAsset sine, TextAsset triangle, TextAsset square, TextAsset saw, bool use)
    {
        float[][] t = new float[4][];
        t[0] = Parse(sine); t[1] = Parse(triangle); t[2] = Parse(square); t[3] = Parse(saw);
        tables = t;
        useTables = use;
    }

    static float[] Parse(TextAsset a)
    {
        if (a == null) return null;
        string[] parts = a.text.Split(new[] { ',', '\n', '\r', '\t', ' ', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        float[] t = new float[parts.Length]; int n = 0; float peak = 0f;
        foreach (string p in parts)
            if (float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            { t[n++] = v; peak = Mathf.Max(peak, Mathf.Abs(v)); }
        if (n < 2 || peak < 1e-6f) return null;
        float[] r = new float[n];
        for (int i = 0; i < n; i++) r[i] = t[i] / peak;
        return r;
    }

    // Oscilador: phase en 0..1
    public float Osc(WaveType w, double phase)
    {
        float p = (float)phase;
        int wi = (int)w;
        float[][] tb = tables;
        if (useTables && tb[wi] != null)
        {
            float[] t = tb[wi];
            float pos = p * t.Length; int i0 = (int)pos; float f = pos - i0;
            i0 %= t.Length; int i1 = (i0 + 1) % t.Length;
            return (t[i0] * (1f - f) + t[i1] * f) * WaveAmp[wi];
        }
        switch (w)
        {
            case WaveType.Sine: return Mathf.Sin(6.2831853f * p);
            case WaveType.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
            case WaveType.Square: return p < 0.5f ? 0.6f : -0.6f;
            default: return (2f * p - 1f) * 0.6f;
        }
    }

    protected float Noise()
    {
        noiseState ^= noiseState << 13; noiseState ^= noiseState >> 17; noiseState ^= noiseState << 5;
        return (noiseState / (float)uint.MaxValue) * 2f - 1f;
    }

    protected float Lfo(float rate, float phaseOffset = 0f)
    {
        return (float)System.Math.Sin(6.283185307 * rate * clock + phaseOffset);
    }

    public static float Mtof(float midi) { return 440f * Mathf.Pow(2f, (midi - 69f) / 12f); }

    protected abstract void InitSynth();
    protected abstract void ResetState();
    protected abstract void Render(out float l, out float r);

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!ready || !output || channels <= 0) { System.Array.Clear(data, 0, data.Length); return; }
        double dt = 1.0 / sr;
        for (int i = 0; i < data.Length; i += channels)
        {
            clock += dt;
            Render(out float l, out float r);
            l = (float)System.Math.Tanh(l * masterGain * 1.4f);
            r = (float)System.Math.Tanh(r * masterGain * 1.4f);
            if (channels == 1) data[i] = (l + r) * 0.5f;
            else
            {
                data[i] = l; data[i + 1] = r;
                for (int c = 2; c < channels; c++) data[i + c] = (l + r) * 0.5f;
            }
        }
    }
}
