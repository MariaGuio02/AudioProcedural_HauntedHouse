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
    [Range(0f, 1f)] public float musicVolume = 0.2f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Min(0.05f)] public float fadeSeconds = 1.5f;

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
    Track current = Track.None;
    float menuFade, gameFade;

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
        Apply(menuMusic, menuFade, "MenuMusic");
        Apply(gameMusic, gameFade, "GameMusic");
    }

    void Apply(MusicSynth m, float fade, string label)
    {
        if (m == null) return;
        m.Source.volume = fade * musicVolume;
        if (fade > 0f)
        {
            if (!m.IsOn)
            m.Begin();
        }
        else
        {
            if (m.IsOn)
            m.End(); 
        }
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
