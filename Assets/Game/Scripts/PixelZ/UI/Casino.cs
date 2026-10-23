using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Casino de la tienda: sala para elegir juego (Ruleta o Blackjack). Se apuesta con las monedas de la partida.
// Vive dentro de la tienda, así que el juego sigue en pausa mientras está abierto.
public class Casino : MonoBehaviour
{
    public static readonly Color Felt = new Color(0.07f, 0.4f, 0.2f, 1f);
    public static readonly Color RedPocket = new Color(0.8f, 0.12f, 0.12f, 1f);
    public static readonly Color BlackPocket = new Color(0.12f, 0.11f, 0.13f, 1f);
    public static readonly Color GreenPocket = new Color(0.08f, 0.6f, 0.28f, 1f);
    public static readonly Color Wood = new Color(0.55f, 0.32f, 0.14f, 1f);
    public static readonly Color GoldTrim = new Color(1f, 0.8f, 0.3f, 1f);

    // Mientras la ruleta gira el saldo mostrado no debe revelar el resultado.
    public static int? HeldCoins;

    private GameObject lobby;
    private Roulette roulette;
    private Blackjack blackjack;
    private Text coinsText, titleText, noticeText;
    private float noticeTime;
    private static Casino current;

    public System.Action onClosed;

    public bool IsOpen => gameObject.activeSelf;
    private bool Busy => (roulette.gameObject.activeSelf && roulette.Busy) || (blackjack.gameObject.activeSelf && blackjack.Busy);

    public static Casino Create(Transform parent)
    {
        PixelZDatabase db = PixelZDatabase.I;
        RectTransform root = UIFactory.Stretch(UIFactory.Rect("Casino", parent));
        Image shade = UIFactory.Image(root, "Shade", db.white, new Color(0f, 0f, 0f, 0.85f));
        UIFactory.Stretch(shade.rectTransform);
        shade.raycastTarget = true;

        var casino = root.gameObject.AddComponent<Casino>();
        current = casino;
        casino.Build(root, db);
        root.gameObject.SetActive(false);
        return casino;
    }

    private void Build(RectTransform root, PixelZDatabase db)
    {
        Image panel = UIFactory.Panel(root, "Panel", new Color(0.07f, 0.05f, 0.06f, 0.98f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1840f, 1040f));
        panel.gameObject.AddComponent<FitInParent>();
        Transform p = panel.transform;

        // Fondo de moqueta de casino en pixel art.
        var carpet = new PixelCanvas(230, 130);
        for (int y = 0; y < carpet.H; y++)
            for (int x = 0; x < carpet.W; x++)
            {
                bool diamond = ((x + y) % 12 == 0) || ((x - y + 1200) % 12 == 0);
                Color c = diamond ? new Color(0.22f, 0.07f, 0.09f) : new Color(0.13f, 0.04f, 0.06f);
                carpet.Set(x, y, PixelCanvas.Shade(c, 0.9f + 0.2f * PixelCanvas.Noise(x, y), x, y));
            }
        carpet.Apply();
        RawImage bg = carpet.Show(p, "Carpet", 8);
        UIFactory.Stretch(bg.rectTransform, 4f);

        titleText = UIFactory.Text(p, "CASINO", 60, UIFactory.Gold);
        UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1000f, 76f));

        Image coinBox = UIFactory.Panel(p, "Saldo", new Color(0.1f, 0.07f, 0.05f, 0.95f));
        UIFactory.Place(coinBox.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(300f, 72f));
        Image coin = UIFactory.Image(coinBox.transform, "Coin", db.coin, Color.white);
        UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(48f, 48f));
        coinsText = UIFactory.Text(coinBox.transform, "0", 42, UIFactory.Gold, TextAnchor.MiddleLeft);
        UIFactory.Place(coinsText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(220f, 56f));

        noticeText = UIFactory.Text(p, "", 30, UIFactory.Cream);
        UIFactory.Place(noticeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(1200f, 40f));

        Button back = UIFactory.Button(p, "VOLVER", 32, Back, new Vector2(240f, 70f));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(240f, 70f));

        BuildLobby(p);

        RectTransform rScreen = UIFactory.Stretch(UIFactory.Rect("Roulette", p));
        roulette = rScreen.gameObject.AddComponent<Roulette>();
        roulette.Build(rScreen);

        RectTransform bScreen = UIFactory.Stretch(UIFactory.Rect("Blackjack", p));
        blackjack = bScreen.gameObject.AddComponent<Blackjack>();
        blackjack.Build(bScreen);
    }

    private void BuildLobby(Transform p)
    {
        RectTransform screen = UIFactory.Stretch(UIFactory.Rect("Lobby", p));
        lobby = screen.gameObject;

        Text sub = UIFactory.Text(screen, "Elige a qué quieres jugar. Apuestas las monedas que ganaste en la partida.", 30, UIFactory.Cream);
        UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1500f, 50f));

        // Miniaturas en pixel art de cada juego.
        var wheel = new PixelCanvas(150, 96);
        new WheelRenderer(wheel, 75f, 48f, 68f).Render(20f, 20f + WheelRenderer.Seg * 7f, 0.68f, 0f, true, false);
        var cards = new PixelCanvas(150, 96);
        cards.Clear(new Color32(0, 0, 0, 0));
        CardArt.Paint(cards, 0 * 13 + 0, 34, 14, false);
        CardArt.Paint(cards, 1 * 13 + 12, 72, 22, false);
        cards.Apply();

        GameCard(screen, new Vector2(-330f, -40f), "RULETA", "Rojo o negro, par o impar, docenas, columnas,\nplenos, caballos, calles y esquinas.\nPaga hasta 35 a 1.", wheel, ShowRoulette);
        GameCard(screen, new Vector2(330f, -40f), "BLACKJACK", "Llega a 21 sin pasarte y gana al crupier.\nBlackjack paga 3 a 2.\nPuedes doblar y dividir.", cards, ShowBlackjack);
    }

    private static void GameCard(Transform parent, Vector2 pos, string title, string desc, PixelCanvas art, System.Action onClick)
    {
        Image card = UIFactory.Panel(parent, title, new Color(0.12f, 0.08f, 0.08f, 0.97f));
        UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(600f, 640f));

        Image frame = UIFactory.Image(card.transform, "Frame", null, new Color(0.04f, 0.2f, 0.1f));
        UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(556f, 300f));
        RawImage img = art.Show(frame.transform, "Art", 3);
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(art.W * 3, art.H * 3));

        Text t = UIFactory.Text(card.transform, title, 60, UIFactory.Gold);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(560f, 70f));
        Text d = UIFactory.Text(card.transform, desc, 25, UIFactory.Cream);
        d.lineSpacing = 1.15f;
        UIFactory.Place(d.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -405f), new Vector2(540f, 100f));

        Button play = UIFactory.Button(card.transform, "JUGAR", 44, onClick, new Vector2(360f, 90f));
        UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(360f, 90f));
    }

    public void Open()
    {
        current = this;
        gameObject.SetActive(true);
        ShowLobby();
    }

    public void Close()
    {
        bool wasOpen = gameObject.activeSelf;
        gameObject.SetActive(false);
        if (wasOpen) onClosed?.Invoke();
    }

    // Esc / VOLVER: del juego a la sala, de la sala a la tienda. No se sale a mitad de una tirada.
    public void Back()
    {
        if (Busy) return;
        if (lobby.activeSelf) Close();
        else ShowLobby();
    }

    private void ShowLobby() => Show(lobby, "CASINO");
    private void ShowRoulette() => Show(roulette.gameObject, "RULETA");
    private void ShowBlackjack() => Show(blackjack.gameObject, "BLACKJACK");

    private void Show(GameObject screen, string title)
    {
        lobby.SetActive(screen == lobby);
        roulette.gameObject.SetActive(screen == roulette.gameObject);
        blackjack.gameObject.SetActive(screen == blackjack.gameObject);
        titleText.text = title;
    }

    private void Update()
    {
        coinsText.text = (HeldCoins ?? GameSession.Coins).ToString();
        noticeTime -= Time.unscaledDeltaTime;
        Color c = noticeText.color;
        c.a = Mathf.Clamp01(noticeTime * 2f);
        noticeText.color = c;
    }

    // Aviso corto dentro del casino (los avisos del HUD quedan tapados por la tienda).
    public static void Toast(string msg, Color color)
    {
        if (current == null) return;
        current.noticeText.text = msg;
        current.noticeText.color = color;
        current.noticeTime = 2.5f;
    }

    public static readonly Color Warning = new Color(1f, 0.6f, 0.5f);

    // Montón de monedas que marca una apuesta: más monedas cuanto mayor es la cantidad, con la cifra encima.
    public static RectTransform CoinStack(Transform parent, int amount, float size)
    {
        RectTransform root = UIFactory.Rect("Coins " + amount, parent);
        root.sizeDelta = new Vector2(size, size);
        int coins = Mathf.Clamp(1 + Mathf.FloorToInt(Mathf.Log10(Mathf.Max(1, amount)) * 2f), 1, 6);
        for (int i = 0; i < coins; i++)
        {
            Image shadow = UIFactory.Image(root, "Shadow", PixelZDatabase.I.coin, new Color(0f, 0f, 0f, 0.45f));
            UIFactory.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(3f, -3f + i * size * 0.14f), Vector2.one * size);
            Image c = UIFactory.Image(root, "Coin", PixelZDatabase.I.coin, Color.Lerp(new Color(0.75f, 0.7f, 0.6f), Color.white, (i + 1f) / coins));
            UIFactory.Place(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, i * size * 0.14f), Vector2.one * size);
        }
        Text label = UIFactory.Text(root, amount.ToString(), Mathf.RoundToInt(size * 0.55f), Color.white);
        UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, coins * size * 0.14f + size * 0.05f), new Vector2(size * 3f, size));
        return root;
    }
}

// Campo para escribir cuántas monedas se apuestan, con botones rápidos.
public class CoinInput
{
    private static readonly List<CoinInput> all = new List<CoinInput>();

    public int Value { get; private set; } = 10;
    private InputField field;

    // Mientras se escribe la cantidad, las teclas del juego (B, Esc, Enter...) no deben actuar.
    public static bool AnyFocused
    {
        get
        {
            all.RemoveAll(c => c.field == null);
            foreach (CoinInput c in all)
                if (c.field.isFocused) return true;
            return false;
        }
    }

    public static CoinInput Create(Transform parent, Vector2 pos)
    {
        var ci = new CoinInput();
        all.Add(ci);
        RectTransform row = UIFactory.Rect("Coin Input", parent);
        UIFactory.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(860f, 80f));

        Text label = UIFactory.Text(row, "APUESTA:", 30, UIFactory.Cream, TextAnchor.MiddleRight);
        UIFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(150f, 60f));

        Small(row, "-", 160f, 64f, () => ci.Set(ci.Value - ci.Step(false)));

        Image box = UIFactory.Panel(row, "Field", new Color(0.05f, 0.04f, 0.03f, 1f));
        UIFactory.Place(box.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(234f, 0f), new Vector2(200f, 72f));
        Image coin = UIFactory.Image(box.transform, "Coin", PixelZDatabase.I.coin, Color.white);
        UIFactory.Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(40f, 40f));
        Text text = UIFactory.Text(box.transform, "10", 38, UIFactory.Gold, TextAnchor.MiddleCenter, false);
        UIFactory.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(52f, 0f), new Vector2(140f, 64f));
        text.supportRichText = false;
        ci.field = box.gameObject.AddComponent<InputField>();
        ci.field.textComponent = text;
        ci.field.contentType = InputField.ContentType.IntegerNumber;
        ci.field.characterLimit = 6;
        ci.field.caretColor = UIFactory.Gold;
        ci.field.text = "10";
        ci.field.onEndEdit.AddListener(s => ci.Set(int.TryParse(s, out int v) ? v : ci.Value));

        Small(row, "+", 444f, 64f, () => ci.Set(ci.Value + ci.Step(true)));
        Small(row, "½", 530f, 90f, () => ci.Set(ci.Value / 2));
        Small(row, "x2", 630f, 90f, () => ci.Set(ci.Value * 2));
        Small(row, "TODO", 730f, 120f, () => ci.Set(GameSession.Coins));
        return ci;
    }

    private static void Small(Transform row, string label, float x, float w, System.Action onClick)
    {
        Button b = UIFactory.Button(row, label, 32, onClick, new Vector2(w, 68f));
        UIFactory.Place((RectTransform)b.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(w, 68f));
    }

    private int Step(bool up)
    {
        int v = up ? Value : Value - 1;
        return v >= 200 ? 50 : v >= 50 ? 10 : v >= 10 ? 5 : 1;
    }

    public void Set(int v)
    {
        Value = Mathf.Clamp(v, 1, 999999);
        field.text = Value.ToString();
    }
}

// Reduce el panel si la pantalla es más estrecha que 16:9 (p. ej. el lienzo 16:10 de la versión web).
public class FitInParent : MonoBehaviour
{
    private void LateUpdate()
    {
        var rt = (RectTransform)transform;
        Rect parent = ((RectTransform)rt.parent).rect;
        float s = Mathf.Min(1f, parent.width / rt.sizeDelta.x, parent.height / rt.sizeDelta.y);
        rt.localScale = new Vector3(s, s, 1f);
    }
}

// Zona clicable del tablero (número, apuesta exterior o borde entre números). Clic izq. apuesta, clic der. retira.
public class BetZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public System.Action onAdd, onRemove, onEnter, onExit;

    public void OnPointerEnter(PointerEventData e) => onEnter?.Invoke();
    public void OnPointerExit(PointerEventData e) => onExit?.Invoke();

    public void OnPointerClick(PointerEventData e)
    {
        Sfx.Play(Sfx.Clip.MenuSelect);
        if (e.button == PointerEventData.InputButton.Right) onRemove?.Invoke();
        else if (e.button == PointerEventData.InputButton.Left) onAdd?.Invoke();
    }

    private void OnDisable() => onExit?.Invoke();
}
