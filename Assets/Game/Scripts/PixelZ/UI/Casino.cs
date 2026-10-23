using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Casino de la tienda: sala para elegir juego (Ruleta o Blackjack). Se apuesta con las monedas de la partida.
// Vive dentro de la tienda, así que el juego sigue en pausa mientras está abierto.
public class Casino : MonoBehaviour
{
    public static readonly int[] ChipValues = { 1, 5, 10, 25, 100 };
    public static readonly Color Felt = new Color(0.05f, 0.32f, 0.16f, 1f);
    public static readonly Color RedPocket = new Color(0.78f, 0.1f, 0.1f, 1f);
    public static readonly Color BlackPocket = new Color(0.08f, 0.08f, 0.09f, 1f);
    public static readonly Color GreenPocket = new Color(0.05f, 0.55f, 0.25f, 1f);

    // Mientras la ruleta gira el saldo mostrado no debe revelar el resultado.
    public static int? HeldCoins;

    public static int SelectedChip { get; private set; } = 10;
    private static readonly List<UIButtonFx> chipButtons = new List<UIButtonFx>();
    private static readonly List<int> chipButtonValues = new List<int>();

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
        casino.Build(root, db);
        root.gameObject.SetActive(false);
        return casino;
    }

    private void Build(RectTransform root, PixelZDatabase db)
    {
        Image panel = UIFactory.Panel(root, "Panel", new Color(0.06f, 0.05f, 0.07f, 0.97f));
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1840f, 1040f));
        panel.gameObject.AddComponent<FitInParent>();
        Transform p = panel.transform;

        titleText = UIFactory.Text(p, "CASINO", 60, UIFactory.Gold);
        UIFactory.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(1000f, 76f));

        Image coin = UIFactory.Image(p, "Coin", db.coin, Color.white);
        UIFactory.Place(coin.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-300f, -54f), new Vector2(48f, 48f));
        coinsText = UIFactory.Text(p, "0", 42, UIFactory.Gold, TextAnchor.MiddleLeft);
        UIFactory.Place(coinsText.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(-266f, -54f), new Vector2(250f, 56f));

        noticeText = UIFactory.Text(p, "", 30, UIFactory.Cream);
        UIFactory.Place(noticeText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(1200f, 40f));

        Button back = UIFactory.Button(p, "VOLVER", 32, Back, new Vector2(240f, 70f));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(240f, 70f));

        BuildLobby(p, db);

        RectTransform rScreen = UIFactory.Stretch(UIFactory.Rect("Roulette", p));
        roulette = rScreen.gameObject.AddComponent<Roulette>();
        roulette.Build(rScreen);

        RectTransform bScreen = UIFactory.Stretch(UIFactory.Rect("Blackjack", p));
        blackjack = bScreen.gameObject.AddComponent<Blackjack>();
        blackjack.Build(bScreen);
    }

    private void BuildLobby(Transform p, PixelZDatabase db)
    {
        RectTransform screen = UIFactory.Stretch(UIFactory.Rect("Lobby", p));
        lobby = screen.gameObject;

        Text sub = UIFactory.Text(screen, "Elige a qué quieres jugar. Apuestas las monedas que ganaste en la partida.", 30, UIFactory.Cream);
        UIFactory.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1500f, 50f));

        GameCard(screen, new Vector2(-330f, -30f), "RULETA", "Rojo o negro, par o impar, docenas, columnas,\nnúmeros sueltos, caballos, calles y esquinas.\nPaga hasta 35 a 1.", RedPocket, ShowRoulette);
        GameCard(screen, new Vector2(330f, -30f), "BLACKJACK", "Llega a 21 sin pasarte y gana al crupier.\nBlackjack paga 3 a 2.\nPuedes doblar tu apuesta.", Felt, ShowBlackjack);
    }

    private static void GameCard(Transform parent, Vector2 pos, string title, string desc, Color color, System.Action onClick)
    {
        Image card = UIFactory.Panel(parent, title, new Color(0.12f, 0.09f, 0.1f, 0.97f));
        UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(600f, 560f));

        Image banner = UIFactory.Image(card.transform, "Banner", null, color);
        UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(552f, 200f));
        Text t = UIFactory.Text(banner.transform, title, 64, UIFactory.Gold);
        UIFactory.Stretch(t.rectTransform);

        Text d = UIFactory.Text(card.transform, desc, 26, UIFactory.Cream);
        d.lineSpacing = 1.2f;
        UIFactory.Place(d.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(540f, 150f));

        Button play = UIFactory.Button(card.transform, "JUGAR", 44, onClick, new Vector2(360f, 96f));
        UIFactory.Place((RectTransform)play.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(360f, 96f));
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

    // ---------- Piezas compartidas ----------

    private static Sprite disc;

    // Círculo generado por código (no hay sprite de círculo sólido en el proyecto).
    public static Sprite Disc
    {
        get
        {
            if (disc != null) return disc;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return disc;
        }
    }

    public static Color ChipColor(int amount)
    {
        if (amount >= 100) return new Color(0.12f, 0.12f, 0.14f);
        if (amount >= 25) return new Color(0.15f, 0.55f, 0.25f);
        if (amount >= 10) return new Color(0.2f, 0.35f, 0.85f);
        if (amount >= 5) return new Color(0.8f, 0.15f, 0.15f);
        return new Color(0.85f, 0.85f, 0.85f);
    }

    public static Image Chip(Transform parent, int amount, float size, out Text label)
    {
        Image chip = UIFactory.Image(parent, "Chip", Disc, ChipColor(amount));
        chip.rectTransform.sizeDelta = new Vector2(size, size);
        var ring = UIFactory.Image(chip.transform, "Ring", Disc, new Color(1f, 1f, 1f, 0.85f));
        UIFactory.Stretch(ring.rectTransform, size * 0.12f);
        var inner = UIFactory.Image(chip.transform, "Inner", Disc, ChipColor(amount));
        UIFactory.Stretch(inner.rectTransform, size * 0.18f);
        label = UIFactory.Text(chip.transform, amount.ToString(), Mathf.RoundToInt(size * 0.4f), Color.white);
        UIFactory.Stretch(label.rectTransform);
        return chip;
    }

    // Fila de fichas para elegir cuánto vale cada clic de apuesta. La selección se comparte entre juegos.
    public static void ChipSelector(Transform parent, Vector2 pos)
    {
        Text label = UIFactory.Text(parent, "FICHA:", 28, UIFactory.Cream, TextAnchor.MiddleRight);
        UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), pos + new Vector2(-10f, 0f), new Vector2(160f, 50f));
        for (int i = 0; i < ChipValues.Length; i++)
        {
            int value = ChipValues[i];
            Button b = UIFactory.Button(parent, "", 24, () => SelectChip(value), new Vector2(84f, 84f));
            UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), pos + new Vector2(i * 94f, 0f), new Vector2(84f, 84f));
            Chip(b.transform, value, 64f, out _);
            var fx = b.GetComponent<UIButtonFx>();
            chipButtons.Add(fx);
            chipButtonValues.Add(value);
        }
        SelectChip(SelectedChip);
    }

    private static void SelectChip(int value)
    {
        SelectedChip = value;
        for (int i = chipButtons.Count - 1; i >= 0; i--)
        {
            if (chipButtons[i] == null) { chipButtons.RemoveAt(i); chipButtonValues.RemoveAt(i); continue; }
            chipButtons[i].selected = chipButtonValues[i] == value;
        }
    }

    // Aviso corto dentro del casino (los avisos del HUD quedan tapados por la tienda).
    public static void Toast(string msg, Color color)
    {
        if (current == null) return;
        current.noticeText.text = msg;
        current.noticeText.color = color;
        current.noticeTime = 2.5f;
    }
}

// Zona clicable del tablero (número, apuesta exterior o borde entre números). Clic izq. pone ficha, clic der. la quita.
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
