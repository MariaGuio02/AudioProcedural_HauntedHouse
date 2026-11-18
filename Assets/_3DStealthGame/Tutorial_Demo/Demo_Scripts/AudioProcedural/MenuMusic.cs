using UnityEngine;

public class MenuMusic : MusicSynth
{
    [Header("Tempo")]
    [Range(80f, 140f)] public float tempo = 112f;

    [Header("Formas de onda")]
    public WaveType padWave = WaveType.Triangle;
    public WaveType bassWave = WaveType.Saw;
    public WaveType arpWave = WaveType.Triangle;
    public WaveType leadWave = WaveType.Square;
    public WaveType bellWave = WaveType.Sine;

    [Header("Mezcla de capas")]
    [Range(0f, 1f)] public float padVol = 0.20f;
    [Range(0f, 1f)] public float bassVol = 0.22f;
    [Range(0f, 1f)] public float arpVol = 0.12f;
    [Range(0f, 1f)] public float leadVol = 0.13f;
    [Range(0f, 1f)] public float bellVol = 0.07f;
    [Range(0f, 1f)] public float kickVol = 0.30f;
    [Range(0f, 1f)] public float snareVol = 0.10f;
    [Range(0f, 1f)] public float hatVol = 0.04f;

    [Header("LFO / Efectos")]
    [Range(0f, 10f)] public float vibratoRate = 5.5f;
    [Range(0f, 50f)] public float vibratoCents = 14f;
    [Range(0f, 2f)] public float padFilterRate = 0.18f;
    [Range(200f, 6000f)] public float padCutoff = 1400f;
    [Range(0f, 3000f)] public float padCutoffSweep = 900f;
    [Range(0f, 1f)] public float reverbMix = 0.28f;
    [Range(0f, 1f)] public float delayMix = 0.22f;
    [Range(0f, 0.9f)] public float delayFeedback = 0.40f;

    static readonly int[][] Chords = { new[] { 58, 61, 65 }, new[] { 54, 58, 61 }, new[] { 61, 65, 68 }, new[] { 56, 60, 63 } };
    static readonly int[] Roots = { 34, 30, 37, 32 };
    static readonly int[] SectionPlan = { 2, 3, 1, 3 };
    static readonly int[] ArpPat = { 0, 1, 2, 1 };
    static readonly int[] Melody =
    {
        70,-1,0,0, 73,-1,75,0, 77,-1,-1,0, 75,0,73,0,
        73,-1,0,0, 75,-1,73,0, 70,-1,-1,-1, 0,0,0,0,
        77,-1,0,77, 80,-1,78,0, 77,-1,-1,0, 75,0,73,0,
        75,-1,0,0, 72,-1,75,0, 72,-1,-1,-1, 0,0,70,72
    };

    Voice bassV, arpV, leadV; Voice[] bellV; int bellIdx;
    PadVoice pad; OnePole padLP, bassLP;
    Reverb revL, revR; Delay dly; Drums drums;
    double stepPos; int lastStep;
    float[] g = new float[3];
    float gainK, bassK;

    protected override void InitSynth()
    {
        bassV = new Voice(this); arpV = new Voice(this); leadV = new Voice(this);
        bellV = new[] { new Voice(this), new Voice(this), new Voice(this) };
        pad = new PadVoice(this, 3, sr, 0.35f);
        padLP = new OnePole(); bassLP = new OnePole();
        revL = new Reverb(sr, 0, 0.86f, 0.3f); revR = new Reverb(sr, 23, 0.86f, 0.3f);
        dly = new Delay(sr, 60f / 112f * 0.75f, delayFeedback, 0.35f);   // corchea con puntillo
        drums = new Drums(sr);
        gainK = 1f - Mathf.Exp(-1f / (sr * 0.25f));
        bassK = OnePole.Coef(500f, sr);
    }

    protected override void ResetState()
    {
        stepPos = 0; lastStep = -1;
        for (int i = 0; i < g.Length; i++) g[i] = 1f; 
        pad.Reset(); bassV.Reset(); arpV.Reset(); leadV.Reset();
        foreach (var b in bellV) b.Reset();
    }

    void OnStep(int step)
    {
        int bar = step >> 4, s = step & 15, ci = bar & 3, section = SectionPlan[(bar >> 2) & 3];
        int[] c = Chords[ci];

        if (s == 0) pad.SetTargets(Mtof(c[0]), Mtof(c[1]), Mtof(c[2]));

        if (s == 0 || s == 3 || s == 6 || s == 8 || s == 11 || s == 14)
            bassV.Trigger(Mtof(Roots[ci] + (s == 6 || s == 14 ? 12 : 0)), bassVol, bassWave, 0.004f, 0.13f, 0f, 0.05f);

        // Arpegio (corcheas; semicorcheas en la ultima seccion)
        int every = section == 3 ? 1 : 2;
        if (s % every == 0)
            arpV.Trigger(Mtof(c[ArpPat[(step / every) & 3]] + 12), arpVol, arpWave, 0.003f, 0.09f, 0f, 0.05f);

        // Melodia
        int m = Melody[(bar & 3) * 16 + s];
        if (m > 0) { leadV.NoteOff(); leadV.Trigger(Mtof(m), leadVol, leadWave, 0.01f, 0.6f, 0.6f, 0.08f); }
        else if (m == 0) leadV.NoteOff();

        // Campanitas
        if (s % 4 == 2 && Noise() > 0f)
        {
            bellV[bellIdx].Trigger(Mtof(c[(s / 4 + bar) % 3] + 24), bellVol, bellWave, 0.002f, 0.35f, 0f, 0.2f);
            bellIdx = (bellIdx + 1) % bellV.Length;
        }

        // Percusion
        if (section >= 1)
        {
            if (s == 0 || s == 8 || (section == 3 && s == 10)) drums.Kick();
            if (s == 4 || s == 12) drums.Snare();
            if (section == 3 ? true : s % 2 == 0) drums.Hat(s == 14 ? 18f : 60f);
        }
    }

    protected override void Render(out float L, out float R)
    {
        stepPos += tempo / 60.0 * 4.0 / sr;
        int step = (int)stepPos;
        if (step != lastStep) { lastStep = step; OnStep(step); }
        int section = SectionPlan[((step >> 4) >> 2) & 3];

        g[0] += ((section >= 1 ? 1f : 0f) - g[0]) * gainK;
        g[1] += ((section >= 2 ? 1f : 0f) - g[1]) * gainK;

        float pm = 1f + Lfo(vibratoRate) * vibratoCents * 0.000578f;

        float cut = Mathf.Max(80f, padCutoff + padCutoffSweep * Lfo(padFilterRate));
        float padOut = padLP.Process(pad.Next(1f), OnePole.Coef(cut, sr)) * padVol * (0.85f + 0.15f * Lfo(0.3f));
        float bass = bassLP.Process(bassV.Next(sr, 1f), bassK);
        float arp = arpV.Next(sr, pm);
        float lead = leadV.Next(sr, pm) * g[1];
        float bell = 0f;
        for (int i = 0; i < bellV.Length; i++) bell += bellV[i].Next(sr, 1f);
        float perc = drums.Next(kickVol, snareVol, hatVol) * g[0];

        float dry = padOut + bass + perc;
        float send = arp + lead + bell + padOut * 0.4f;
        float d = dly.Process(send) * delayMix;

        L = dry + send + d + revL.Process(send + d) * reverbMix;
        R = dry + send + d + revR.Process(send + d) * reverbMix;
    }
}
