using UnityEngine;

public class GameMusic : MusicSynth
{
    [Header("Estructura")]
    [Range(1, 8)] public int sectionBars = 4;         // compases por seccion (4 compases ~ 8.5 s)

    [Header("Tempo")]
    [Range(70f, 140f)] public float baseTempo = 112f; 

    [Header("Formas de onda")]
    public WaveType padWave = WaveType.Triangle;
    public WaveType droneWave = WaveType.Saw;
    public WaveType tensionWave = WaveType.Saw;
    public WaveType bellWave = WaveType.Sine;
    public WaveType bassWave = WaveType.Square;
    public WaveType arpWave = WaveType.Triangle;
    public WaveType leadWave = WaveType.Saw;

    [Header("Mezcla de capas")]
    [Range(0f, 1f)] public float padVol = 0.22f;
    [Range(0f, 1f)] public float droneVol = 0.20f;
    [Range(0f, 1f)] public float windVol = 0.05f;
    [Range(0f, 1f)] public float bellVol = 0.12f;
    [Range(0f, 1f)] public float bassVol = 0.22f;
    [Range(0f, 1f)] public float arpVol = 0.11f;
    [Range(0f, 1f)] public float leadVol = 0.13f;
    [Range(0f, 1f)] public float tensionPadVol = 0.07f;
    [Range(0f, 1f)] public float creakVol = 0.05f;
    [Range(0f, 1f)] public float riserVol = 0.12f;
    [Range(0f, 1f)] public float kickVol = 0.28f;
    [Range(0f, 1f)] public float snareVol = 0.09f;
    [Range(0f, 1f)] public float hatVol = 0.035f;

    [Header("Vibrato / Tremolo / FM")]
    [Range(0f, 10f)] public float vibratoRate = 4.5f;
    [Range(0f, 50f)] public float vibratoCents = 18f;
    [Range(0f, 10f)] public float tremoloRate = 2.5f;
    [Range(0f, 1f)] public float tremoloDepth = 0.15f;
    [Range(0f, 10f)] public float fmRate = 3f;
    [Range(0f, 60f)] public float fmCents = 10f;

    [Header("Filtro / Efectos")]
    [Range(0f, 1f)] public float padFilterRate = 0.1f;
    [Range(300f, 4000f)] public float padCutoff = 1900f;
    [Range(0f, 1f)] public float windLfoRate = 0.13f;
    [Range(0f, 1f)] public float reverbMix = 0.36f;
    [Range(0f, 1f)] public float delayMix = 0.25f;
    [Range(0f, 0.9f)] public float delayFeedback = 0.45f;

    // ---------- Patrones ----------
    static int M(string s) { int m = 0; for (int i = 0; i < s.Length && i < 16; i++) if (s[i] == 'x') m |= 1 << i; return m; }
    static bool Hit(int mask, int s) { return ((mask >> s) & 1) != 0; }

    // bateria: { bombo, caja, hat }
    const int DR_FOUR = 0, DR_HALF = 1, DR_SYNC = 2, DR_BREAK = 3, DR_BUILD = 4;
    static readonly int[][] Drum =
    {
        new[] { M("x...x...x...x..."), M("....x.......x..."), M("xxxxxxxxxxxxxxxx") },
        new[] { M("x.........x....."), M("........x......."), M("x.x.x.x.x.x.x.x.") },
        new[] { M("x..x..x...x.x..."), M("....x.......x..x"), M("x.x.xxx.x.x.xxx.") },
        new[] { M("................"), M("................"), M("..x...x...x...x.") },
        new[] { M("x...x...x...x..."), M("....x.......x..."), M("xxxxxxxxxxxxxxxx") }
    };
    static readonly int[] BassMask = { M("x.x.x.x.x.x.x.x."), M("x..x..x.x..x..x."), M("x.......x......."), M("x.xx..x.x.xx..x.") };
    static readonly int[][] ArpPats = { new[] { 0, 1, 2, 1 }, new[] { 2, 1, 0, 1 }, new[] { 0, 2, 1, 3 }, new[] { 0, 1, 2, 3, 2, 1, 4, 2 } };

    class Plan
    {
        public float pad = 1, bass = 1, arp = 1, bell = 1, lead = 1, tpad = 0.6f, kick = 1, snare = 1, hat = 1;
        public float cutoff = 1, reverb = 1, fx = 1, trem = 1, delay = 1, pump = 0, gate = 0, sparkle = 0, creak = 0.7f;
        public int prog, bassMode, drums, leadMode, arpPat, arpEvery = 1, arpOct = 12, transpose;
        public bool bellOct, variation, leadOct, snareRoll, riser, hit;
    }

    static readonly Plan[] Plans =
    {
        new Plan { bellOct = true, pump = 0.3f, hit = true },                                                       // 0 Pleno
        new Plan { bass = 0.7f, arp = 0.8f, bell = 1.3f, tpad = 1f, kick = 0, snare = 0, hat = 0.35f, cutoff = 0.5f,
                   reverb = 1.6f, fx = 0.5f, trem = 2f, delay = 1.6f, arpEvery = 2, arpPat = 2, bassMode = 2,
                   drums = DR_BREAK, sparkle = 1f, creak = 0.4f },                                                  // 1 Susurro
        new Plan { prog = 1, variation = true, bellOct = true, cutoff = 1.2f, fx = 2f, trem = 0.5f, delay = 1.2f,
                   arpPat = 1, arpOct = 24, bassMode = 1, drums = DR_SYNC, leadMode = 1 },                          // 2 Variacion
        new Plan { prog = 2, tpad = 1.3f, bell = 1.1f, cutoff = 0.7f, reverb = 1.4f, fx = 1.2f, gate = 0.5f,
                   arpEvery = 2, arpPat = 3, bassMode = 2, drums = DR_HALF, leadMode = 1, sparkle = 1f, creak = 0.2f }, // 3 Misterio
        new Plan { bellOct = true, bassMode = 3, drums = DR_FOUR, gate = 0.6f, pump = 0.5f, hat = 1.2f, arpPat = 3,
                   leadMode = 2, fx = 1.5f, trem = 0.5f, cutoff = 1.3f },                                           // 4 Pulso
        new Plan { prog = 1, tpad = 1f, bellOct = true, hat = 1.3f, cutoff = 1.6f, fx = 1.5f, trem = 1.5f,
                   drums = DR_BUILD, snareRoll = true, riser = true, arpPat = 2, pump = 0.4f },                     // 5 Subida
        new Plan { pad = 1.1f, bass = 1.15f, arp = 1.2f, bell = 1.2f, lead = 1.2f, tpad = 1.4f, kick = 1.1f,
                   snare = 1.1f, hat = 1.2f, cutoff = 1.8f, reverb = 0.8f, fx = 2.5f, delay = 1.4f, bellOct = true,
                   leadOct = true, arpOct = 24, transpose = 2, leadMode = 1, sparkle = 1f, pump = 0.3f, hit = true }, // 6 Climax
        new Plan { prog = 2, variation = true, arp = 0.8f, arpEvery = 2, arpPat = 1, cutoff = 0.9f, reverb = 1.3f,
                   fx = 1.5f, trem = 1.5f, delay = 2.5f, drums = DR_HALF, sparkle = 1f, creak = 0.5f }              // 7 Eco
    };
    static readonly int[][][] Chords =
    {
        new[] { new[] { 55, 58, 62 }, new[] { 55, 58, 63 }, new[] { 55, 60, 63 }, new[] { 57, 62, 66 } },
        new[] { new[] { 55, 58, 62 }, new[] { 58, 62, 65 }, new[] { 55, 60, 63 }, new[] { 57, 62, 66 } },
        new[] { new[] { 55, 58, 62 }, new[] { 56, 60, 63 }, new[] { 55, 58, 63 }, new[] { 57, 62, 66 } }
    };
    static readonly int[][] Roots = { new[] { 31, 39, 36, 38 }, new[] { 31, 34, 36, 38 }, new[] { 31, 32, 39, 38 } };

    static readonly int[][] BellMel =
    {
        new[] { 79,0,82,0,86,0,82,0, 81,0,79,0,0,0,74,0 },
        new[] { 87,0,82,0,79,0,82,0, 87,0,86,0,82,0,0,0 },
        new[] { 84,0,79,0,75,0,79,0, 84,0,87,0,86,0,0,0 },
        new[] { 86,0,81,0,78,0,81,0, 78,0,81,0,86,0,84,0 }
    };

    static readonly int[] GenA = { 2,-1,1,-1,0,-1,1,-1, 2,-1,3,-1,2,-1,-1,-1 };
    static readonly int[] GenB = { 0,-1,2,-1,1,-1,2,-1, 3,-1,2,-1,1,-1,-1,-1 };
    static readonly int[] LeadMel  = { 74, 70, 75, 79, 72, 75, 78, 74 };
    static readonly int[] LeadMelB = { 79, 82, 79, 75, 84, 79, 81, 78 };
    static readonly int[] LeadGen  = { 2, 1, 3, 2, 4, 3, 2, 1 };
    static readonly int[] LeadRhy  = { 2, 3, 2, 1 };

    static int GenNote(int[] c, int k, int oct) { return c[k % 3] + 12 * (oct + k / 3); }

    const int PAD = 0, DRONE = 1, WIND = 2, BELL = 3, BASS = 4, HAT = 5, ARP = 6, KICK = 7, LEAD = 8, SNARE = 9, TPAD = 10,
              REV = 11, CUT = 12, FX = 13, TREM = 14, DLY = 15, PUMP = 16, GATE = 17, NG = 18;

    PadVoice pad, drone, tpad;
    Voice bassV, arpV, leadV, sparkleV; Voice[] bellV; int bellIdx;
    OnePole padLP, droneLP, tpadLP, windLP, creakLP, riseLP;
    Reverb revL, revR; Delay dly; Drums drums;
    double stepPos, creakPh; int lastStep, lastPlan;
    float creakAge = 9f, riseAge = 9f;
    float[] g = new float[NG], tg = new float[NG];
    float gainK, droneK, tpadK;

    protected override void InitSynth()
    {
        pad = new PadVoice(this, 3, sr, 0.6f); pad.wave = padWave;
        drone = new PadVoice(this, 1, sr, 0.8f); drone.wave = droneWave;
        tpad = new PadVoice(this, 2, sr, 0.8f); tpad.wave = tensionWave;
        bassV = new Voice(this); arpV = new Voice(this); leadV = new Voice(this); sparkleV = new Voice(this);
        bellV = new[] { new Voice(this), new Voice(this), new Voice(this), new Voice(this), new Voice(this), new Voice(this) };
        padLP = new OnePole(); droneLP = new OnePole(); tpadLP = new OnePole(); windLP = new OnePole(); creakLP = new OnePole(); riseLP = new OnePole();
        revL = new Reverb(sr, 0, 0.90f, 0.35f); revR = new Reverb(sr, 29, 0.90f, 0.35f);
        dly = new Delay(sr, 60f / baseTempo * 0.75f, delayFeedback, 0.4f);
        drums = new Drums(sr);
        gainK = 1f - Mathf.Exp(-1f / (sr * 0.6f));
        droneK = OnePole.Coef(170f, sr); tpadK = OnePole.Coef(1800f, sr);
    }

    void FillTargets(Plan p)
    {
        tg[PAD] = p.pad; tg[DRONE] = 1f; tg[WIND] = 1f; tg[BELL] = p.bell; tg[BASS] = p.bass;
        tg[HAT] = p.hat; tg[ARP] = p.arp; tg[KICK] = p.kick; tg[LEAD] = p.lead; tg[SNARE] = p.snare;
        tg[TPAD] = p.tpad; tg[REV] = p.reverb; tg[CUT] = p.cutoff; tg[FX] = p.fx; tg[TREM] = p.trem;
        tg[DLY] = p.delay; tg[PUMP] = p.pump; tg[GATE] = p.gate;
    }

    protected override void ResetState()
    {
        stepPos = 0; lastStep = -1; lastPlan = -1; creakAge = 9f; riseAge = 9f;
        FillTargets(Plans[0]);
        for (int i = 0; i < NG; i++) g[i] = tg[i];
        pad.Reset(); drone.Reset(); tpad.Reset(); bassV.Reset(); arpV.Reset(); leadV.Reset(); sparkleV.Reset();
        foreach (var b in bellV) b.Reset();
    }

    void BellOn(float midi, float vol)
    {
        bellV[bellIdx].Trigger(Mtof(midi), vol, bellWave, 0.002f, 0.45f, 0f, 0.3f);
        bellIdx = (bellIdx + 1) % bellV.Length;
    }

    void LeadOn(float midi, float atk)
    {
        leadV.NoteOff();
        leadV.Trigger(Mtof(midi), leadVol, leadWave, atk, 0.5f, 0.7f, 0.2f);
    }

    void OnStep(int step)
    {
        int bar = step >> 4, s = step & 15, ci = (bar >> 1) & 3;
        int sb = Mathf.Max(1, sectionBars);
        int planIdx = (bar / sb) % Plans.Length;
        Plan plan = Plans[planIdx];
        int barIn = bar % sb;
        int[] c = Chords[plan.prog][ci];
        int root = Roots[plan.prog][ci];
        int T = plan.transpose;

        // Eventos de seccion
        if (s == 0 && barIn == 0 && planIdx != lastPlan)
        {
            lastPlan = planIdx;
            if (plan.hit) drums.Kick();
        }
        if (plan.riser && s == 0 && barIn == sb - 1) riseAge = 0f;
        if (plan.snareRoll && barIn == sb - 1 && (s == 8 || s == 10 || s >= 12)) drums.Snare();

        // Armonia
        if (s == 0 && (bar & 1) == 0)
        {
            pad.SetTargets(Mtof(c[0] + T), Mtof(c[1] + T), Mtof(c[2] + T));
            drone.SetTargets(Mtof(root + T));
            tpad.SetTargets(Mtof(root + 36 + T), Mtof(root + 42 + T));     // tritono
            if (Noise() > plan.creak) creakAge = 0f;
        }

        // Caja de musica
        if ((s & 1) == 0)
        {
            int idx = (((bar & 1) << 4) + s) >> 1;
            if (plan.variation) idx = 15 - idx;
            int note;
            if (plan.prog == 0) note = BellMel[ci][idx];
            else { int k = ((ci & 1) == 0 ? GenA : GenB)[idx]; note = k < 0 ? 0 : GenNote(c, k, 2); }
            if (note > 0)
            {
                BellOn(note + T, bellVol);
                if (plan.bellOct) BellOn(note + 12 + T, bellVol * 0.5f);
            }
        }

        // Destello largo
        if (plan.sparkle > 0f && s == 8 && (bar & 1) == 0)
            sparkleV.Trigger(Mtof(c[(bar >> 1) % 3] + 36 + T), bellVol * plan.sparkle * 0.9f, bellWave, 0.01f, 0.9f, 0f, 0.6f);

        // Bajo
        if (Hit(BassMask[plan.bassMode], s))
        {
            int oct = ((s & 2) != 0 && plan.bassMode != 2) ? 12 : 0;
            bassV.Trigger(Mtof(root + 12 + oct + T), bassVol, bassWave, 0.004f, plan.bassMode == 2 ? 0.5f : 0.16f, 0f, 0.05f);
        }

        // Arpegio
        if (s % plan.arpEvery == 0)
        {
            int[] pat = ArpPats[plan.arpPat];
            int k = pat[(step / plan.arpEvery) % pat.Length];
            arpV.Trigger(Mtof(c[k % 3] + plan.arpOct + 12 * (k / 3) + T), arpVol, arpWave, 0.003f, 0.10f, 0f, 0.05f);
        }

        // Melodia
        int lo = (plan.leadOct ? 12 : 0) + T;
        if (plan.leadMode == 0)
        {
            if (s == 0)
            {
                int n = plan.prog == 0 ? (plan.variation ? LeadMelB : LeadMel)[bar & 7] : GenNote(c, LeadGen[bar & 7], 1);
                LeadOn(n + lo, 0.03f);
            }
            if (s == 12) leadV.NoteOff();
        }
        else if (plan.leadMode == 1)
        {
            if (s == 0) LeadOn(GenNote(c, LeadGen[bar & 7], 1) + lo, 0.03f);
            if (s == 8) LeadOn(GenNote(c, LeadGen[(bar + 3) & 7], 1) + lo, 0.03f);
            if (s == 6 || s == 14) leadV.NoteOff();
        }
        else
        {
            int slot = s == 0 ? 0 : s == 3 ? 1 : s == 6 ? 2 : s == 10 ? 3 : -1;
            if (slot >= 0) LeadOn(GenNote(c, LeadRhy[slot] + ((bar >> 1) & 1), 1) + lo, 0.01f);
            if (s == 2 || s == 5 || s == 8 || s == 12) leadV.NoteOff();
        }

        // Percusion
        int[] dm = Drum[plan.drums];
        if (Hit(dm[2], s)) drums.Hat(s % 4 == 2 ? 30f : 60f);
        if (Hit(dm[0], s)) drums.Kick();
        if (Hit(dm[1], s)) drums.Snare();
    }

    protected override void Render(out float L, out float R)
    {
        int bar0 = (int)(stepPos / 16.0);
        Plan plan = Plans[(bar0 / Mathf.Max(1, sectionBars)) % Plans.Length];
        FillTargets(plan);
        for (int i = 0; i < NG; i++) g[i] += (tg[i] - g[i]) * gainK;

        stepPos += baseTempo / 60.0 * 4.0 / sr;
        int step = (int)stepPos;
        if (step != lastStep) { lastStep = step; OnStep(step); }

        float vib = Lfo(vibratoRate) * vibratoCents * g[FX] * 0.000578f;
        float fm = Lfo(fmRate) * fmCents * g[FX] * 0.000578f;
        float pm = 1f + vib;
        float pmLead = 1f + vib + fm;

        float beatPhase = (float)((stepPos / 4.0) % 1.0);
        float pump = 1f - g[PUMP] * Mathf.Pow(1f - beatPhase, 3f);
        float ph8 = (float)((stepPos / 2.0) % 1.0);
        float gate = 1f - g[GATE] * 0.5f * (1f - Mathf.Cos(6.2831853f * ph8));

        float cut = Mathf.Max(80f, padCutoff * g[CUT] * (1f + 0.25f * Lfo(padFilterRate)));
        float trem = 1f - Mathf.Min(0.9f, tremoloDepth * g[TREM]) * (0.5f + 0.5f * Lfo(tremoloRate));
        float padOut = padLP.Process(pad.Next(1f), OnePole.Coef(cut, sr)) * padVol * g[PAD] * trem * gate * pump;
        float droneOut = droneLP.Process(drone.Next(1f), droneK) * droneVol * g[DRONE] * pump;

        float ttrem = 0.5f + 0.5f * Lfo(5f);
        float tpadOut = tpadLP.Process(tpad.Next(pm), tpadK) * ttrem * tensionPadVol * g[TPAD] * gate * pump;

        float wc = 250f + 500f * (0.5f + 0.5f * Lfo(windLfoRate));
        float wind = windLP.Process(Noise(), OnePole.Coef(wc, sr)) * (0.5f + 0.5f * Lfo(windLfoRate * 0.7f, 1f)) * windVol;

        float creak = 0f;
        if (creakAge < 1.1f)
        {
            float u = creakAge / 1.1f;
            float f = 240f + 120f * Mathf.Sin(3.14159f * u) + 20f * Mathf.Sin(6.2831853f * 8f * creakAge);
            creakPh += f / sr; creakPh -= System.Math.Floor(creakPh);
            float env = Mathf.Sin(3.14159f * u); env *= env;
            creak = creakLP.Process(Osc(WaveType.Saw, creakPh), OnePole.Coef(1100f, sr)) * env * creakVol;
            creakAge += 1f / sr;
        }

        float riser = 0f;
        if (riseAge < 2.2f)
        {
            float u = riseAge / 2.2f;
            float env = u < 0.85f ? u / 0.85f : (1f - u) / 0.15f;
            riser = riseLP.Process(Noise(), OnePole.Coef(400f + 6000f * u * u, sr)) * env * riserVol;
            riseAge += 1f / sr;
        }

        float bass = bassV.Next(sr, 1f) * g[BASS];
        float arp = arpV.Next(sr, pmLead) * g[ARP];
        float lead = leadV.Next(sr, pmLead) * g[LEAD];
        float bell = 0f;
        for (int i = 0; i < bellV.Length; i++) bell += bellV[i].Next(sr, 1f);
        bell = bell * g[BELL] + sparkleV.Next(sr, 1f);
        float perc = drums.Next(kickVol * g[KICK], snareVol * g[SNARE], hatVol * g[HAT]);

        float dry = padOut + droneOut + wind + bass + perc + riser;
        float send = bell + arp + lead + tpadOut + creak + padOut * 0.3f;
        float d = dly.Process(send) * delayMix * g[DLY];
        float rv = reverbMix * g[REV];

        L = dry + send + d + revL.Process(send + d) * rv;
        R = dry + send + d + revR.Process(send + d) * rv;
    }
}
