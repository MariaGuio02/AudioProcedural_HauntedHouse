using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    enum Track { None, Menu, Game }

    [Header("Escenas")]
    public string menuSceneName = "MainMenu";
    public string gameSceneName = "DemoScene";

    [Header("Volumen")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Min(0.05f)] public float fadeSeconds = 1.5f;

    [Header("Efectos de sonido (generados por codigo)")]
    [Range(0f, 1f)] public float startVol = 1f;
    [Range(0f, 1f)] public float exitVol = 1f;
    [Range(0f, 1f)] public float winVol = 1f;
    [Range(0f, 1f)] public float loseVol = 1f;
    [Tooltip("Baja la musica mientras suena ganar/perder")]
    [Range(0f, 1f)] public float musicDuckLevel = 0.1f;

    [Header("Wavetables")]
    public bool useTxtWaveTables = false;
    public TextAsset sineTxt;
    public TextAsset triangleTxt;
    public TextAsset squareTxt;
    public TextAsset sawTxt;

    [Header("Musica")]
    public MenuMusic menuMusic;
    public GameMusic gameMusic;

    AudioSource sfxSource;
    AudioClip startClip, exitClip, winClip, loseClip;
    Track current = Track.None;
    float menuFade, gameFade;
    float duck = 1f, duckTarget = 1f, duckUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null) new GameObject("AudioManager").AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { gameObject.SetActive(false); Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (menuMusic == null) menuMusic = CreateChild<MenuMusic>("MenuMusic");
        if (gameMusic == null) gameMusic = CreateChild<GameMusic>("GameMusic");
        ApplyWaveTables();

        GameObject sfx = new GameObject("SFX");
        sfx.transform.SetParent(transform, false);
        sfxSource = sfx.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false; sfxSource.spatialBlend = 0f;

        // Efectos procedurales: se renderizan una sola vez
        startClip = SfxSynth.StartButton();
        exitClip = SfxSynth.ExitButton();
        winClip = SfxSynth.Win();
        loseClip = SfxSynth.Lose();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start() { SelectForScene(SceneManager.GetActiveScene().name); }

    void OnDestroy() { if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded; }

    T CreateChild<T>(string n) where T : MusicSynth
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        return go.AddComponent<T>();
    }

    public void ApplyWaveTables()
    {
        if (menuMusic != null) menuMusic.SetWaveTables(sineTxt, triangleTxt, squareTxt, sawTxt, useTxtWaveTables);
        if (gameMusic != null) gameMusic.SetWaveTables(sineTxt, triangleTxt, squareTxt, sawTxt, useTxtWaveTables);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { SelectForScene(scene.name); }

    static bool Is(string a, string b) { return string.Equals((a ?? "").Trim(), (b ?? "").Trim(), System.StringComparison.OrdinalIgnoreCase); }
    static bool Has(string a, string part) { return (a ?? "").ToLowerInvariant().Contains(part); }

    void SelectForScene(string sceneName)
    {
        if (Is(sceneName, gameSceneName) || Has(sceneName, "demo")) current = Track.Game;
        else if (Is(sceneName, menuSceneName) || Has(sceneName, "menu")) current = Track.Menu;
        else current = Track.None;
    }

    void Update()
    {
        float step = Time.unscaledDeltaTime / fadeSeconds;
        menuFade = Mathf.MoveTowards(menuFade, current == Track.Menu ? 1f : 0f, step);
        gameFade = Mathf.MoveTowards(gameFade, current == Track.Game ? 1f : 0f, step);

        // Ducking: la musica baja mientras suena un efecto importante y luego vuelve
        if (Time.unscaledTime > duckUntil) duckTarget = 1f;
        duck = Mathf.MoveTowards(duck, duckTarget, Time.unscaledDeltaTime / 0.35f);

        Apply(menuMusic, menuFade);
        Apply(gameMusic, gameFade);
    }

    void Apply(MusicSynth m, float fade)
    {
        if (m == null) return;
        m.Source.volume = fade * musicVolume * duck;
        if (fade > 0f) { if (!m.IsOn) m.Begin(); }
        else { if (m.IsOn) m.End(); }
    }

    // ---- Efectos ----
    public void DuckMusic(float seconds) { duckTarget = musicDuckLevel; duckUntil = Time.unscaledTime + seconds; }

    public void PlayStart() { PlaySfx(startClip, startVol); }
    public void PlayExit() { PlaySfx(exitClip, exitVol); }

    public void PlayWin()
    {
        PlaySfx(winClip, winVol);
        DuckMusic(winClip.length * 0.8f);
    }

    public void PlayLose()
    {
        PlaySfx(loseClip, loseVol);
        DuckMusic(loseClip.length * 0.8f);
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume * sfxVolume);
    }

    public void PlaySfxAt(AudioClip clip, Vector3 pos, float volume = 1f)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, pos, volume * sfxVolume);
    }
}