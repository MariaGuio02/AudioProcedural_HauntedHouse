
using UnityEngine;

// ================================================================
// EFECTOS
// ================================================================

public enum Sfx
{
    Walk,
    WalkCreak,
    Ghost,
    Key,
    DoorCreak,
    UIBack,
    JumpPad,
    PowerGrow,
    PowerShrink,
    PowerPickup,
    DoorOpen,
    DoorClose,
    ButtonPress,
    ButtonRelease,
    DoorEnter,
}

public enum SfxPlayback
{
    OneShot,
    Loop
}

// ================================================================
// PROCEDURAL SFX
// ================================================================

public class ProceduralSfx : MonoBehaviour
{
    private const double TwoPi = 2.0 * System.Math.PI;

    private const int OneShotPoolSize = 8;
    private const int LoopPoolSize = 8;

    private static readonly float[] Pure = { 1f };
    private static readonly float[] Two = { 1f, 0.48f };
    private static readonly float[] Soft3 = { 1f, 0.25f, 0.10f };
    private static readonly float[] Saw5 = { 1f, 0.48f, 0.30f, 0.23f, 0.19f };

    public static ProceduralSfx Instance { get; private set; }

    [Range(0f, 1f)]
    public float masterVolume = 1f;

    private const float ClipGain = 2.8f;

    private AudioSource[] _oneShotPool;
    private AudioSource[] _loopPool;

    private int _nextOneShot;
    private int _nextLoop;

    private AudioClip[] _clips;

    private static uint _noiseState = 22222;

    // ============================================================
    // INICIALIZACION
    // ============================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("ProceduralSfx");
        go.AddComponent<ProceduralSfx>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _oneShotPool = new AudioSource[OneShotPoolSize];
        _loopPool = new AudioSource[LoopPoolSize];

        for (int i = 0; i < OneShotPoolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0.5f;

            _oneShotPool[i] = source;
        }

        for (int i = 0; i < LoopPoolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0.5f;

            _loopPool[i] = source;
        }

        Generate();
    }

    private void Generate()
    {
        int sr = AudioSettings.outputSampleRate;

        Sfx[] all = (Sfx[])System.Enum.GetValues(typeof(Sfx));

        _clips = new AudioClip[all.Length];

        foreach (Sfx sfx in all)
        {
            float[] data = Render(sfx, sr);

            AudioClip clip = AudioClip.Create(
                sfx.ToString(),
                data.Length,
                1,
                sr,
                false
            );

            clip.SetData(data, 0);

            _clips[(int)sfx] = clip;
        }
    }

    // ============================================================
    // API PUBLICA
    // ============================================================

    public static void Play(
        Sfx sfx,
        float volume = 1f,
        float pitchRandom = 0.05f)
    {
        if (Instance == null || Instance._clips == null)
            return;

        AudioSource source =
            Instance._oneShotPool[Instance._nextOneShot];

        Instance._nextOneShot =
            (Instance._nextOneShot + 1) % OneShotPoolSize;

        source.Stop();

        source.clip = Instance._clips[(int)sfx];
        source.loop = false;

        source.volume = Mathf.Clamp01(
            volume * Instance.masterVolume
        );

        source.pitch = 1f + Random.Range(
            -pitchRandom,
            pitchRandom
        );

        source.Play();
    }

    private static int _stepsSinceCreak = 99;
private static float _nextStepTime = 0f;

public static void PlayStep(
    float volume = 0.5f,
    float creakChance = 0.15f,
    int minStepsBetweenCreaks = 3,
    float stepInterval = 0.33f)
{
    // Evita que los pasos se reproduzcan demasiado seguido.
    if (Time.time < _nextStepTime)
        return;

    _nextStepTime = Time.time + stepInterval;

    Play(Sfx.Walk, volume, 0.08f);

    _stepsSinceCreak++;

    if (_stepsSinceCreak >= minStepsBetweenCreaks &&
        Random.value < creakChance)
    {
        _stepsSinceCreak = 0;
        Play(Sfx.WalkCreak, volume * 0.8f, 0.12f);
    }
}

    public static void PlayLoop(Sfx sfx, float volume = 1f)
    {
        if (Instance == null || Instance._clips == null)
            return;

        AudioClip clip = Instance._clips[(int)sfx];

        foreach (AudioSource source in Instance._loopPool)
        {
            if (source.clip == clip && source.isPlaying)
            {
                source.volume = Mathf.Clamp01(
                    volume * Instance.masterVolume
                );

                return;
            }
        }

        AudioSource newSource =
            Instance._loopPool[Instance._nextLoop];

        Instance._nextLoop =
            (Instance._nextLoop + 1) % LoopPoolSize;

        newSource.Stop();

        newSource.clip = clip;
        newSource.loop = true;

        newSource.volume = Mathf.Clamp01(
            volume * Instance.masterVolume
        );

        newSource.pitch = 1f;

        newSource.Play();
    }

    public static void StopLoop(Sfx sfx)
    {
        if (Instance == null || Instance._clips == null)
            return;

        foreach (AudioSource source in Instance._loopPool)
        {
            if (source.clip == Instance._clips[(int)sfx])
            {
                source.Stop();
                source.clip = null;
            }
        }
    }

    public static void SetLoopVolume(Sfx sfx, float volume)
    {
        if (Instance == null || Instance._loopPool == null)
            return;

        foreach (AudioSource source in Instance._loopPool)
        {
            if (source.clip == Instance._clips[(int)sfx])
            {
                source.volume = Mathf.Clamp01(
                    volume * Instance.masterVolume
                );
            }
        }
    }

    public static AudioClip GetClip(Sfx sfx)
    {
        if (Instance == null || Instance._clips == null)
            return null;

        return Instance._clips[(int)sfx];
    }

    // ============================================================
    // SISTEMA DE LAYERING
    // ============================================================

    private static float[] CreateLayer(int length)
    {
        return new float[length];
    }

    // Mezcla una capa en el buffer de destino.
    private static void MixLayer(
        float[] destination,
        float[] layer,
        float volume = 1f)
    {
        int length = Mathf.Min(
            destination.Length,
            layer.Length
        );

        for (int i = 0; i < length; i++)
        {
            destination[i] += layer[i] * volume;
        }
    }

    // Pasa-bajos independiente.
    private static void LayerLowPass(
        float[] data,
        int sr,
        float cutoff)
    {
        cutoff = Mathf.Clamp(cutoff, 1f, sr * 0.49f);

        float alpha = 1f - Mathf.Exp(
            -(float)TwoPi * cutoff / sr
        );

        float previous = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            previous += alpha * (data[i] - previous);
            data[i] = previous;
        }
    }

    // Pasa-altos independiente.
    private static void LayerHighPass(
        float[] data,
        int sr,
        float cutoff)
    {
        cutoff = Mathf.Clamp(cutoff, 1f, sr * 0.49f);

        float alpha = Mathf.Exp(
            -(float)TwoPi * cutoff / sr
        );

        float previousInput = 0f;
        float previousOutput = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float input = data[i];

            float output = alpha *
                (previousOutput + input - previousInput);

            data[i] = output;

            previousInput = input;
            previousOutput = output;
        }
    }

    // Distorsión independiente.
    private static void LayerDistortion(
        float[] data,
        float drive)
    {
        drive = Mathf.Max(0f, drive);

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)System.Math.Tanh(data[i] * drive);
        }
    }

    // Tremolo independiente.
    private static void LayerTremolo(
        float[] data,
        int sr,
        float rate,
        float depth)
    {
        depth = Mathf.Clamp01(depth);

        for (int i = 0; i < data.Length; i++)
        {
            float lfo = 0.5f + 0.5f *
                (float)System.Math.Sin(
                    TwoPi * rate * i / sr
                );

            data[i] *= 1f - depth * lfo;
        }
    }

    // Tremolo con frecuencia variable.
private static void LayerVariableTremolo(
    float[] data,
    int sr,
    float startRate,
    float endRate,
    float depth)
{
    depth = Mathf.Clamp01(depth);

    double phase = 0.0;

    for (int i = 0; i < data.Length; i++)
    {
        float t = i / (float)sr;

        float progress = Mathf.Clamp01(
            t / (data.Length / (float)sr)
        );

        float rate = Mathf.Lerp(
            startRate,
            endRate,
            progress
        );

        phase += TwoPi * rate / sr;

        float lfo = 0.5f + 0.5f *
            (float)System.Math.Sin(phase);

        float modulation = 1f - depth * lfo;

        data[i] *= modulation;
    }
}

    // Vibrato independiente.
    private static void LayerVibrato(
        float[] data,
        int sr,
        float rate,
        float depth)
    {
        Vibrato(data, sr, rate, depth);
    }

    // Multiply independiente.
    private static void LayerMultiply(
        float[] data,
        int sr,
        float rate,
        float depth)
    {
        for (int i = 0; i < data.Length; i++)
        {
            float modulation = 0.7f + 0.3f *
                (float)System.Math.Sin(
                    TwoPi * rate * i / sr
                );

            data[i] *= modulation;
        }
    }

    // Eco independiente.
    private static void LayerEcho(
        float[] data,
        double delaySeconds,
        double feedback,
        int sr)
    {
        Echo(data, delaySeconds, feedback, sr);
    }

    // ============================================================
    // RENDERIZADO
    // ============================================================

    private static float[] Render(Sfx sfx, int sr)
    {
        switch (sfx)
        {
            case Sfx.Walk: return RenderWalk(sr);
            case Sfx.WalkCreak: return RenderWalkCreak(sr);
            case Sfx.Ghost: return RenderGhost(sr);
            case Sfx.Key: return RenderKey(sr);
            case Sfx.DoorCreak: return RenderDoorCreak(sr);
            case Sfx.UIBack: return RenderUIBack(sr);
            case Sfx.JumpPad: return RenderJumpPad(sr);
            case Sfx.PowerGrow: return RenderPowerGrow(sr);
            case Sfx.PowerShrink: return RenderPowerShrink(sr);
            case Sfx.PowerPickup: return RenderPowerPickup(sr);
            case Sfx.DoorOpen: return RenderDoorOpen(sr);
            case Sfx.DoorClose: return RenderDoorClose(sr);
            case Sfx.ButtonPress: return RenderButtonPress(sr);
            case Sfx.ButtonRelease: return RenderButtonRelease(sr);
            case Sfx.DoorEnter: return RenderDoorEnter(sr);

            default:
                return new float[sr / 10];
        }
    }

    // ============================================================
    // WALK
    // ============================================================
// Pisada normal sobre madera: golpe grave + "toc" de madera + roce del talón.
private static float[] RenderWalk(int sr)
{
    float[] d = new float[sr / 4];

    // Golpe grave (peso del paso)
    Tone(d, sr,
        100, 60f,
        0.0, 0.10,
        0.001f, 0.08f, 0f, 0.02f,
        0.55f, Pure);

    // Toc de madera (resonancia)
    Tone(d, sr,
        200, 170f,
        0.0, 0.06,
        0.001f, 0.045f, 0f, 0.015f,
        0.20f, Two);


    // Click del talón (ruido agudo, muy corto)
    NoiseBurst(d, sr, 0.0, 0.015,
        0.0005f, 0.012f, 0f, 0.003f, 0.12f,
        2800f, true);

    return Finish(d);
}

// Crujido de madera (tu sonido original). Se suena solo a veces.
private static float[] RenderWalkCreak(int sr)
{
    float[] d = new float[sr / 3];

    Tone(d, sr,
        0, 0,
        0.0, 0.05,
        0.005f, 0.5f, 0.05f, 0.1f,
        0.1f, Two);

    LayerTremolo(d, sr, 8f, 0.7f);

    return Finish(d);
}

    // ============================================================
    // GHOST
    // ============================================================

    private static float[] RenderGhost(int sr)
    {
        float[] d = new float[sr / 3];

        Tone(
            d, sr,
            400f, 100f,
            0.0, 3.0,
            0f, 0f, 1f, 0f,
            0.4f, Two
        );

        Vibrato(d, sr, 15f, 1f);
        Multiply(d, sr, 2f, 0.3f);

        return Finish(d);
    }

    // ============================================================
    // UI CLICK
    // ============================================================

    private static float[] RenderKey(int sr)
{
    float[] finalMix = new float[sr / 2 + sr / 4];

    // ============================================================
    // LAYER 1: TONO PRINCIPAL DE LA TECLA
    // ============================================================

    float[] key = CreateLayer(finalMix.Length);

    Tone(
        key, sr,
        587.33f, 587.33f,
        0.0, 0.09,
        0.003f, 0.07f, 0f, 0.02f,
        0.35f, Two
    );

    MixLayer(finalMix, key, 1f);

    // ============================================================
    // LAYER 2: WHISTLE / SILBIDO
    // ============================================================

    float[] whistle = CreateLayer(finalMix.Length);

    // Silbido ascendente suave.
    Tone(
        whistle, sr,
        1100f, 1850f,
        0.015, 0.32,
        0.04f, 0.18f, 0.65f, 0.10f,
        0.16f, Pure
    );

    // Pequeña caída de frecuencia al final,
    // para darle un carácter más orgánico.
    Tone(
        whistle, sr,
        1850f, 1450f,
        0.24, 0.22,
        0.015f, 0.12f, 0.25f, 0.08f,
        0.07f, Pure
    );

    // Ligera modulación del silbido.
    LayerTremolo(
        whistle,
        sr,
        7f,
        0.15f
    );

    MixLayer(finalMix, whistle, 1f);

    // ============================================================
    // LAYER 3: SPARKLE / AGUDOS
    // ============================================================

    float[] sparkle = CreateLayer(finalMix.Length);

    Tone(
        sparkle, sr,
        783.99f, 783.99f,
        0.18, 0.30,
        0.005f, 0.25f, 0f, 0.05f,
        0.12f, Two
    );

    LayerHighPass(sparkle, sr, 700f);

    LayerTremolo(
        sparkle,
        sr,
        12f,
        0.2f
    );

    MixLayer(finalMix, sparkle, 0.8f);

    // IMPORTANTE: antes retornabas d y perdías
    // las capas de sparkle que estaban en finalMix.
    return Finish(finalMix);
}



    // ============================================================
// DOOR CLICK + CREAK
// ============================================================

private static float[] RenderDoorCreak(int sr)
{
    float[] finalMix = new float[(int)(sr * 0.7f)];

    // --------------------------------------------------------
    // LAYER 1: CLICK MECÁNICO
    // --------------------------------------------------------

    float[] click = CreateLayer(finalMix.Length);

    Tone(
        click, sr,
        1800f, 750f,
        0.0, 0.025,
        0.001f, 0.008f, 0f, 0.012f,
        0.45f, Two
    );

    MixLayer(finalMix, click, 1f);

    // --------------------------------------------------------
    // LAYER 2: CRUJIDO DE LA PUERTA
    // --------------------------------------------------------

    float[] creak = CreateLayer(finalMix.Length);

    Tone(
        creak, sr,
        240f, 95f,
        0.025, 0.55,
        0.015f, 0.18f, 0.25f, 0.20f,
        0.30f, Saw5
    );

    // El tremolo cambia progresivamente su frecuencia.
    LayerVariableTremolo(
        creak,
        sr,
        4f,     // Frecuencia inicial
        13f,    // Frecuencia final
        0.70f   // Profundidad
    );

    MixLayer(finalMix, creak, 1f);

    return Finish(finalMix);
}

    // ============================================================
    // UI BACK
    // ============================================================

    private static float[] RenderUIBack(int sr)
    {
        float[] d = new float[sr / 4];

        Tone(
            d, sr,
            440f, 329.63f,
            0.0, 0.11,
            0.003f, 0.09f, 0f, 0.02f,
            0.35f, Two
        );

        return Finish(d);
    }

    // ============================================================
    // JUMP PAD - LAYERING
    // ============================================================

    private static float[] RenderJumpPad(int sr)
    {
        float[] finalMix = new float[sr / 2 + sr / 4];

        // --------------------------------------------------------
        // LAYER 1: SUB / GRAVES
        // --------------------------------------------------------

        float[] sub = CreateLayer(finalMix.Length);

        Tone(
            sub, sr,
            130f, 520f,
            0.0, 0.25,
            0.004f, 0.20f, 0.2f, 0.05f,
            0.35f, Saw5
        );

        LayerLowPass(sub, sr, 500f);
        LayerTremolo(sub, sr, 8f, 0.15f);

        MixLayer(finalMix, sub, 0.8f);

        // --------------------------------------------------------
        // LAYER 2: BODY / MEDIOS
        // --------------------------------------------------------

        float[] body = CreateLayer(finalMix.Length);

        Tone(
            body, sr,
            260f, 1040f,
            0.0, 0.25,
            0.004f, 0.20f, 0.1f, 0.05f,
            0.12f, Pure
        );

        LayerHighPass(body, sr, 300f);
        LayerDistortion(body, 1.5f);

        MixLayer(finalMix, body, 0.7f);

        // --------------------------------------------------------
        // LAYER 3: SPARKLE / AGUDOS
        // --------------------------------------------------------

        float[] sparkle = CreateLayer(finalMix.Length);

        Tone(
            sparkle, sr,
            783.99f, 783.99f,
            0.18, 0.30,
            0.005f, 0.25f, 0f, 0.05f,
            0.12f, Two
        );

        LayerHighPass(sparkle, sr, 700f);
        LayerTremolo(sparkle, sr, 12f, 0.2f);

        MixLayer(finalMix, sparkle, 0.8f);

        // --------------------------------------------------------
        // MASTER EFFECTS
        // --------------------------------------------------------

        Echo(finalMix, 0.09, 0.25, sr);

        return Finish(finalMix);
    }

    // ============================================================
    // POWER GROW
    // ============================================================

    private static float[] RenderPowerGrow(int sr)
    {
        float[] d = new float[(int)(sr * 1.3f)];

        float[] notes =
        {
            392.00f, 493.88f, 587.33f, 783.99f
        };

        for (int i = 0; i < notes.Length; i++)
        {
            Tone(
                d, sr,
                notes[i], notes[i],
                i * 0.07, 0.35,
                0.004f, 0.25f, 0.1f, 0.10f,
                0.25f, Two
            );
        }

        Tone(
            d, sr,
            392f, 1568f,
            0.0, 0.40,
            0.01f, 0.30f, 0.1f, 0.10f,
            0.07f, Pure
        );

        Echo(d, 0.14, 0.30, sr);

        return Finish(d);
    }

    // ============================================================
    // POWER SHRINK
    // ============================================================

    private static float[] RenderPowerShrink(int sr)
    {
        float[] d = new float[(int)(sr * 1.1f)];

        float[] notes =
        {
            783.99f, 587.33f, 493.88f, 392.00f
        };

        for (int i = 0; i < notes.Length; i++)
        {
            Tone(
                d, sr,
                notes[i], notes[i],
                i * 0.065, 0.28,
                0.004f, 0.20f, 0.05f, 0.08f,
                0.25f, Pure
            );
        }

        Tone(
            d, sr,
            1568f, 392f,
            0.0, 0.35,
            0.01f, 0.25f, 0.1f, 0.10f,
            0.07f, Pure
        );

        Echo(d, 0.12, 0.25, sr);

        return Finish(d);
    }

    // ============================================================
    // POWER PICKUP
    // ============================================================

    private static float[] RenderPowerPickup(int sr)
    {
        float[] d = new float[sr];

        Tone(
            d, sr,
            783.99f, 783.99f,
            0.00, 0.20,
            0.003f, 0.15f, 0.05f, 0.05f,
            0.30f, Two
        );

        Tone(
            d, sr,
            1174.66f, 1174.66f,
            0.08, 0.40,
            0.003f, 0.30f, 0.05f, 0.10f,
            0.30f, Two
        );

        Echo(d, 0.10, 0.25, sr);

        return Finish(d);
    }

    // ============================================================
    // DOOR OPEN
    // ============================================================

    private static float[] RenderDoorOpen(int sr)
    {
        float[] d = new float[(int)(sr * 1f)];

        Tone(d, sr, 98f, 196f, 0.03, 0.17,
            0.03f, 0.10f, 0.3f, 0.05f, 0.14f, Saw5);

        NoiseBurst(d, sr, 0.22, 0.30,
            0.01f, 0.20f, 0.3f, 0.08f, 0.34f,
            4500f, false, 12f, 0.7f);

        Tone(d, sr, 196f, 196f, 0.23, 0.24,
            0.01f, 0.15f, 0.4f, 0.06f, 0.45f, Saw5);

        Tone(d, sr, 98f, 98f, 0.23, 0.22,
            0.01f, 0.15f, 0.4f, 0.06f, 0.20f, Saw5);

        Tone(d, sr, 392f, 392f, 0.24, 0.16,
            0.01f, 0.12f, 0.2f, 0.05f, 0.12f, Two);

        NoiseBurst(d, sr, 0.43, 0.07,
            0.001f, 0.06f, 0f, 0.01f, 0.90f,
            2500f, true);

        Tone(d, sr, 784f, 784f, 0.43, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.14f, Two);

        Tone(d, sr, 1568f, 1568f, 0.43, 0.14,
            0.001f, 0.12f, 0f, 0.02f, 0.12f, Pure);

        NoiseBurst(d, sr, 0.46, 0.55,
            0.02f, 0.40f, 0.2f, 0.15f, 0.025f,
            1500f, false, 12f, 0.7f);

        Tone(d, sr, 196f, 165f, 0.46, 0.55,
            0.02f, 0.40f, 0.25f, 0.15f, 0.14f, Saw5);

        NoiseBurst(d, sr, 0.73, 0.06,
            0.001f, 0.05f, 0f, 0.01f, 0.30f,
            1200f, true);

        Tone(d, sr, 392f, 392f, 0.73, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.12f, Two);

        NoiseBurst(d, sr, 0.88, 0.07,
            0.001f, 0.06f, 0f, 0.01f, 0.30f,
            1500f, true);

        Tone(d, sr, 392f, 392f, 0.88, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.12f, Two);

        Tone(d, sr, 784f, 784f, 0.88, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.08f, Pure);

        Tone(d, sr, 196f, 98f, 0.95, 0.40,
            0.05f, 0.25f, 0.1f, 0.10f, 0.03f, Two);

        return Finish(d);
    }

    // ============================================================
    // DOOR CLOSE
    // ============================================================

    private static float[] RenderDoorClose(int sr)
    {
        float[] d = new float[(int)(sr * 1.2f)];

        NoiseBurst(d, sr, 0.0, 0.30,
            0.02f, 0.20f, 0.3f, 0.08f, 0.30f,
            4000f, false, 12f, 0.7f);

        Tone(d, sr, 196f, 165f, 0.0, 0.30,
            0.02f, 0.20f, 0.3f, 0.08f, 0.40f, Saw5);

        Tone(d, sr, 98f, 82f, 0.0, 0.30,
            0.02f, 0.20f, 0.3f, 0.08f, 0.16f, Saw5);

        Tone(d, sr, 196f, 98f, 0.32, 0.28,
            0.003f, 0.22f, 0f, 0.05f, 0.40f, Saw5);

        NoiseBurst(d, sr, 0.32, 0.12,
            0.002f, 0.10f, 0f, 0.02f, 0.30f,
            1500f, false);

        NoiseBurst(d, sr, 0.32, 0.07,
            0.001f, 0.06f, 0f, 0.01f, 0.80f,
            2500f, true);

        Tone(d, sr, 784f, 784f, 0.32, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.14f, Two);

        Tone(d, sr, 1568f, 1568f, 0.32, 0.14,
            0.001f, 0.12f, 0f, 0.02f, 0.12f, Pure);

        NoiseBurst(d, sr, 0.55, 0.06,
            0.001f, 0.05f, 0f, 0.01f, 0.28f,
            1200f, true);

        Tone(d, sr, 392f, 392f, 0.55, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.12f, Two);

        NoiseBurst(d, sr, 0.68, 0.07,
            0.001f, 0.06f, 0f, 0.01f, 0.26f,
            1500f, true);

        Tone(d, sr, 392f, 392f, 0.68, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.12f, Two);

        Tone(d, sr, 784f, 784f, 0.68, 0.12,
            0.001f, 0.10f, 0f, 0.02f, 0.08f, Pure);

        Tone(d, sr, 196f, 98f, 0.60, 0.45,
            0.05f, 0.25f, 0.1f, 0.10f, 0.03f, Two);

        return Finish(d);
    }

    // ============================================================
    // DOOR ENTER
    // ============================================================

    private static float[] RenderDoorEnter(int sr)
    {
        float[] d = new float[(int)(sr * 0.7f)];

        Tone(d, sr, 1174.66f, 293.66f, 0.0, 0.36,
            0.005f, 0.28f, 0.2f, 0.06f, 0.35f, Two);

        Tone(d, sr, 2349.32f, 587.33f, 0.0, 0.30,
            0.005f, 0.24f, 0.1f, 0.05f, 0.10f, Pure);

        NoiseBurst(d, sr, 0.0, 0.34,
            0.02f, 0.25f, 0.1f, 0.07f, 0.20f,
            2500f, true, 14f, 0.5f);

        Tone(d, sr, 196f, 147f, 0.32, 0.22,
            0.003f, 0.18f, 0f, 0.04f, 0.35f, Two);

        Tone(d, sr, 392f, 392f, 0.32, 0.18,
            0.003f, 0.14f, 0f, 0.04f, 0.18f, Pure);

        Echo(d, 0.08, 0.20, sr);

        return Finish(d);
    }

   

    // ============================================================
    // BUTTON PRESS
    // ============================================================

    private static float[] RenderButtonPress(int sr)
    {
        float[] d = new float[sr / 2];

        NoiseBurst(d, sr, 0.0, 0.03,
            0.001f, 0.025f, 0f, 0.005f, 0.30f,
            1800f, true);

        Tone(d, sr, 196f, 130f, 0.0, 0.12,
            0.002f, 0.10f, 0f, 0.02f, 0.40f, Pure);

        Tone(d, sr, 392f, 392f, 0.02, 0.22,
            0.003f, 0.18f, 0f, 0.04f, 0.12f, Two);

        return Finish(d);
    }

    // ============================================================
    // BUTTON RELEASE
    // ============================================================

    private static float[] RenderButtonRelease(int sr)
    {
        float[] d = new float[sr / 3];

        NoiseBurst(d, sr, 0.0, 0.02,
            0.001f, 0.015f, 0f, 0.005f, 0.18f,
            2200f, true);

        Tone(d, sr, 293.66f, 220f, 0.0, 0.07,
            0.002f, 0.06f, 0f, 0.01f, 0.22f, Pure);

        return Finish(d);
    }

   
    private static void Vibrato(
        float[] data,
        int sr,
        float rate,
        float depth)
    {
        float[] original = (float[])data.Clone();

        for (int i = 0; i < data.Length; i++)
        {
            double t = i / (double)sr;

            float modulation = depth *
                (float)System.Math.Sin(
                    TwoPi * rate * t
                );

            double readPosition =
                i + modulation * sr / 1000.0;

            int index = Mathf.Clamp(
                (int)readPosition,
                0,
                original.Length - 1
            );

            data[i] = original[index];
        }
    }

    private static void Multiply(
        float[] data,
        int sr,
        float rate,
        float depth)
    {
        for (int i = 0; i < data.Length; i++)
        {
            float modulation = 0.7f + 0.3f *
                (float)System.Math.Sin(
                    TwoPi * rate * i / sr
                );

            data[i] *= modulation;
        }
    }

    // ============================================================
    // OSCILADOR CON ARMONICOS Y ADSR
    // ============================================================

    private static void Tone(
        float[] data,
        int sr,
        float f0,
        float f1,
        double start,
        double duration,
        float attack,
        float decay,
        float sustain,
        float release,
        float level,
        float[] harmonics)
    {
        int first = Mathf.Max(0, (int)(start * sr));

        int last = System.Math.Min(
            data.Length,
            (int)((start + duration) * sr)
        );

        if (f0 <= 0f || duration <= 0.0 || harmonics == null)
            return;

        float norm = 0f;

        for (int h = 0; h < harmonics.Length; h++)
            norm += Mathf.Abs(harmonics[h]);

        if (norm <= 0f)
            norm = 1f;

        double ratio = f1 / (double)f0;
        double phase = 0.0;

        for (int i = first; i < last; i++)
        {
            double t = (i - first) / (double)sr;
            double k = t / duration;

            double freq = f0 * System.Math.Pow(ratio, k);

            phase += TwoPi * freq / sr;

            float env = Adsr(
                t,
                duration,
                attack,
                decay,
                sustain,
                release
            );

            float sample = 0f;

            for (int h = 0; h < harmonics.Length; h++)
            {
                sample += harmonics[h] *
                    (float)System.Math.Sin(
                        phase * (h + 1)
                    );
            }

            data[i] += level * env * sample / norm;
        }
    }

    // ============================================================
    // RUIDO FILTRADO CON ADSR
    // ============================================================

    private static void NoiseBurst(
        float[] data,
        int sr,
        double start,
        double duration,
        float attack,
        float decay,
        float sustain,
        float release,
        float level,
        float cutoff,
        bool highpass,
        float amRate = 0f,
        float amDepth = 0f)
    {
        int first = Mathf.Max(0, (int)(start * sr));

        int last = System.Math.Min(
            data.Length,
            (int)((start + duration) * sr)
        );

        cutoff = Mathf.Clamp(cutoff, 1f, sr * 0.49f);

        float alpha = 1f - (float)System.Math.Exp(
            -TwoPi * cutoff / sr
        );

        float gain = highpass
            ? 1f
            : Mathf.Min(6f, Mathf.Sqrt((2f - alpha) / alpha));

        float lp = 0f;

        for (int i = first; i < last; i++)
        {
            double t = (i - first) / (double)sr;

            float n = NextNoise();

            lp += alpha * (n - lp);

            float x = highpass
                ? n - lp
                : lp * gain;

            float env = Adsr(
                t,
                duration,
                attack,
                decay,
                sustain,
                release
            );

            float am = 1f;

            if (amRate > 0f)
            {
                am = 1f - amDepth *
                    (0.5f + 0.5f *
                    (float)System.Math.Sin(
                        TwoPi * amRate * t
                    ));
            }

            data[i] += level * env * x * am;
        }
    }

    // ============================================================
    // ADSR
    // ============================================================

    private static float Adsr(
        double t,
        double duration,
        float a,
        float d,
        float s,
        float r)
    {
        if (t < 0.0 || t >= duration)
            return 0f;

        double total = a + d + r;

        double k = total > duration
            ? duration / total
            : 1.0;

        double aa = a * k;
        double dd = d * k;
        double rr = r * k;

        if (t < aa)
            return aa > 0.0
                ? (float)(t / aa)
                : 1f;

        t -= aa;

        if (t < dd)
        {
            return Mathf.Lerp(
                1f,
                s,
                dd > 0.0 ? (float)(t / dd) : 1f
            );
        }

        t -= dd;

        double sustainLength =
            duration - aa - dd - rr;

        if (t < sustainLength)
            return s;

        t -= sustainLength;

        return rr > 0.0
            ? s * (1f - (float)(t / rr))
            : 0f;
    }

    // ============================================================
    // ECO
    // ============================================================

    private static void Echo(
        float[] data,
        double delaySeconds,
        double feedback,
        int sr)
    {
        int delay = (int)(delaySeconds * sr);

        if (delay <= 0 || delay >= data.Length)
            return;

        float fb = Mathf.Clamp((float)feedback, 0f, 0.95f);

        for (int i = delay; i < data.Length; i++)
        {
            data[i] += data[i - delay] * fb;
        }
    }

    // ============================================================
    // GENERADOR DE RUIDO
    // ============================================================

    private static float NextNoise()
    {
        _noiseState ^= _noiseState << 13;
        _noiseState ^= _noiseState >> 17;
        _noiseState ^= _noiseState << 5;

        return (_noiseState / (float)uint.MaxValue) * 2f - 1f;
    }

    // ============================================================
    // MASTER / LIMITADOR SUAVE
    // ==============================================================

    private static float[] Finish(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (float)System.Math.Tanh(
                data[i] * ClipGain
            );
        }

        return data;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
