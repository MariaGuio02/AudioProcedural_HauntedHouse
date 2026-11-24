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
    Wind
}

public class ContinuousProceduralSfx : MonoBehaviour
{
    private const double TwoPi = 2.0 * Math.PI;
    private const int PoolSize = 5;

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

// ==========================================
// NUEVO MOTOR DE FUEGO
// ==========================================

// Modulación compartida entre capas
public float fireSlowNoise;
public float fireFlutterNoise;
public float fireHissMod;

// Respiración / medios
public float fireBreathLp;
public float fireBreathHp;

// Crepitaciones agrupadas
public float fireCrackleRate;
public float fireClusterEnergy;
public float fireAftershockTimer;
public int fireAftershocks;

// Filtros de chasquidos
public float fireCrackleLp1;
public float fireCrackleLp2;
public float fireCrackleLp3;

// Pop resonante
public float firePopRing;
public float firePopRingLp;
public float firePopBody;
public double firePopPhase;

// Sizzle después de un pop
public float fireSizzleTimer;
public float fireSizzleEnvelope;


// ================================================================
// WIND: VARIACIÓN DE FRECUENCIA
// ================================================================

public bool windEnabled;

public float windChangeTimer;
public float windNextChange;

public float windBaseFrequency = 180f;
public float windTargetFrequency = 180f;

// ================================================================
// WIND: PEQUEÑOS CHOQUES DE AIRE
// ================================================================

public float windImpactTimer;
public float windImpactNextTime;

public bool windImpactActive;
public float windImpactElapsed;
public float windImpactDuration;

public float windImpactVolume;
public float windImpactFrequency;

public double windImpactPhase;

public float windImpactFilter;
public uint windImpactRandomState = 192837465;

// Permite ráfagas de dos o tres choques cercanos.
public int windBurstRemaining;

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
          
            case ContinuousSfx.Wind:
            {
                // Capa principal: viento grave y suave
                ToneState wind = NewTone(180f, 180f, 1f);

                wind.glideSpeed = 0.8f;

                Vibrato(wind, 0.12f, 2.5f);
                Tremolo(wind, 0.08f, 0.12f);

                Harmonics(wind,
                    1f,
                    0.12f,
                    0.04f
                );

                // Segunda capa: aire más agudo
                ToneState windLayer = NewTone(320f, 320f, 0.45f);

                windLayer.glideSpeed = 0.55f;

                Vibrato(windLayer, 0.08f, 4f);
                Tremolo(windLayer, 0.12f, 0.18f);

                Harmonics(windLayer,
                    1f,
                    0.08f,
                    0.03f
                );

                state.tones = new ToneState[]
                {
                    wind,
                    windLayer
                };

                state.targetVolume = 0.65f;
                state.smoothing = 0.8f;

                // Variación de frecuencia
                state.windEnabled = true;
                state.windChangeTimer = 0f;
                state.windNextChange = 2f;

                state.windBaseFrequency = 180f;
                state.windTargetFrequency = 180f;

                // Choques de aire
                state.windImpactTimer = 0f;
                state.windImpactNextTime = 3f;
                state.windImpactActive = false;
                state.windImpactVolume = 0f;
                state.windImpactRandomState = 192837465;

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




private void GenerateWind(SoundState state, float sampleRate)
{
    if (!state.windEnabled || state.tones == null)
        return;

    state.windChangeTimer += 1f / sampleRate;

    if (state.windChangeTimer >= state.windNextChange)
    {
        state.windChangeTimer = 0f;

        state.windNextChange = RandomWind(state, 2f, 5f);

        state.windTargetFrequency = RandomWind(
            state,
            130f,
            240f
        );

        state.windBaseFrequency = state.windTargetFrequency;
    }

    if (state.tones.Length > 0)
    {
        state.tones[0].targetFrequency =
            state.windTargetFrequency;

        state.tones[0].frequency = Mathf.Lerp(
            state.tones[0].frequency,
            state.windTargetFrequency,
            1f - Mathf.Exp(-0.8f / sampleRate)
        );
    }

    if (state.tones.Length > 1)
    {
        float targetLayer = state.windTargetFrequency * 1.8f;

        state.tones[1].targetFrequency = targetLayer;

        state.tones[1].frequency = Mathf.Lerp(
            state.tones[1].frequency,
            targetLayer,
            1f - Mathf.Exp(-0.6f / sampleRate)
        );
    }
}

private float GenerateWindImpact(
    SoundState state,
    float sampleRate)
{
    if (!state.windEnabled)
        return 0f;

    float dt = 1f / sampleRate;

    // ------------------------------------------------------------
    // PROGRAMACIÓN DE CHOQUES
    // ------------------------------------------------------------

    if (!state.windImpactActive)
    {
        state.windImpactTimer += dt;

        if (state.windImpactTimer >= state.windImpactNextTime)
        {
            state.windImpactTimer = 0f;

            state.windImpactActive = true;
            state.windImpactElapsed = 0f;

            state.windImpactDuration = RandomWind(
                state,
                0.12f,
                0.32f
            );

            state.windImpactFrequency = RandomWind(
                state,
                500f,
                1400f
            );

            state.windImpactVolume = RandomWind(
                state,
                0.025f,
                0.065f
            );

            // Algunas veces se producen pequeños grupos
            // de dos o tres choques.
            if (state.windBurstRemaining == 0 &&
                RandomWind(state, 0f, 1f) > 0.72f)
            {
                state.windBurstRemaining =
                    RandomWind(state, 0f, 1f) > 0.5f ? 2 : 1;
            }
        }
    }

    // ------------------------------------------------------------
    // GENERACIÓN DEL CHOQUE
    // ------------------------------------------------------------

    if (!state.windImpactActive)
        return 0f;

    state.windImpactElapsed += dt;

    float progress = Mathf.Clamp01(
        state.windImpactElapsed / state.windImpactDuration
    );

    // Ataque corto y caída progresiva.
    float envelope = Mathf.Sin(progress * Mathf.PI);

    envelope = Mathf.Pow(envelope, 1.4f);

    // Ruido aleatorio.
    float noise = RandomWind(state, -1f, 1f);

    // Filtro pasa-bajos para suavizar el ruido.
    float alpha = LowPassAlpha(
        state.windImpactFrequency,
        (int)sampleRate
    );

    state.windImpactFilter +=
        alpha * (noise - state.windImpactFilter);

    float impact = state.windImpactFilter *
                   envelope *
                   state.windImpactVolume;

    // ------------------------------------------------------------
    // FINALIZACIÓN Y SIGUIENTE CHOQUE
    // ------------------------------------------------------------

    if (state.windImpactElapsed >= state.windImpactDuration)
    {
        state.windImpactActive = false;
        state.windImpactElapsed = 0f;

        if (state.windBurstRemaining > 0)
        {
            state.windBurstRemaining--;

            state.windImpactNextTime = RandomWind(
                state,
                0.08f,
                0.22f
            );
        }
        else
        {
            state.windImpactNextTime = RandomWind(
                state,
                2f,
                6f
            );
        }
    }

    return impact;
}

private static uint NextWindRandom(SoundState state)
{
    uint x = state.windImpactRandomState;

    x ^= x << 13;
    x ^= x >> 17;
    x ^= x << 5;

    state.windImpactRandomState = x;

    return x;
}

private static float RandomWind(
    SoundState state,
    float min,
    float max)
{
    float random = NextWindRandom(state) /
                   (float)uint.MaxValue;

    return Mathf.Lerp(min, max, random);
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

            if (state.windEnabled)
            {
                GenerateWind(state, _sampleRate);

                mixedSample += GenerateWindImpact(
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