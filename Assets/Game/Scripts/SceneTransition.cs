using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Cambia de escena con un fundido a negro y recuerda en qué SpawnPoint debe aparecer el jugador.
public class SceneTransition : MonoBehaviour
{
    public static string PendingSpawnId { get; private set; }
    public static bool IsBusy { get; private set; }

    private const float FadeTime = 0.35f;

    private static SceneTransition instance;
    private Image fadeImage;

    public static void Go(string sceneName, string spawnId)
    {
        if (IsBusy) return;
        if (instance == null) instance = Create();
        instance.StartCoroutine(instance.Run(sceneName, spawnId));
    }

    private static SceneTransition Create()
    {
        var go = new GameObject("SceneTransition");
        DontDestroyOnLoad(go);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var imageGo = new GameObject("Fade");
        imageGo.transform.SetParent(go.transform, false);
        var rect = imageGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var t = go.AddComponent<SceneTransition>();
        t.fadeImage = imageGo.AddComponent<Image>();
        t.fadeImage.color = new Color(0f, 0f, 0f, 0f);
        t.fadeImage.raycastTarget = false;
        return t;
    }

    private IEnumerator Run(string sceneName, string spawnId)
    {
        IsBusy = true;
        yield return Fade(0f, 1f);

        PendingSpawnId = spawnId;
        yield return SceneManager.LoadSceneAsync(sceneName);
        PendingSpawnId = null;

        yield return Fade(1f, 0f);
        IsBusy = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / FadeTime));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }
}
