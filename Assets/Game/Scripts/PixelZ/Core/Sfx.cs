using UnityEngine;
using UnityEngine.SceneManagement;

// Sonidos y música del juego. Los clips se cargan de Resources/Audio y se reproducen desde un objeto persistente.
public class Sfx : MonoBehaviour
{
    public enum Clip { Potion, LoseLife, Coin, Dodge, MenuSelect }

    private const float MusicVolume = 0.45f;
    private const float MusicFadeSpeed = 0.6f;   // volumen por segundo
    private const float CoinMinInterval = 0.06f; // al recoger un montón de monedas a la vez no se amontona el sonido

    private static Sfx instance;

    private AudioSource sfxSource;
    private AudioSource coinSource;   // aparte para poder variar el tono sin afectar a los demás sonidos
    private AudioSource musicSource;
    private AudioClip[] clips;
    private AudioClip hordeMusic;
    private float musicTarget;
    private float lastCoinTime = -1f;

    private static Sfx I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sfx>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        coinSource = gameObject.AddComponent<AudioSource>();
        coinSource.playOnAwake = false;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0f;

        clips = new[]
        {
            Resources.Load<AudioClip>("Audio/pocion"),
            Resources.Load<AudioClip>("Audio/pierde_vida"),
            Resources.Load<AudioClip>("Audio/moneda"),
            Resources.Load<AudioClip>("Audio/esquivar"),
            Resources.Load<AudioClip>("Audio/menu_seleccion"),
        };
        hordeMusic = Resources.Load<AudioClip>("Audio/musica_horda");

        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureListener();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    // Al cambiar de escena la horda termina: la música se corta y se comprueba que haya quien escuche.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        musicTarget = 0f;
        musicSource.Stop();
        musicSource.volume = 0f;
        EnsureListener();
    }

    private void EnsureListener()
    {
        AudioListener own = GetComponent<AudioListener>();
        bool other = false;
        foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (l != own) other = true;
        if (!other && own == null) gameObject.AddComponent<AudioListener>();
        else if (other && own != null) Destroy(own);
    }

    private void Update()
    {
        if (musicSource.clip == null) return;
        musicSource.volume = Mathf.MoveTowards(musicSource.volume, musicTarget, MusicFadeSpeed * Time.unscaledDeltaTime);
        if (musicTarget <= 0f && musicSource.volume <= 0f && musicSource.isPlaying) musicSource.Stop();
    }

    public static void Play(Clip clip, float volume = 1f)
    {
        Sfx s = I;
        AudioClip c = s.clips[(int)clip];
        if (c == null) return;

        if (clip == Clip.Coin)
        {
            if (Time.unscaledTime - s.lastCoinTime < CoinMinInterval) return;
            s.lastCoinTime = Time.unscaledTime;
            s.coinSource.pitch = Random.Range(0.95f, 1.1f);
            s.coinSource.PlayOneShot(c, volume);
            return;
        }
        s.sfxSource.PlayOneShot(c, volume);
    }

    public static void StartHordeMusic()
    {
        Sfx s = I;
        if (s.hordeMusic == null) return;
        s.musicSource.clip = s.hordeMusic;
        s.musicTarget = MusicVolume;
        if (!s.musicSource.isPlaying)
        {
            s.musicSource.volume = 0f;
            s.musicSource.Play();
        }
    }

    public static void StopHordeMusic()
    {
        if (instance != null) instance.musicTarget = 0f;
    }
}
