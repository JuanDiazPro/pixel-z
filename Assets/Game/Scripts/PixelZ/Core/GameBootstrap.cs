using UnityEngine;
using UnityEngine.SceneManagement;

// Arranca el GameManager automáticamente al cargar las escenas de juego (Game / Castle),
// tanto si se viene del menú como si se pulsa Play directamente en la escena.
public static class GameBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        GameSession.RunActive = false;
        Enemy.All.Clear();
        Pickup.All.Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnFirstSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameManager.SetupScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) GameManager.SetupScene(scene);
    }
}
