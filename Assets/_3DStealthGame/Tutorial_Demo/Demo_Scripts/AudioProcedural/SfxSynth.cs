using UnityEngine;

public static class SfxSynth
{
    const int SR = 44100;
    static System.Random rng = new System.Random(1234);

    static float Mtof(float m) { return 440f * Mathf.Pow(2f, (m - 69f) / 12f); }
    static float Rand() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

    // ---------- Bloques de sintesis ----------

    static readonly float[] BellP = { 1f, 2.0f, 3.0f, 4.02f };
    static readonly float[] BellA = { 1f, 0.42f, 0.16f, 0.05f };
    static readonly float[] BellD = { 1f, 0.50f, 0.28f, 0.15f };

    static void Bell(float[] b, float t0, float f, float amp, float decay)
    {
        int s = (int)(t0 * SR);
        int len = (int)(decay * 5f * SR);
        for (int i = 0; i < len && s + i < b.Length; i++)
        {
            double t = i / (double)SR;
            float atk = Mathf.Min(1f, (float)t / 0.004f);
            float v = 0f;
            for (int p = 0; p < 4; p++)
                v += BellA[p] * Mathf.Exp(-(float)t / (decay * BellD[p])) *
                     (float)System.Math.Sin(6.283185307 * f * BellP[p] * t);
            b[s + i] += v * atk * amp;
        }
    }

    static void Click(float[] b, float t0, float amp)
    {
        int s = (int)(t0 * SR), len = (int)(0.015f * SR);
        float lp = 0f;
        for (int i = 0; i < len && s + i < b.Length; i++)
        {
            float n = Rand();
            lp += 0.24f * (n - lp);
            b[s + i] += lp * Mathf.Exp(-i / (0.004f * SR)) * amp;
        }
    }

    static void Thump(float[] b, float t0, float f0, float f1, float amp, float tau)
    {
        int s = (int)(t0 * SR), len = (int)(tau * 6f * SR);
        double ph = 0;
        for (int i = 0; i < len && s + i < b.Length; i++)
        {
            float t = i / (float)SR;
            float f = f1 + (f0 - f1) * Mathf.Exp(-t * 26f);
            ph += 6.283185307 * f / SR;
            b[s + i] += (float)System.Math.Sin(ph) * Mathf.Exp(-t / tau) * amp;
        }
    }

    static void Drone(float[] b, float t0, float f, float amp, float atk, float tau,
                      float lp0, float lp1, float lpTau, float glideSemis, float glideTau,
                      float tremRate, float tremDepth)
    {
        int s = (int)(t0 * SR);
        int len = Mathf.Min(b.Length - s, (int)(tau * 6f * SR));
        double ph = 0; float z = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            float fr = f * Mathf.Pow(2f, -glideSemis * (1f - Mathf.Exp(-t / glideTau)) / 12f);
            ph += fr / SR; ph -= System.Math.Floor(ph);
            float saw = (float)(2.0 * ph - 1.0);
            float tri = 1f - 4f * Mathf.Abs((float)ph - 0.5f);
            float raw = saw * 0.6f + tri * 0.4f;
            float cut = lp1 + (lp0 - lp1) * Mathf.Exp(-t / lpTau);
            z += (1f - Mathf.Exp(-6.2831853f * cut / SR)) * (raw - z);
            float env = Mathf.Exp(-t / tau) * Mathf.Min(1f, t / atk);
            float trem = 1f - tremDepth * (0.5f + 0.5f * Mathf.Sin(6.2831853f * tremRate * t));
            b[s + i] += z * env * trem * amp;
        }
    }

    // Retumbar de ruido filtrado en medios-graves
    static void Rumble(float[] b, float t0, float amp, float tau, float cutoff)
    {
        int s = (int)(t0 * SR);
        int len = Mathf.Min(b.Length - s, (int)(tau * 5f * SR));
        float z = 0f, a = 1f - Mathf.Exp(-6.2831853f * cutoff / SR);
        for (int i = 0; i < len; i++)
        {
            z += a * (Rand() - z);
            float t = i / (float)SR;
            b[s + i] += z * Mathf.Exp(-t / tau) * Mathf.Min(1f, t / 0.03f) * amp;
        }
    }

    // Reverb + normalizado
    static AudioClip ToClip(string name, float[] dry, float send, float wet, float peak)
    {
        int n = dry.Length;
        Reverb rl = new Reverb(SR, 0, 0.88f, 0.3f), rr = new Reverb(SR, 29, 0.88f, 0.3f);
        float[] data = new float[n * 2];
        float max = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float w = dry[i] * send;
            float l = dry[i] + rl.Process(w) * wet;
            float r = dry[i] + rr.Process(w) * wet;
            data[2 * i] = l; data[2 * i + 1] = r;
            max = Mathf.Max(max, Mathf.Max(Mathf.Abs(l), Mathf.Abs(r)));
        }
        float k = peak / max;
        int fade = (int)(0.05f * SR);
        for (int i = 0; i < n; i++)
        {
            float g = k;
            if (i > n - fade) g *= (n - i) / (float)fade;
            data[2 * i] *= g; data[2 * i + 1] *= g;
        }
        AudioClip clip = AudioClip.Create(name, n, 2, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Efectos

    // Boton jugar
    public static AudioClip StartButton()
    {
        float[] b = new float[(int)(1.8f * SR)];
        Thump(b, 0f, 150f, 68f, 0.50f, 0.14f);
        Rumble(b, 0f, 0.25f, 0.28f, 220f);
        Click(b, 0f, 0.30f);

        // Ascendente en octavas 3 y 4 (audible y con cuerpo)
        Drone(b, 0.02f, Mtof(53), 0.38f, 0.02f, 0.32f, 1600f, 480f, 0.28f, 0f, 1f, 0f, 0f); // Fa3
        Drone(b, 0.16f, Mtof(60), 0.40f, 0.02f, 0.32f, 1800f, 520f, 0.28f, 0f, 1f, 0f, 0f); // Do4

        // Remate final del acorde
        Thump(b, 0.32f, 135f, 65f, 0.45f, 0.20f);
        Drone(b, 0.32f, Mtof(53), 0.38f, 0.02f, 0.80f, 1900f, 450f, 0.65f, 0f, 1f, 0f, 0f); // Fa3
        Drone(b, 0.32f, Mtof(65), 0.36f, 0.02f, 0.80f, 2100f, 500f, 0.65f, 0f, 1f, 0f, 0f); // Fa4
        Drone(b, 0.32f, Mtof(69), 0.28f, 0.02f, 0.80f, 2100f, 500f, 0.65f, 0f, 1f, 0f, 0f); // La4
        Bell(b, 0.32f, Mtof(65), 0.26f, 0.45f);

        return ToClip("sfx_start", b, 0.45f, 0.40f, 0.85f);
    }

    // Boton salir
    public static AudioClip ExitButton()
    {
        float[] b = new float[(int)(0.9f * SR)];
        Thump(b, 0f, 140f, 65f, 0.55f, 0.10f);
        Click(b, 0f, 0.40f);
        Rumble(b, 0f, 0.25f, 0.15f, 200f);

        // Dos notas descendentes en registro medio-grave (octavas 3 y 4)
        Bell(b, 0.01f, Mtof(62), 0.42f, 0.14f);  // Re4
        Drone(b, 0.01f, Mtof(50), 0.38f, 0.01f, 0.18f, 950f, 300f, 0.15f, 0f, 1f, 0f, 0f); // Re3

        Thump(b, 0.14f, 115f, 58f, 0.45f, 0.14f);
        Bell(b, 0.14f, Mtof(57), 0.42f, 0.22f);  // La3
        Drone(b, 0.14f, Mtof(45), 0.42f, 0.01f, 0.28f, 850f, 260f, 0.24f, 0.4f, 0.25f, 0f, 0f); // La2

        return ToClip("sfx_exit", b, 0.30f, 0.25f, 0.80f);
    }

    // Ganar
    public static AudioClip Win()
    {
        float[] b = new float[(int)(2.6f * SR)];

        // Golpe inicial
        Thump(b, 0f, 165f, 75f, 0.45f, 0.14f);
        Click(b, 0f, 0.25f);

        // Ascendente en Do Mayor (octavas 4 y 5: Do4 -> Mi4 -> Sol4 -> Do5)
        float[] notes = { 60, 64, 67, 72 };
        float[] times = { 0.00f, 0.07f, 0.14f, 0.23f };
        float[] durs = { 0.22f, 0.22f, 0.24f, 0.30f };

        for (int i = 0; i < notes.Length; i++)
        {
            float f = Mtof(notes[i]);
            Drone(b, times[i], f, 0.36f, 0.015f, durs[i], 2800f, 750f, 0.25f, 0f, 1f, 0f, 0f);
            // Brillo agudo una octava arriba
            Bell(b, times[i], f * 2f, 0.26f, 0.28f);
        }

        // Remate final
        float tEnd = 0.34f;
        Thump(b, tEnd, 155f, 70f, 0.50f, 0.22f);
        Rumble(b, tEnd, 0.22f, 0.35f, 260f);

        float[] chord = { 50, 62, 66, 70, 74, 78 }; // Do3, Do4, Mi4, Sol4, Do5, Mi5
        for (int i = 0; i < chord.Length; i++)
        {
            float amp = (i == 0) ? 0.34f : 0.28f;
            Drone(b, tEnd, Mtof(chord[i]), amp, 0.02f, 1.05f, 3200f, 700f, 0.90f, 0f, 1f, 5.5f, 0.12f);
        }

        // Destello agudo final
        Bell(b, tEnd, Mtof(84), 0.24f, 0.85f); // Do6
        Bell(b, tEnd + 0.03f, Mtof(91), 0.14f, 0.75f); // Sol6

        return ToClip("sfx_win", b, 0.50f, 0.45f, 0.88f);
    }

    // Perder
    public static AudioClip Lose()
    {
        float[] b = new float[(int)(4.0f * SR)];

        // --- Bloque1
        Thump(b, 0f, 140f, 65f, 0.60f, 0.22f);
        Rumble(b, 0f, 0.40f, 0.55f, 220f);

        float[] chord1 = { 48, 53, 56, 60, 63 }; // Lab2, Re3, Mib3, Lab3, Si3
        float[] amps1 = { 0.42f, 0.34f, 0.33f, 0.30f, 0.24f };
        for (int i = 0; i < chord1.Length; i++)
        {
            Drone(b, 0.02f, Mtof(chord1[i]), amps1[i], 0.02f, 0.32f, 1450f, 360f, 0.35f, 0.3f, 0.45f, 4.5f, 0.18f);
        }
        Drone(b, 0.06f, Mtof(63), 0.32f, 0.02f, 0.30f, 1300f, 380f, 0.30f, 0.2f, 0.40f, 0f, 0f); // Mib4

        // --- Bloque2
        float t2 = 0.72f;
        Thump(b, t2, 125f, 54f, 0.65f, 0.28f);
        Rumble(b, t2, 0.50f, 1.10f, 200f);

        float[] chord2 = { 45, 50, 52, 55, 58 }; // Fa2, Si2, Do3, Fa3, Lab3 (3 semitonos abajo)
        float[] amps2 = { 0.46f, 0.36f, 0.35f, 0.32f, 0.26f };
        for (int i = 0; i < chord2.Length; i++)
        {
            Drone(b, t2 + 0.02f, Mtof(chord2[i]), amps2[i], 0.02f, 0.85f, 1300f, 280f, 0.85f, 0.7f, 1.0f, 4.5f, 0.22f);
        }
        Drone(b, t2 + 0.08f, Mtof(60), 0.32f, 0.03f, 0.45f, 1150f, 320f, 0.45f, 0.3f, 0.50f, 0f, 0f); // Do4
        Drone(b, t2 + 0.42f, Mtof(56), 0.34f, 0.03f, 0.65f, 1050f, 260f, 0.65f, 0.8f, 0.80f, 0f, 0f); // Lab3

        Thump(b, 1.65f, 120f, 56f, 0.65f, 0.14f);
        Rumble(b, 1.65f, 0.35f, 0.18f, 180f);
        Drone(b, 1.65f, Mtof(41), 0.35f, 0.01f, 0.14f, 700f, 220f, 0.14f, 0f, 1f, 0f, 0f); // Fa2

        Thump(b, 2.02f, 108f, 46f, 0.78f, 0.42f);
        Rumble(b, 2.02f, 0.60f, 0.85f, 170f);
        Drone(b, 2.02f, Mtof(36), 0.45f, 0.02f, 0.70f, 650f, 180f, 0.65f, 0.9f, 0.75f, 0f, 0f); // Do2

        return ToClip("sfx_lose", b, 0.60f, 0.55f, 0.90f);
    }
}