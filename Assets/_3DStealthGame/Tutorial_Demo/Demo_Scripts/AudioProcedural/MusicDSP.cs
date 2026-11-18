using UnityEngine;

public class OnePole
{
    float z;
    public float Process(float x, float a) { z += a * (x - z); return z; }
    public void Reset() { z = 0f; }
    public static float Coef(float cutoff, float sr) { return 1f - Mathf.Exp(-6.2831853f * cutoff / sr); }
}

// Nota con envolvente (ADSR)
public class Voice
{
    readonly MusicSynth o;
    public double phase;
    public float freq, amp, attack = 0.005f, decayTau = 0.2f, sustain, releaseTau = 0.1f, age, relAge, envRel;
    public WaveType wave;
    public bool gate, active;

    public Voice(MusicSynth owner) { o = owner; }

    public void Trigger(float f, float a, WaveType w, float atk, float dTau, float sus, float rTau)
    {
        freq = f; amp = a; wave = w; attack = atk; decayTau = dTau; sustain = sus; releaseTau = rTau;
        age = 0f; gate = true; active = true;
    }

    public void NoteOff() { if (gate) { gate = false; relAge = 0f; } }
    public void Reset() { active = false; gate = false; }

    public float Next(float sr, float pitchMul)
    {
        if (!active) return 0f;
        float dt = 1f / sr, env;
        if (gate)
        {
            age += dt;
            float a = attack > 0f ? Mathf.Min(1f, age / attack) : 1f;
            env = (sustain + (1f - sustain) * Mathf.Exp(-age / decayTau)) * a;
            envRel = env;
            if (sustain <= 0f && age > attack && env < 0.0004f) active = false;
        }
        else
        {
            relAge += dt;
            env = envRel * Mathf.Exp(-relAge / releaseTau);
            if (env < 0.0004f) active = false;
        }
        phase += freq * pitchMul * dt;
        phase -= System.Math.Floor(phase);
        return o.Osc(wave, phase) * env * amp;
    }
}

// Acorde sostenido
public class PadVoice
{
    readonly MusicSynth o;
    readonly int n;
    readonly float[] cur, tgt;
    readonly double[] p1, p2;
    readonly float sr, glideK;
    public WaveType wave = WaveType.Triangle;
    public float detune = 0.004f;
    bool init;

    public PadVoice(MusicSynth owner, int voices, float sampleRate, float glideSeconds)
    {
        o = owner; n = voices; sr = sampleRate;
        cur = new float[n]; tgt = new float[n]; p1 = new double[n]; p2 = new double[n];
        glideK = 1f - Mathf.Exp(-1f / (sr * Mathf.Max(0.01f, glideSeconds)));
    }

    public void Reset() { init = false; for (int i = 0; i < n; i++) { cur[i] = 0; } }

    public void SetTargets(params float[] f)
    {
        for (int i = 0; i < n && i < f.Length; i++) tgt[i] = f[i];
        if (!init) { for (int i = 0; i < n; i++) cur[i] = tgt[i]; init = true; }
    }

    public float Next(float pitchMul)
    {
        if (!init) return 0f;
        float sum = 0f;
        for (int k = 0; k < n; k++)
        {
            cur[k] += (tgt[k] - cur[k]) * glideK;
            p1[k] += cur[k] * (1f - detune) * pitchMul / sr; p1[k] -= System.Math.Floor(p1[k]);
            p2[k] += cur[k] * (1f + detune) * pitchMul / sr; p2[k] -= System.Math.Floor(p2[k]);
            sum += o.Osc(wave, p1[k]) + o.Osc(wave, p2[k]);
        }
        return sum / (2f * n);
    }
}

public class Reverb
{
    static readonly int[] CombBase = { 1116, 1188, 1277, 1356 };
    static readonly int[] ApBase = { 556, 441 };
    readonly float[][] comb, ap;
    readonly float[] filt;
    readonly int[] ci, ai;
    readonly float fb, damp;

    public Reverb(float sr, int offset, float feedback, float damping)
    {
        float s = sr / 44100f;
        fb = feedback; damp = damping;
        comb = new float[4][]; ci = new int[4]; filt = new float[4];
        for (int i = 0; i < 4; i++) comb[i] = new float[Mathf.Max(8, (int)(CombBase[i] * s) + offset)];
        ap = new float[2][]; ai = new int[2];
        for (int i = 0; i < 2; i++) ap[i] = new float[Mathf.Max(8, (int)(ApBase[i] * s))];
    }

    public float Process(float input)
    {
        float x = input * 0.25f, sum = 0f;
        for (int c = 0; c < 4; c++)
        {
            float[] b = comb[c]; int idx = ci[c];
            float y = b[idx];
            filt[c] = y * (1f - damp) + filt[c] * damp;
            b[idx] = x + filt[c] * fb;
            if (++idx >= b.Length) idx = 0;
            ci[c] = idx; sum += y;
        }
        sum *= 0.25f;
        for (int a = 0; a < 2; a++)
        {
            float[] b = ap[a]; int idx = ai[a];
            float bv = b[idx], o = -sum + bv;
            b[idx] = sum + bv * 0.5f;
            if (++idx >= b.Length) idx = 0;
            ai[a] = idx; sum = o;
        }
        return sum * 3f;
    }
}

public class Delay
{
    readonly float[] buf; int idx; float lp; readonly float fb, damp;
    public Delay(float sr, float seconds, float feedback, float damping)
    {
        buf = new float[Mathf.Max(8, (int)(sr * seconds))]; fb = feedback; damp = damping;
    }
    public float Process(float x)
    {
        float y = buf[idx];
        lp += (1f - damp) * (y - lp);
        buf[idx] = x + lp * fb;
        if (++idx >= buf.Length) idx = 0;
        return y;
    }
}

// Kick, snare y hi-hat sintetizados
public class Drums
{
    readonly float sr; float kAge = 9f, sAge = 9f, hAge = 9f, hatDecay = 60f, hatLp;
    double kPh, sPh; uint ns = 12345;
    public Drums(float sampleRate) { sr = sampleRate; }
    public void Kick() { kAge = 0f; }
    public void Snare() { sAge = 0f; }
    public void Hat(float decay) { hAge = 0f; hatDecay = decay; }
    float Noise() { ns ^= ns << 13; ns ^= ns >> 17; ns ^= ns << 5; return (ns / (float)uint.MaxValue) * 2f - 1f; }

    public float Next(float kv, float sv, float hv)
    {
        float dt = 1f / sr, o = 0f;
        if (kAge < 0.5f)
        {
            float f = 45f + 90f * Mathf.Exp(-kAge * 35f);
            kPh += 6.2831853 * f * dt; if (kPh > 6.2831853) kPh -= 6.2831853;
            o += (float)System.Math.Sin(kPh) * Mathf.Exp(-kAge * 9f) * kv; kAge += dt;
        }
        if (sAge < 0.4f)
        {
            sPh += 6.2831853 * 190.0 * dt; if (sPh > 6.2831853) sPh -= 6.2831853;
            o += (Noise() * 0.7f + (float)System.Math.Sin(sPh) * 0.5f) * Mathf.Exp(-sAge * 22f) * sv; sAge += dt;
        }
        if (hAge < 0.3f)
        {
            float n = Noise(); hatLp += 0.3f * (n - hatLp);
            o += (n - hatLp) * Mathf.Exp(-hAge * hatDecay) * hv; hAge += dt;
        }
        return o;
    }
}
