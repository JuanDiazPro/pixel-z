using UnityEngine;
using UnityEngine.SceneManagement;

// Sonidos y música del juego. Los clips se cargan de Resources/Audio y se reproducen desde un objeto persistente.
public class Sfx : MonoBehaviour
{
    public enum Clip { Potion, LoseLife, Coin, Dodge, MenuSelect, PlayerHit, EnemySword, EnemyDeath, Breakable, GameOver }

    // Nombre del archivo en Resources/Audio, separación mínima entre repeticiones y variación de tono.
    // Con muchos enemigos a la vez el límite evita que los golpes y muertes se amontonen en un ruido.
    private static readonly (string file, float minInterval, float pitchJitter)[] Defs =
    {
        ("pocion", 0f, 0f),
        ("pierde_vida", 0f, 0f),
        ("moneda", 0.06f, 0.08f),
        ("esquivar", 0f, 0.05f),
        ("menu_seleccion", 0.03f, 0f),
        ("jugador_golpe", 0.05f, 0.08f),
        ("enemigo_espada", 0.09f, 0.1f),
        ("enemigo_muere", 0.07f, 0.1f),
        ("golpe_caja", 0.05f, 0.08f),
        ("game_over", 0f, 0f),
    };

    private const float MusicVolume = 0.45f;
    private const float MusicFadeSpeed = 0.6f;   // volumen por segundo

    private static Sfx instance;

    private AudioSource sfxSource;
    private AudioSource variedSource;   // aparte para poder variar el tono sin afectar a los demás sonidos
    private AudioSource musicSource;
    private AudioClip[] clips;
    private AudioClip hordeMusic;
    private float musicTarget;
    private float[] lastPlayed;

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
        variedSource = gameObject.AddComponent<AudioSource>();
        variedSource.playOnAwake = false;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0f;

        clips = new AudioClip[Defs.Length];
        lastPlayed = new float[Defs.Length];
        for (int i = 0; i < Defs.Length; i++)
        {
            clips[i] = Resources.Load<AudioClip>("Audio/" + Defs[i].file);
            lastPlayed[i] = -1f;
        }
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
        int i = (int)clip;
        AudioClip c = s.clips[i];
        if (c == null) return;

        var def = Defs[i];
        if (Time.unscaledTime - s.lastPlayed[i] < def.minInterval) return;
        s.lastPlayed[i] = Time.unscaledTime;
        if (def.pitchJitter > 0f)
        {
            s.variedSource.pitch = 1f + Random.Range(-def.pitchJitter, def.pitchJitter);
            s.variedSource.PlayOneShot(c, volume);
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
