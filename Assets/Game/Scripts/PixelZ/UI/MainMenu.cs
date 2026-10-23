using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Menú principal: portada de Pixel Z, selección de soldado y botón Jugar (pasa por la pantalla de carga).
public class MainMenu : MonoBehaviour
{
    private PixelZDatabase db;
    private GameObject titleScreen, selectScreen;
    private readonly List<Card> cards = new List<Card>();
    private int selected;

    private class Card
    {
        public Image frame;
        public UIButtonFx fx;
        public SpriteAnimator preview;
        public Text check;
        public PlayerClassDef def;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        db = PixelZDatabase.I;
        selected = GameSession.SelectedClass;
        UIFactory.EnsureEventSystem();

        Canvas canvas = UIFactory.CreateCanvas("Main Menu", 0);
        RectTransform root = UIFactory.Stretch(UIFactory.Rect("Root", canvas.transform));
        root.gameObject.AddComponent<RectMask2D>();
        UIFactory.FullscreenImage(root, db.menuBackground, Color.white);

        BuildTitle(root);
        BuildSelect(root);
        ShowSelect(false);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || LoadingScreen.IsLoading) return;

        if (titleScreen.activeSelf)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
            {
                Sfx.Play(Sfx.Clip.MenuSelect);
                ShowSelect(true);
            }
            return;
        }

        bool left = kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
        bool right = kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
        if (left || right || kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Sfx.Play(Sfx.Clip.MenuSelect);

        if (kb.escapeKey.wasPressedThisFrame) ShowSelect(false);
        if (left) Select((selected + cards.Count - 1) % cards.Count);
        if (right) Select((selected + 1) % cards.Count);
        if (kb.enterKey.wasPressedThisFrame) Play();
    }

    // ---------- Portada ----------

    private void BuildTitle(Transform root)
    {
        RectTransform screen = UIFactory.Stretch(UIFactory.Rect("Title Screen", root));
        titleScreen = screen.gameObject;

        Image fade = UIFactory.Image(screen, "Bottom Fade", db.white, new Color(0f, 0f, 0f, 0.35f));
        UIFactory.Place(fade.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(4000f, 250f));

        var row = UIFactory.Rect("Buttons", screen);
        UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1100f, 110f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = layout.childControlWidth = false;

        UIFactory.Button(row, "JUGAR", 52, () => ShowSelect(true), new Vector2(420f, 100f));
        UIFactory.Button(row, "SALIR", 40, Quit, new Vector2(300f, 100f));

        string record = GameSession.BestWave > 0
            ? "Récord: ronda " + GameSession.BestWave + "  ·  " + GameSession.BestCoins + " monedas"
            : "Sobrevive a hordas infinitas";
        Text rec = UIFactory.Text(screen, record, 30, UIFactory.Cream);
        UIFactory.Place(rec.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1200f, 40f));
    }

    // ---------- Selección de personaje ----------

    private void BuildSelect(Transform root)
    {
        RectTransform screen = UIFactory.Stretch(UIFactory.Rect("Character Select", root));
        selectScreen = screen.gameObject;

        Image shade = UIFactory.Image(screen, "Shade", db.white, new Color(0.02f, 0.02f, 0.04f, 0.72f));
        UIFactory.Stretch(shade.rectTransform);
        shade.raycastTarget = true;

        Text title = UIFactory.Text(screen, "ELIGE TU SOLDADO", 72, UIFactory.Gold);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1400f, 90f));

        var row = UIFactory.Rect("Cards", screen);
        UIFactory.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1800f, 680f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 30f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = layout.childControlWidth = false;

        for (int i = 0; i < PlayerClasses.All.Length; i++) cards.Add(BuildCard(row, i));

        Text controls = UIFactory.Text(screen,
            "WASD mover  ·  Click izq. atacar (hacia el ratón)  ·  Espacio dash  ·  Click der. / Q habilidad  ·  E interactuar  ·  B tienda  ·  Esc pausa",
            22, new Color(1f, 1f, 1f, 0.75f));
        UIFactory.Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1800f, 34f));

        var buttons = UIFactory.Rect("Buttons", screen);
        UIFactory.Place(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1000f, 100f));
        var bl = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        bl.spacing = 40f;
        bl.childAlignment = TextAnchor.MiddleCenter;
        bl.childControlHeight = bl.childControlWidth = false;
        UIFactory.Button(buttons, "VOLVER", 38, () => ShowSelect(false), new Vector2(300f, 90f));
        UIFactory.Button(buttons, "¡A JUGAR!", 50, Play, new Vector2(440f, 96f));

        Select(selected);
    }

    private Card BuildCard(Transform parent, int index)
    {
        PlayerClassDef def = PlayerClasses.All[index];
        var card = new Card { def = def };

        card.frame = UIFactory.Panel(parent, "Card - " + def.name, new Color(0.1f, 0.08f, 0.1f, 0.95f));
        card.frame.rectTransform.sizeDelta = new Vector2(400f, 660f);
        var button = card.frame.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() =>
        {
            Sfx.Play(Sfx.Clip.MenuSelect);
            Select(index);
        });
        card.fx = card.frame.gameObject.AddComponent<UIButtonFx>();
        card.fx.normal = new Color(0.1f, 0.08f, 0.1f, 0.95f);
        card.fx.hover = new Color(0.28f, 0.18f, 0.1f, 0.98f);
        Transform t = card.frame.transform;

        // Vista previa animada (se recorta para hacer zoom al personaje dentro del frame de 100x100).
        Image previewBg = UIFactory.Image(t, "Preview BG", db.white, new Color(0f, 0f, 0f, 0.35f));
        UIFactory.Place(previewBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(368f, 230f));
        previewBg.gameObject.AddComponent<RectMask2D>();
        Image preview = UIFactory.Image(previewBg.transform, "Preview", db.soldier.idle[0], def.tint);
        UIFactory.Place(preview.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(760f, 760f));
        card.preview = preview.gameObject.AddComponent<SpriteAnimator>();
        card.preview.uiTarget = preview;
        card.preview.unscaledTime = true;
        card.preview.Play(db.soldier.idle, 8f, true);

        Text name = UIFactory.Text(t, def.name.ToUpper(), 42, UIFactory.Gold);
        UIFactory.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(380f, 50f));

        Text desc = UIFactory.Text(t, def.description, 21, UIFactory.Cream);
        UIFactory.Place(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -302f), new Vector2(360f, 56f));

        float y = -372f;
        StatRow(t, "Velocidad", def.statSpeed, ref y);
        StatRow(t, "Daño", def.statDamage, ref y);
        StatRow(t, "Cadencia", def.statRate, ref y);
        StatRow(t, "Alcance", def.statRange, ref y);

        Image abilityIcon = UIFactory.Image(t, "Ability Icon", db.AbilityIcon(def.ability), Color.white);
        abilityIcon.preserveAspect = true;
        UIFactory.Place(abilityIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-148f, y - 14f), new Vector2(68f, 68f));
        Text ability = UIFactory.Text(t, "<color=#ffd24d>" + def.abilityName + "</color>\n<size=19>" + def.abilityDescription + "</size>", 24, UIFactory.Cream, TextAnchor.UpperLeft);
        UIFactory.Place(ability.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(38f, y - 8f), new Vector2(290f, 80f));

        // Corazones iniciales
        for (int h = 0; h < GameSession.StartHearts; h++)
        {
            Image heart = UIFactory.Image(t, "Heart", db.heart, Color.white);
            UIFactory.Place(heart.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((h - 1) * 44f, 16f), new Vector2(38f, 38f));
        }

        card.check = UIFactory.Text(t, "SELECCIONADO", 22, new Color(0.5f, 1f, 0.5f));
        UIFactory.Place(card.check.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(360f, 30f));
        return card;
    }

    private void StatRow(Transform parent, string label, int value, ref float y)
    {
        Text l = UIFactory.Text(parent, label, 22, UIFactory.Cream, TextAnchor.MiddleLeft);
        UIFactory.Place(l.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-170f, y), new Vector2(150f, 30f));
        for (int i = 0; i < 5; i++)
        {
            Image pip = UIFactory.Image(parent, "Pip", db.white, i < value ? UIFactory.Gold : new Color(1f, 1f, 1f, 0.15f));
            UIFactory.Place(pip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-10f + i * 36f, y - 6f), new Vector2(30f, 18f));
        }
        y -= 34f;
    }

    private void Select(int index)
    {
        selected = index;
        for (int i = 0; i < cards.Count; i++)
        {
            bool on = i == index;
            Card c = cards[i];
            c.fx.selected = on;
            c.check.gameObject.SetActive(on);
            c.frame.transform.localScale = Vector3.one;
            Sprite[] anim = on ? (c.def.ranged ? db.soldier.attack3 : db.soldier.attack1) : db.soldier.idle;
            c.preview.Play(on ? db.soldier.walk : db.soldier.idle, on ? 10f : 8f, true);
            if (on) StartCoroutine(PreviewAttack(c, anim));
        }
    }

    private System.Collections.IEnumerator PreviewAttack(Card c, Sprite[] attack)
    {
        c.preview.Play(attack, 12f, false, true);
        yield return new WaitForSecondsRealtime(attack.Length / 12f);
        if (cards.IndexOf(c) == selected) c.preview.Play(db.soldier.walk, 10f, true);
    }

    private void ShowSelect(bool show)
    {
        titleScreen.SetActive(!show);
        selectScreen.SetActive(show);
    }

    private void Play()
    {
        if (LoadingScreen.IsLoading) return;
        GameSession.SelectedClass = selected;
        GameSession.NewRun(selected);
        LoadingScreen.Load("Game", 3f);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
