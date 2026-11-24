using UnityEngine;
using System;

public enum ContinuousSfx
{
    Walk,
    Ghost,
    GhostShower,
    GhostElegant,
    Machine,
    Alarm,
    Fire
}

public class ContinuousProceduralSfx : MonoBehaviour
{
    private const double TwoPi = 2.0 * Math.PI;
    private const int PoolSize = 2;

    public static ContinuousProceduralSfx Instance { get; private set; }

    [Range(0f, 1f)]
    public float masterVolume = 1f;

    private AudioSource[] _sources;
    private AudioClip[] _clips;
    private SoundState[] _states;
    private int _sampleRate;

   

    // ==================================================
    // TONE: cada oscilador tiene sus propios parametros
    // ==================================================

    [Serializable]
    private class ToneState
    {
        // Frecuencia
        public float frequency;
        public float targetFrequency;
        public float glideSpeed;

        // Volumen individual
        public float volume = 1f;

        // Fase del oscilador
        public double phase;

        // Vibrato 1
        public float vibratoRate;
        public float vibratoDepth;
        public double vibratoPhase;

        // Vibrato 2
        public float vibrato2Rate;
        public float vibrato2Depth;
        public double vibrato2Phase;

        // Tremolo
        public float tremoloRate;
        public float tremoloDepth;
        public double tremoloPhase;

        // Multiply
        public float multiplyRate;
        public float multiplyDepth;
        public double multiplyPhase;

        // Armonicos
        public float[] harmonics = { 1f };
    }

    // ==================================================
    // SOUND: controles globales del sonido
    // ==================================================

    private class SoundState
    {
        public ContinuousSfx sound;

        public bool playing;
        public bool stopping;

        public ToneState[] tones;

        // Volumen global
        public float volume;
        public float targetVolume;
        public float smoothing = 3f;

        // Fade de salida
        public float fadeDuration = 2f;
        public float fadeStartVolume;
        public float fadeElapsed;

        // Fade global
        public float fadeMultiplier = 1f;

         // ==================================================
        // LLUVIA / GOTAS
        // ==================================================

        public bool dropletsEnabled;

        public float dropletTimer;
        public float dropletDuration;
        public float dropletElapsed;

        public float dropletFrequency;
        public float dropletVolume = 0.18f;

        public double dropletPhase;

        public float dropletEnvelope;

        public uint dropletRandomState = 1234567;


        // ==========================================
        // PIEDRA DESLIZANTE / GOLPES MECÁNICOS
        // ==========================================

        public bool stoneEnabled;

        public float stoneTimer;
        public float stoneElapsed;
        public float stoneDuration;

        public int stoneBurstIndex;

        public float stoneFrequency;
        public float stoneVolume = 0.20f;

        public double stonePhase;

        public float stoneNoiseFilter;

        public uint stoneRandomState = 987654321;


        // ==========================================
// FUEGO
// ==========================================

public bool fireEnabled;
public bool fireInitialized;
public float fireIntensity = 1f;

public uint fireRandomState = 2463534242;

// Coeficientes de filtros
public float fireAlphaRumble;
public float fireAlphaBody;
public float fireAlphaHiss;
public float fireAlphaHiss2;
public float fireGainRumble;
public float fireGainBody;

// Estados de filtros
public float fireRumbleLp;
public float fireBodyLp;
public float fireHissLp;
public float fireHissLp2;

// LFOs de parpadeo de la llama
public double fireLfo1;
public double fireLfo2;
public double fireLfo3;

// Chispas
public bool sparkActive;
public bool sparkIsPop;
public float sparkTimer;
public float sparkElapsed;
public float sparkDuration;
public float sparkVolume;
public float sparkFrequency;
public double sparkPhase;
public float sparkNoiseLp;


        }

    // ==================================================
    // INICIALIZACION
    // ==================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject go = new GameObject("ContinuousProceduralSfx");
        go.AddComponent<ContinuousProceduralSfx>();
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

        _sampleRate = AudioSettings.outputSampleRate;

        _sources = new AudioSource[PoolSize];
        _clips = new AudioClip[PoolSize];
        _states = new SoundState[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            _sources[i] = gameObject.AddComponent<AudioSource>();

            _sources[i].playOnAwake = false;
            _sources[i].loop = true;
            _sources[i].spatialBlend = 0f;

            int index = i;

            _clips[i] = AudioClip.Create(
                "ContinuousSfx_" + i,
                _sampleRate,
                1,
                _sampleRate,
                true,
                data => GenerateSound(data, index)
            );

            _sources[i].clip = _clips[i];
        }
    }

    // ==================================================
    // CONFIGURACION DE TONES
    // ==================================================

    private static ToneState NewTone(
        float frequency,
        float targetFrequency,
        float volume)
    {
        return new ToneState
        {
            frequency = frequency,
            targetFrequency = targetFrequency,
            volume = Mathf.Clamp01(volume)
        };
    }

    private static void Vibrato(
        ToneState tone,
        float rate,
        float depth)
    {
        tone.vibratoRate = Mathf.Max(0f, rate);
        tone.vibratoDepth = Mathf.Max(0f, depth);
    }

    private static void Vibrato2(
        ToneState tone,
        float rate,
        float depth)
    {
        tone.vibrato2Rate = Mathf.Max(0f, rate);
        tone.vibrato2Depth = Mathf.Max(0f, depth);
    }

    private static void Tremolo(
        ToneState tone,
        float rate,
        float depth)
    {
        tone.tremoloRate = Mathf.Max(0f, rate);
        tone.tremoloDepth = Mathf.Clamp01(depth);
    }

    private static void Multiply(
        ToneState tone,
        float rate,
        float depth)
    {
        tone.multiplyRate = Mathf.Max(0f, rate);
        tone.multiplyDepth = Mathf.Clamp01(depth);
    }

    private static void Harmonics(
        ToneState tone,
        params float[] harmonics)
    {
        tone.harmonics = harmonics;
    }

    // ==================================================
    // RENDER DE CADA SONIDO
    // ==================================================

    private static SoundState Render(ContinuousSfx sfx)
    {
        SoundState state = new SoundState();

        switch (sfx)
        {
            // ------------------------------------------
            case ContinuousSfx.Walk:
            {
                ToneState t0 = NewTone(400f, 100f, 1f);

                Tremolo(t0, 20f, 0.5f);
                Harmonics(t0, 1f, 0.48f, 0.30f);

                state.tones = new ToneState[] { t0 };
                state.targetVolume = 0.3f;
                break;
            }

            // ------------------------------------------
            // ------------------------------------------
            case ContinuousSfx.Ghost:
            {
                // TONE 0: cuerpo principal, grave y espectral
                ToneState t0 = NewTone(420f, 320f, 1f);

                Vibrato(t0, 0.45f, 28f);
                Vibrato2(t0, 1.8f, 9f);

                Tremolo(t0, 0.65f, 0.30f);
                Multiply(t0, 1.3f, 0.18f);

                Harmonics(t0,
                    1f,
                    0.20f,
                    0.08f,
                    0.03f
                );

                // TONE 1: capa aguda, aire y movimiento
                ToneState t1 = NewTone(650f, 780f, 0.30f);

                Vibrato(t1, 0.8f, 18f);
                Vibrato2(t1, 3.2f, 5f);

                Tremolo(t1, 1.1f, 0.55f);
                Multiply(t1, 2.4f, 0.15f);

                Harmonics(t1,
                    1f,
                    0.08f,
                    0.03f
                );

                // TONE 2: subgrave muy sutil
                ToneState t2 = NewTone(180f, 145f, 0.18f);

                Vibrato(t2, 0.25f, 5f);
                Vibrato2(t2, 0.6f, 2f);

                Tremolo(t2, 0.35f, 0.20f);

                Harmonics(t2,
                    1f,
                    0.15f,
                    0.05f
                );

                state.tones = new ToneState[] { t0, t1, t2 };

                state.targetVolume = 0.22f;
                state.smoothing = 1.5f;

                break;
            }
            // ------------------------------------------
            // ------------------------------------------
            case ContinuousSfx.GhostShower:
            {
                // TONE 0: cuerpo fantasmal lento
                ToneState t0 = NewTone(330f, 270f, 1f);

                // Vibraciones muy lentas y suaves
                Vibrato(t0, 0.20f, 12f);
                Vibrato2(t0, 0.45f, 5f);

                // Respiración lenta
                Tremolo(t0, 0.25f, 0.30f);
                Multiply(t0, 0.40f, 0.10f);

                Harmonics(t0,
                    1f,
                    0.20f,
                    0.06f
                );

                // TONE 1: aura etérea
                ToneState t1 = NewTone(540f, 480f, 0.25f);

                Vibrato(t1, 0.30f, 8f);
                Vibrato2(t1, 0.65f, 3f);

                Tremolo(t1, 0.35f, 0.45f);
                Multiply(t1, 0.55f, 0.12f);

                Harmonics(t1,
                    1f,
                    0.10f,
                    0.03f
                );

                state.tones = new ToneState[] { t0, t1 };

                state.targetVolume = 0.20f;
                state.smoothing = 1.2f;

                // Activar gotas de llovizna
                state.dropletsEnabled = true;
                state.dropletTimer = Mathf.Lerp(0.01f, 0.02f, RandomDroplet(state));

                break;
            }


            case ContinuousSfx.GhostElegant:
            {
                // TONE 0: cuerpo principal, grave y espectral
                ToneState t0 = NewTone(520f, 320f, 1f);

                Vibrato(t0, 0.45f, 28f);
                Vibrato2(t0, 1.8f, 9f);

                Tremolo(t0, 0.65f, 0.30f);
                Multiply(t0, 1.3f, 0.18f);

                Harmonics(t0,
                    1f,
                    0.20f,
                    0.08f,
                    0.03f
                );

                // TONE 1: capa aguda, aire y movimiento
                ToneState t1 = NewTone(550f, 650f, 0.30f);

                Vibrato(t1, 0.8f, 18f);
                Vibrato2(t1, 3.2f, 5f);

                Tremolo(t1, 1.1f, 0.55f);
                Multiply(t1, 2.4f, 0.15f);

                Harmonics(t1,
                    1f,
                    0.08f,
                    0.03f
                );

                // TONE 2: subgrave muy sutil
                ToneState t2 = NewTone(40f, 180, 0.18f);

                Vibrato(t2, 0.25f, 5f);
                Vibrato2(t2, 0.6f, 2f);

                Tremolo(t2, 0.35f, 0.20f);

                Harmonics(t2,
                    1f,
                    0.15f,
                    0.05f
                );

                state.tones = new ToneState[] { t0, t1, t2 };

                state.targetVolume = 0.22f;
                state.smoothing = 1.5f;

                break;
            }

            // ------------------------------------------
            case ContinuousSfx.Machine:
            {
                ToneState t0 = NewTone(100f, 100f, 1f);

                Vibrato(t0, 10f, 1f);
                Tremolo(t0, 20f, 0.15f);
                Harmonics(t0, 1f, 0.48f, 0.30f, 0.23f);

                ToneState t1 = NewTone(50f, 50f, 0.35f);

                Vibrato(t1, 3f, 2f);
                Tremolo(t1, 8f, 0.4f);
                Multiply(t1, 12f, 0.2f);

                state.tones = new ToneState[] { t0, t1 };
                state.targetVolume = 0.4f;

                state.stoneEnabled = true;
                state.stoneTimer = 0.25f;
                state.stoneBurstIndex = 0;
                state.stoneVolume = 0.20f;
                break;
            }

            // ------------------------------------------
            case ContinuousSfx.Alarm:
            {
                ToneState t0 = NewTone(600f, 600f, 1f);

                Tremolo(t0, 4f, 0.8f);
                Harmonics(t0, 1f, 0.25f, 0.10f);

                ToneState t1 = NewTone(900f, 900f, 0.3f);

                Tremolo(t1, 6f, 0.5f);
                Vibrato(t1, 8f, 20f);

                state.tones = new ToneState[] { t0, t1 };
                state.targetVolume = 0.5f;
                break;
            }

            // ------------------------------------------
            case ContinuousSfx.Fire:
            {
                // El fuego se genera con ruido filtrado, no con tones.
                state.tones = new ToneState[0];

                state.targetVolume = 0.6f;
                state.smoothing = 1.5f;

                state.fireEnabled = true;
                state.fireIntensity = 1f;
                state.sparkTimer = 0.2f;

                break;
            }

            
        }

        return state;
    }

    // ==================================================
    // API: REPRODUCCION
    // ==================================================

    public static void PlayContinuous(
        ContinuousSfx sfx,
        float volume = 1f)
    {
        if (Instance == null) return;

        var p = Instance;

        // Si ya existe, reactivar o actualizar volumen
        for (int i = 0; i < PoolSize; i++)
        {
            SoundState existing = p._states[i];

            if (existing != null &&
                existing.playing &&
                existing.sound == sfx)
            {
                existing.stopping = false;
                existing.targetVolume = Mathf.Clamp01(volume) *
                    Render(sfx).targetVolume;
                existing.fadeMultiplier = 1f;
                return;
            }
        }

        // Buscar espacio
        int slot = -1;

        for (int i = 0; i < PoolSize; i++)
        {
            if (p._states[i] == null || !p._states[i].playing)
            {
                slot = i;
                break;
            }
        }

        if (slot == -1) return;

        SoundState state = Render(sfx);

        state.sound = sfx;
        state.playing = true;
        state.volume = 0f;

        float defaultVolume = state.targetVolume;

        state.targetVolume =
            defaultVolume * Mathf.Clamp01(volume);

        p._states[slot] = state;

        p._sources[slot].Stop();
        p._sources[slot].clip = p._clips[slot];
        p._sources[slot].loop = true;
        p._sources[slot].Play();
    }

    // ==================================================
    // API: VOLUMEN GLOBAL DEL SONIDO
    // ==================================================

    public static void SetVolume(
        ContinuousSfx sfx,
        float volume)
    {
        SoundState state = Find(sfx);

        if (state == null) return;

        state.targetVolume = Mathf.Clamp01(volume);
    }

    // ==================================================
    // API: FRECUENCIA POR TONE
    // ==================================================

    public static void SetToneFrequency(
        ContinuousSfx sfx,
        int toneIndex,
        float frequency,
        float glideSpeed = 0f)
    {
        ToneState tone = GetTone(sfx, toneIndex);

        if (tone == null) return;

        tone.targetFrequency = Mathf.Max(1f, frequency);
        tone.glideSpeed = Mathf.Max(0f, glideSpeed);
    }

    // Compatibilidad: cambia frecuencia del Tone 0
    public static void SetFrequency(
        ContinuousSfx sfx,
        float frequency,
        float glideSpeed = 0f)
    {
        SetToneFrequency(sfx, 0, frequency, glideSpeed);
    }

    // ==================================================
    // API: VOLUMEN INDIVIDUAL
    // ==================================================

    public static void SetToneVolume(
        ContinuousSfx sfx,
        int toneIndex,
        float volume)
    {
        ToneState tone = GetTone(sfx, toneIndex);

        if (tone == null) return;

        tone.volume = Mathf.Clamp01(volume);
    }

    // ==================================================
    // API: TREMolo INDIVIDUAL
    // ==================================================

    public static void SetToneTremolo(
        ContinuousSfx sfx,
        int toneIndex,
        float rate,
        float depth)
    {
        ToneState tone = GetTone(sfx, toneIndex);

        if (tone == null) return;

        tone.tremoloRate = Mathf.Max(0f, rate);
        tone.tremoloDepth = Mathf.Clamp01(depth);
    }

    // Compatibilidad: aplica al Tone 0
    public static void SetTremolo(
        ContinuousSfx sfx,
        float rate,
        float depth)
    {
        SetToneTremolo(sfx, 0, rate, depth);
    }

    // ==================================================
    // API: VIBRATO INDIVIDUAL
    // ==================================================

    public static void SetToneVibrato(
        ContinuousSfx sfx,
        int toneIndex,
        float rate,
        float depth)
    {
        ToneState tone = GetTone(sfx, toneIndex);

        if (tone == null) return;

        tone.vibratoRate = Mathf.Max(0f, rate);
        tone.vibratoDepth = Mathf.Max(0f, depth);
    }

    // Compatibilidad: aplica al Tone 0
    public static void SetVibrato(
        ContinuousSfx sfx,
        float rate,
        float depth)
    {
        SetToneVibrato(sfx, 0, rate, depth);
    }

    // ==================================================
    // API: SEGUNDO VIBRATO
    // ==================================================

    public static void SetToneVibrato2(
        ContinuousSfx sfx,
        int toneIndex,
        float rate,
        float depth)
    {
        ToneState tone = GetTone(sfx, toneIndex);

        if (tone == null) return;

        tone.vibrato2Rate = Mathf.Max(0f, rate);
        tone.vibrato2Depth = Mathf.Max(0f, depth);
    }

    // ==================================================
    // API: MULTIPLY INDIVIDUAL
    // ==================================================

    public static void SetToneMultiply(
        ContinuousSfx sfx,
        int toneIndex,
        float rate,
        float depth)
    {
        ToneState tone = GetTone(sfx, toneIndex);

        if (tone == null) return;

        tone.multiplyRate = Mathf.Max(0f, rate);
        tone.multiplyDepth = Mathf.Clamp01(depth);
    }

    // ==================================================
    // API: STOP / FADE
    // ==================================================

    public static void StopContinuous(
        ContinuousSfx sfx,
        float fadeDuration = 2f)
    {
        SoundState state = Find(sfx);

        if (state == null) return;

        state.stopping = true;
        state.fadeStartVolume = state.volume;
        state.fadeElapsed = 0f;
        state.fadeDuration = Mathf.Max(0.001f, fadeDuration);
        state.targetVolume = 0f;
    }

    // ==================================================
    // BUSQUEDA
    // ==================================================

    private static SoundState Find(ContinuousSfx sfx)
    {
        if (Instance == null || Instance._states == null)
            return null;

        foreach (SoundState state in Instance._states)
        {
            if (state != null &&
                state.playing &&
                state.sound == sfx)
                return state;
        }

        return null;
    }

    private static ToneState GetTone(
        ContinuousSfx sfx,
        int toneIndex)
    {
        SoundState state = Find(sfx);

        if (state == null ||
            state.tones == null ||
            toneIndex < 0 ||
            toneIndex >= state.tones.Length)
            return null;

        return state.tones[toneIndex];
    }








    // ==================================================
// GENERADOR DE GOTAS
// ==================================================

private static uint NextDropletRandom(SoundState state)
{
    uint x = state.dropletRandomState;

    x ^= x << 13;
    x ^= x >> 17;
    x ^= x << 5;

    state.dropletRandomState = x;

    return x;
}

private static float RandomDroplet(SoundState state)
{
    return NextDropletRandom(state) /
           (float)uint.MaxValue;
}

private static float GenerateDroplet( SoundState state,
    int sampleRate)
{
    if (!state.dropletsEnabled)
        return 0f;

    // Espera entre gotas
    state.dropletTimer -= 1f / 44100f;

    // --------------------------------------------------
    // INICIAR NUEVA GOTA
    // --------------------------------------------------

    if (state.dropletTimer <= 0f &&
        state.dropletEnvelope <= 0f)
    {
        state.dropletFrequency =
            Mathf.Lerp(1100f, 2200f, RandomDroplet(state));

        state.dropletDuration =
            Mathf.Lerp(0.07f, 0.16f, RandomDroplet(state));

        state.dropletElapsed = 0f;
        state.dropletPhase = 0.0;

        state.dropletEnvelope = 1f;

        // Tiempo irregular entre gotas
        state.dropletTimer =
            Mathf.Lerp(0.25f, 1.1f, RandomDroplet(state));
    }

    if (state.dropletEnvelope <= 0f)
        return 0f;

    // --------------------------------------------------
    // EVOLUCIÓN DE LA GOTA
    // --------------------------------------------------

    float t = state.dropletElapsed /
              Mathf.Max(0.001f, state.dropletDuration);

    t = Mathf.Clamp01(t);

    // La gota cae de tono ligeramente
    float frequency = Mathf.Lerp(
        state.dropletFrequency,
        state.dropletFrequency * 0.65f,
        t
    );

    state.dropletPhase +=
        TwoPi * frequency / 44100f;

    if (state.dropletPhase >= TwoPi)
        state.dropletPhase -= TwoPi;

    // Envolvente de gota: ataque rápido y caída
    float envelope = (1f - t) * (1f - t);

    // Resonancia de agua
    float drop = (float)Math.Sin(state.dropletPhase);

    // Pequeño segundo resonador
    float sparkle = (float)Math.Sin(
        state.dropletPhase * 2.73
    );

    drop = drop * 0.75f + sparkle * 0.25f;

    drop *= envelope;

    state.dropletElapsed += 1f / 44100f;

    if (state.dropletElapsed >= state.dropletDuration)
    {
        state.dropletEnvelope = 0f;
    }

    return drop * state.dropletVolume;
}



// ==================================================
// PIEDRA DESLIZANTE: DOS GOLPES EN BUCLE
// ==================================================

private static float GenerateStone(
    SoundState state,
    int sampleRate)
{
    if (!state.stoneEnabled)
        return 0f;

    // Esperar hasta el siguiente golpe
    if (state.stoneDuration <= 0f)
    {
        state.stoneTimer -= 1f / sampleRate;

        if (state.stoneTimer > 0f)
            return 0f;

        // Iniciar golpe
        state.stoneElapsed = 0f;
        state.stoneDuration = 0.12f;

        state.stoneFrequency =
            state.stoneBurstIndex == 0 ? 155f : 115f;

        state.stonePhase = 0.0;
        state.stoneNoiseFilter = 0f;
    }

    float t = state.stoneElapsed /
              state.stoneDuration;

    t = Mathf.Clamp01(t);

    // Envolvente seca: ataque rápido y caída
    float envelope =
        Mathf.Pow(1f - t, 2.5f);

    // Ruido para simular fricción de piedra
    uint x = state.stoneRandomState;

    x ^= x << 13;
    x ^= x >> 17;
    x ^= x << 5;

    state.stoneRandomState = x;

    float noise =
        (x / (float)uint.MaxValue) * 2f - 1f;

    // Filtrar el ruido para evitar un sonido demasiado áspero
    state.stoneNoiseFilter +=
        (noise - state.stoneNoiseFilter) * 0.12f;

    // Resonancia grave del golpe
    float frequency = Mathf.Lerp(
        state.stoneFrequency,
        state.stoneFrequency * 0.55f,
        t
    );

    state.stonePhase +=
        TwoPi * frequency / sampleRate;

    if (state.stonePhase >= TwoPi)
        state.stonePhase -= TwoPi;

    float impact =
        (float)Math.Sin(state.stonePhase);

    // Mezcla de golpe y rozamiento
    float sound =
        impact * 0.65f +
        state.stoneNoiseFilter * 0.55f;

    sound *= envelope;

    state.stoneElapsed += 1f / sampleRate;

    // Finalizar golpe y programar el siguiente
    if (state.stoneElapsed >= state.stoneDuration)
    {
        state.stoneDuration = 0f;

        if (state.stoneBurstIndex == 0)
        {
            // Pausa corta antes del segundo golpe
            state.stoneBurstIndex = 1;
            state.stoneTimer = 0.16f;
        }
        else
        {
            // Pausa larga antes de repetir el patrón
            state.stoneBurstIndex = 0;
            state.stoneTimer = 0.85f;
        }
    }

    return sound * state.stoneVolume;
}






// ==================================================
// FUEGO: LLAMA SUAVE + CHISPAS
// ==================================================
private static float GenerateFire(SoundState state, int sampleRate)
{
    if (!state.fireEnabled)
        return 0f;

    // ==================================================
    // INICIALIZACIÓN
    // ==================================================

    if (!state.fireInitialized)
    {
        // Capas de ruido más cálidas y menos brillantes.
        state.fireAlphaRumble = LowPassAlpha(120f, sampleRate);
        state.fireAlphaBody   = LowPassAlpha(650f, sampleRate);
        state.fireAlphaHiss   = LowPassAlpha(1400f, sampleRate);
        state.fireAlphaHiss2  = LowPassAlpha(2600f, sampleRate);

        state.fireGainRumble = LowPassGain(state.fireAlphaRumble);
        state.fireGainBody   = LowPassGain(state.fireAlphaBody);

        state.fireLfo1 = FireRandom(state) * TwoPi;
        state.fireLfo2 = FireRandom(state) * TwoPi;
        state.fireLfo3 = FireRandom(state) * TwoPi;

        state.fireInitialized = true;
    }

    float dt = 1f / sampleRate;

    // ==================================================
    // LLAMA: VARIACIÓN ORGÁNICA Y SUAVE
    // ==================================================

    state.fireLfo1 += TwoPi * 0.23 / sampleRate;
    state.fireLfo2 += TwoPi * 0.57 / sampleRate;
    state.fireLfo3 += TwoPi * 1.13 / sampleRate;

    if (state.fireLfo1 >= TwoPi) state.fireLfo1 -= TwoPi;
    if (state.fireLfo2 >= TwoPi) state.fireLfo2 -= TwoPi;
    if (state.fireLfo3 >= TwoPi) state.fireLfo3 -= TwoPi;

    float flicker =
        0.78f +
        0.13f * (float)Math.Sin(state.fireLfo1) +
        0.07f * (float)Math.Sin(state.fireLfo2) +
        0.02f * (float)Math.Sin(state.fireLfo3);

    flicker = Mathf.Clamp(flicker, 0.50f, 1f);

    // ==================================================
    // CAPA 1: RUMOR GRAVE
    // ==================================================

    state.fireRumbleLp +=
        state.fireAlphaRumble *
        (FireNoise(state) - state.fireRumbleLp);

    float rumble =
        state.fireRumbleLp * state.fireGainRumble;

    // ==================================================
    // CAPA 2: CUERPO CÁLIDO
    // ==================================================

    state.fireBodyLp +=
        state.fireAlphaBody *
        (FireNoise(state) - state.fireBodyLp);

    float body =
        state.fireBodyLp * state.fireGainBody;

    // ==================================================
    // CAPA 3: AIRE SUAVE
    // ==================================================

    float hissNoise = FireNoise(state);

    state.fireHissLp +=
        state.fireAlphaHiss *
        (hissNoise - state.fireHissLp);

    float hiss = hissNoise - state.fireHissLp;

    state.fireHissLp2 +=
        state.fireAlphaHiss2 *
        (hiss - state.fireHissLp2);

    hiss = state.fireHissLp2;

    // ==================================================
    // MEZCLA DE LA LLAMA
    // ==================================================

    float intensity = Mathf.Clamp01(state.fireIntensity);

    // Predominan los graves y medios cálidos.
    // El aire está presente, pero es mucho más discreto.
    float bed =
        rumble * 0.42f +
        body   * 0.75f +
        hiss   * 0.12f;

    bed *= flicker;
    bed *= 0.65f + 0.35f * intensity;

    // ==================================================
    // PARTÍCULAS: CREPITACIONES Y BRASAS
    // ==================================================

    float spark = 0f;

    if (!state.sparkActive)
    {
        state.sparkTimer -= dt;

        if (state.sparkTimer <= 0f)
        {
            // La mayoría son pequeñas partículas.
            // Algunas son chasquidos más notorios.
            state.sparkIsPop = FireRandom(state) < 0.22f;

            float randomSize = FireRandom(state);
            randomSize *= randomSize;

            if (state.sparkIsPop)
            {
                // Chasquido cálido de una pequeña brasa.
                state.sparkDuration =
                    Mathf.Lerp(0.018f, 0.045f, FireRandom(state));

                state.sparkFrequency =
                    Mathf.Lerp(180f, 480f, FireRandom(state));

                state.sparkVolume =
                    Mathf.Lerp(0.12f, 0.38f, randomSize);
            }
            else
            {
                // Partícula diminuta, más suave y discreta.
                state.sparkDuration =
                    Mathf.Lerp(0.006f, 0.022f, FireRandom(state));

                state.sparkFrequency =
                    Mathf.Lerp(700f, 1800f, FireRandom(state));

                state.sparkVolume =
                    Mathf.Lerp(0.035f, 0.18f, randomSize);
            }

            state.sparkVolume *=
                0.35f + 0.65f * intensity;

            state.sparkElapsed = 0f;
            state.sparkPhase = 0.0;
            state.sparkNoiseLp = 0f;
            state.sparkActive = true;
        }
    }

    // ==================================================
    // REPRODUCCIÓN DE LA PARTÍCULA
    // ==================================================

    if (state.sparkActive)
    {
        float t = Mathf.Clamp01(
            state.sparkElapsed /
            Mathf.Max(0.001f, state.sparkDuration)
        );

        // Ataque suave para evitar clicks digitales.
        float attack = Mathf.Clamp01(
            state.sparkElapsed / 0.0025f
        );

        float decay = 1f - t;

        // Caída natural.
        float envelope =
            attack * decay * decay * decay;

        // Ruido filtrado para que las partículas
        // no sean un siseo blanco demasiado agresivo.
        float n = FireNoise(state);

        state.sparkNoiseLp +=
            0.18f * (n - state.sparkNoiseLp);

        float crackle = n - state.sparkNoiseLp;

        // Resonancia corta y cálida.
        float frequency = Mathf.Lerp(
            state.sparkFrequency,
            state.sparkFrequency * 0.48f,
            t
        );

        state.sparkPhase +=
            TwoPi * frequency / sampleRate;

        if (state.sparkPhase >= TwoPi)
            state.sparkPhase -= TwoPi;

        float tone = (float)Math.Sin(state.sparkPhase);

        float particle;

        if (state.sparkIsPop)
        {
            // Pop: resonancia más grave con poco ruido.
            particle =
                tone * 0.75f +
                crackle * 0.20f;
        }
        else
        {
            // Partícula: sonido pequeño y ligeramente brillante.
            particle =
                crackle * 0.45f +
                tone * 0.55f;
        }

        spark =
            particle *
            envelope *
            state.sparkVolume;

        state.sparkElapsed += dt;

        // ==================================================
        // PROGRAMAR SIGUIENTE PARTÍCULA
        // ==================================================

        if (state.sparkElapsed >= state.sparkDuration)
        {
            state.sparkActive = false;

            // Algunas pequeñas ráfagas de partículas,
            // intercaladas con pausas naturales.
            if (FireRandom(state) < 0.22f)
            {
                state.sparkTimer =
                    Mathf.Lerp(0.025f, 0.10f, FireRandom(state));
            }
            else
            {
                state.sparkTimer =
                    Mathf.Lerp(0.18f, 1.10f, FireRandom(state));
            }

            // A menor intensidad, menos partículas.
            state.sparkTimer /=
                Mathf.Max(0.35f, intensity);
        }
    }

    // ==================================================
    // RESULTADO
    // ==================================================

    // El crepitar se mantiene por debajo del cuerpo de la llama.
    return bed + spark * 0.65f;
}


// ==================================================
// RANDOM DEL FUEGO
// ==================================================

private static uint NextFireRandom(SoundState state)
{
    uint x = state.fireRandomState;

    x ^= x << 13;
    x ^= x >> 17;
    x ^= x << 5;

    state.fireRandomState = x;

    return x;
}

private static float FireRandom(SoundState state)
{
    return NextFireRandom(state) / (float)uint.MaxValue;
}

private static float FireNoise(SoundState state)
{
    return FireRandom(state) * 2f - 1f;
}

// ==================================================
// FILTROS
// ==================================================

private static float LowPassAlpha(
    float cutoff,
    int sampleRate)
{
    return 1f - (float)Math.Exp(
        -TwoPi * cutoff / sampleRate
    );
}

private static float LowPassGain(float alpha)
{
    return Mathf.Sqrt((2f - alpha) / alpha);
}
    // ==================================================
    // GENERACION DE AUDIO
    // ==================================================

    private void GenerateSound(float[] data, int index)
    {
        SoundState state = _states[index];

        if (state == null || !state.playing)
        {
            Array.Clear(data, 0, data.Length);
            return;
        }

        for (int i = 0; i < data.Length; i++)
        {
            // ------------------------------------------
            // Volumen global y fade
            // ------------------------------------------

            if (state.stopping)
            {
                state.fadeElapsed += 1f / _sampleRate;

                float t = Mathf.Clamp01(
                    state.fadeElapsed / state.fadeDuration
                );

                // Fade suave
                float fade = 1f - t * t * (3f - 2f * t);

                state.fadeMultiplier = fade;

                if (t >= 1f)
                {
                    state.volume = 0f;
                    state.playing = false;
                    state.stopping = false;
                }
            }
            else
            {
                state.volume = Mathf.MoveTowards(
                    state.volume,
                    state.targetVolume,
                    state.smoothing / _sampleRate
                );

                state.fadeMultiplier = 1f;
            }

            // ------------------------------------------
            // Mezcla de osciladores
            // ------------------------------------------

            float mixedSample = 0f;
            float totalWeight = 0f;

            for (int ti = 0; ti < state.tones.Length; ti++)
            {
                ToneState tone = state.tones[ti];

                // Glide
                if (tone.glideSpeed > 0f)
                {
                    tone.frequency = Mathf.MoveTowards(
                        tone.frequency,
                        tone.targetFrequency,
                        tone.glideSpeed / _sampleRate
                    );
                }
                else
                {
                    tone.frequency = tone.targetFrequency;
                }

                // Vibrato 1
                float vibrato1 = tone.vibratoDepth *
                    (float)Math.Sin(tone.vibratoPhase);

                tone.vibratoPhase +=
                    TwoPi * tone.vibratoRate / _sampleRate;

                if (tone.vibratoPhase >= TwoPi)
                    tone.vibratoPhase -= TwoPi;

                // Vibrato 2
                float vibrato2 = tone.vibrato2Depth *
                    (float)Math.Sin(tone.vibrato2Phase);

                tone.vibrato2Phase +=
                    TwoPi * tone.vibrato2Rate / _sampleRate;

                if (tone.vibrato2Phase >= TwoPi)
                    tone.vibrato2Phase -= TwoPi;

                // Frecuencia final
                float frequency = Mathf.Max(
                    1f,
                    tone.frequency + vibrato1 + vibrato2
                );

                // Oscilador
                tone.phase += TwoPi * frequency / _sampleRate;

                if (tone.phase >= TwoPi)
                    tone.phase -= TwoPi;

                // --------------------------------------
                // Armonicos propios
                // --------------------------------------

                float toneSample = 0f;
                float norm = 0f;

                for (int h = 0; h < tone.harmonics.Length; h++)
                {
                    float amp = tone.harmonics[h];

                    toneSample += amp *
                        (float)Math.Sin(tone.phase * (h + 1));

                    norm += Mathf.Abs(amp);
                }

                if (norm > 0f)
                    toneSample /= norm;

                // --------------------------------------
                // Tremolo propio
                // --------------------------------------

                float tremoloLfo = 0.5f + 0.5f *
                    (float)Math.Sin(tone.tremoloPhase);

                float tremolo = 1f -
                    tone.tremoloDepth * tremoloLfo;

                tone.tremoloPhase +=
                    TwoPi * tone.tremoloRate / _sampleRate;

                if (tone.tremoloPhase >= TwoPi)
                    tone.tremoloPhase -= TwoPi;

                // --------------------------------------
                // Multiply propio
                // --------------------------------------

                float multiplyLfo =
                    (float)Math.Sin(tone.multiplyPhase);

                float multiply = 1f -
                    tone.multiplyDepth * 0.5f *
                    (1f - multiplyLfo);

                tone.multiplyPhase +=
                    TwoPi * tone.multiplyRate / _sampleRate;

                if (tone.multiplyPhase >= TwoPi)
                    tone.multiplyPhase -= TwoPi;

                // --------------------------------------
                // Volumen individual y mezcla
                // --------------------------------------

                float outputTone =
                    toneSample *
                    tremolo *
                    multiply *
                    tone.volume;

                mixedSample += outputTone;
                totalWeight += tone.volume;
            }

            // Normalizar para que agregar osciladores
            // no dispare excesivamente el volumen.
            if (totalWeight > 0f)
                mixedSample /= totalWeight;

                // ------------------------------------------
            // GOTAS DE LLOVIZNA
            // ------------------------------------------

            if (state.dropletsEnabled)
            {
                mixedSample += GenerateDroplet(
                    state,
                    _sampleRate
                );
            }

            // ------------------------------------------
            // FUEGO  
            // ------------------------------------------

            if (state.fireEnabled)
            {
                mixedSample += GenerateFire(
                    state,
                    _sampleRate
                );
            }

            float output =
                mixedSample *
                state.volume *
                state.fadeMultiplier *
                masterVolume;

            data[i] = (float)Math.Tanh(output * 0.3f);
        }
    }

    // ==================================================
    // ACTUALIZACION
    // ==================================================

    private void Update()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            if (_states[i] != null &&
                !_states[i].playing &&
                _sources[i].isPlaying)
            {
                _sources[i].Stop();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}