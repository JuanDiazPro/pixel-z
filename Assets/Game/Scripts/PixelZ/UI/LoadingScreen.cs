using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pantalla "CARGANDO" con barra de progreso. Espera unos segundos mínimos aunque la escena cargue antes.
public class LoadingScreen : MonoBehaviour
{
    public static bool IsLoading { get; private set; }

    private static readonly string[] Tips =
    {
        "Consejo: mantén pulsado el click izquierdo para atacar sin parar.",
        "Consejo: el dash (Espacio) te hace invulnerable un instante.",
        "Consejo: las gemas valen 5 monedas cada una.",
        "Consejo: compra el cofre del centro para conseguir la llave del castillo.",
        "Consejo: entre rondas pulsa B para abrir la tienda.",
        "Consejo: cada 5 rondas aparece un jefe. ¡Prepárate!",
        "Consejo: si te queda un corazón, los enemigos sueltan vidas con más frecuencia.",
        "Consejo: la poción roja te da velocidad durante unos segundos.",
    };

    private Image bar;
    private Text percent;
    private CanvasGroup group;

    public static void Load(string sceneName, float minSeconds = 3f)
    {
        if (IsLoading) return;
        IsLoading = true;

        Canvas canvas = UIFactory.CreateCanvas("Loading Screen", 2000);
        DontDestroyOnLoad(canvas.gameObject);
        var screen = canvas.gameObject.AddComponent<LoadingScreen>();
        screen.Build(canvas.transform);
        screen.StartCoroutine(screen.Run(sceneName, minSeconds));
    }

    private void Build(Transform canvas)
    {
        PixelZDatabase db = PixelZDatabase.I;
        RectTransform root = UIFactory.Stretch(UIFactory.Rect("Root", canvas));
        root.gameObject.AddComponent<RectMask2D>();
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Image block = UIFactory.Image(root, "Black", db.white, Color.black);
        UIFactory.Stretch(block.rectTransform);
        block.raycastTarget = true;
        UIFactory.FullscreenImage(root, db.loadingBackground, Color.white);

        Image barBg = UIFactory.Panel(root, "Bar BG", new Color(0.05f, 0.04f, 0.06f, 0.9f));
        UIFactory.Place(barBg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1100f, 36f));
        bar = UIFactory.Image(barBg.transform, "Fill", db.white, UIFactory.Gold);
        UIFactory.Stretch(bar.rectTransform, 5f);
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillAmount = 0f;

        percent = UIFactory.Text(barBg.transform, "0%", 26, Color.white);
        UIFactory.Stretch(percent.rectTransform);

        Text tip = UIFactory.Text(root, Tips[Random.Range(0, Tips.Length)], 28, UIFactory.Cream);
        UIFactory.Place(tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1500f, 40f));
    }

    private IEnumerator Run(string sceneName, float minSeconds)
    {
        yield return FadeTo(1f, 0.3f);

        Time.timeScale = 1f;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        float t = 0f;
        float shown = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float target = Mathf.Min(real, t / minSeconds);
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.5f);
            bar.fillAmount = shown;
            percent.text = Mathf.RoundToInt(shown * 100f) + "%";
            if (shown >= 0.999f && real >= 1f) break;
            yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;

        IsLoading = false;
        yield return FadeTo(0f, 0.4f);
        Destroy(gameObject);
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float start = group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }
        group.alpha = target;
    }
}
