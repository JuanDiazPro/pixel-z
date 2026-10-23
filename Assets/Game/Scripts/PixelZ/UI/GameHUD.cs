using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// HUD de la partida: corazones, monedas, llave, ronda, tiempo, habilidad, dash,
// avisos (toasts), carteles de ronda, textos sobre objetos interactivos y números de daño.
public class GameHUD : MonoBehaviour
{
    private static GameHUD instance;

    private PixelZDatabase db;
    private RectTransform root;

    private readonly List<Image> hearts = new List<Image>();
    private RectTransform heartsRow;
    private Text coinsText, waveText, waveInfo, timeText, killsText, comboText;
    private RectTransform coinIcon;
    private Image keyIcon;
    private Image abilityIcon, abilityFill, dashFill, potionIcon, potionFill;
    private Text abilityText, abilityKey, dashText;
    private RectTransform bossBar;
    private Image bossFill;
    private Text bossName;
    private Image damageFlash;
    private Text objectiveText;
    private RectTransform objectiveArrow;
    private Text objectiveDistance;

    private Text bannerTitle, bannerSub;
    private CanvasGroup bannerGroup;
    private float bannerTime;

    private readonly List<(Text text, float time)> toasts = new List<(Text, float)>();
    private RectTransform toastRoot;

    private Text prompt;
    private Vector2 promptWorld;
    private bool promptRequested;

    private RectTransform popupRoot;
    private readonly List<PopupEntry> popups = new List<PopupEntry>();

    private class PopupEntry
    {
        public Text text;
        public Vector2 world;
        public float time;
    }

    private float coinPulse;

    public static GameHUD Create()
    {
        Canvas canvas = UIFactory.CreateCanvas("HUD", 10);
        var hud = canvas.gameObject.AddComponent<GameHUD>();
        hud.Build(canvas.transform);
        instance = hud;
        return hud;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Build(Transform canvas)
    {
        db = PixelZDatabase.I;
        root = UIFactory.Rect("Root", canvas);
        UIFactory.Stretch(root);

        damageFlash = UIFactory.Image(root, "Damage Flash", db.white, new Color(1f, 0f, 0f, 0f));
        UIFactory.Stretch(damageFlash.rectTransform);

        popupRoot = UIFactory.Rect("Popups", root);
        UIFactory.Stretch(popupRoot);

        // --- Arriba a la izquierda: corazones y monedas ---
        Image topLeft = UIFactory.Panel(root, "Stats Panel", new Color(0.05f, 0.04f, 0.07f, 0.6f), false);
        UIFactory.Place(topLeft.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(420f, 150f));
        topLeft.raycastTarget = false;

        heartsRow = UIFactory.Rect("Hearts", topLeft.transform);
        UIFactory.Place(heartsRow, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(400f, 60f));

        coinIcon = UIFactory.Image(topLeft.transform, "Coin", db.coin, Color.white).rectTransform;
        UIFactory.Place(coinIcon, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -84f), new Vector2(44f, 44f));
        coinsText = UIFactory.Text(topLeft.transform, "0", 34, UIFactory.Gold, TextAnchor.MiddleLeft);
        UIFactory.Place(coinsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(68f, -82f), new Vector2(130f, 48f));

        // Las gemas no son otra moneda: suman GemValue monedas a este mismo contador.
        keyIcon = UIFactory.Image(topLeft.transform, "Key", db.key, Color.white);
        UIFactory.Place(keyIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -80f), new Vector2(52f, 52f));

        // Objetivo actual (debajo del panel)
        objectiveText = UIFactory.Text(root, "", 22, UIFactory.Cream, TextAnchor.UpperLeft);
        UIFactory.Place(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -176f), new Vector2(640f, 60f));

        objectiveArrow = UIFactory.Image(root, "Objective Arrow", db.arrow, UIFactory.Gold).rectTransform;
        objectiveArrow.sizeDelta = new Vector2(150f, 150f);
        objectiveArrow.anchorMin = objectiveArrow.anchorMax = new Vector2(0.5f, 0.5f);
        objectiveDistance = UIFactory.Text(objectiveArrow, "", 20, UIFactory.Gold);
        objectiveDistance.rectTransform.sizeDelta = new Vector2(120f, 30f);

        // --- Arriba al centro: ronda ---
        waveText = UIFactory.Text(root, "RONDA 1", 52, UIFactory.Gold);
        UIFactory.Place(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(700f, 64f));
        waveInfo = UIFactory.Text(root, "", 26, UIFactory.Cream);
        UIFactory.Place(waveInfo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(1000f, 36f));

        // Barra de jefe
        bossBar = UIFactory.Rect("Boss Bar", root);
        UIFactory.Place(bossBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(720f, 30f));
        Image bossBg = UIFactory.Panel(bossBar, "BG", new Color(0.1f, 0.02f, 0.05f, 0.9f));
        UIFactory.Stretch(bossBg.rectTransform);
        bossFill = UIFactory.Image(bossBar, "Fill", db.white, new Color(0.85f, 0.15f, 0.75f));
        UIFactory.Stretch(bossFill.rectTransform, 4f);
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        bossName = UIFactory.Text(bossBar, "JEFE", 24, Color.white);
        UIFactory.Place(bossName.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(700f, 34f));
        bossBar.gameObject.SetActive(false);

        // --- Arriba a la derecha: tiempo, bajas y combo ---
        timeText = UIFactory.Text(root, "00:00", 34, UIFactory.Cream, TextAnchor.MiddleRight);
        UIFactory.Place(timeText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -16f), new Vector2(300f, 44f));
        killsText = UIFactory.Text(root, "Bajas: 0", 26, UIFactory.Cream, TextAnchor.MiddleRight);
        UIFactory.Place(killsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -60f), new Vector2(300f, 36f));
        comboText = UIFactory.Text(root, "", 40, new Color(1f, 0.55f, 0.25f), TextAnchor.MiddleRight);
        UIFactory.Place(comboText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -104f), new Vector2(400f, 50f));

        // --- Abajo a la izquierda: habilidad, dash y poción ---
        abilityIcon = BuildSlot("Ability", new Vector2(24f, 24f), out abilityFill, out abilityText);
        UIFactory.Stretch(abilityIcon.rectTransform, 10f);   // los iconos de habilidad ya traen su propio marco/aire
        abilityKey = UIFactory.Text(abilityIcon.transform.parent, "Click der. / Q", 18, UIFactory.Cream);
        UIFactory.Place(abilityKey.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(200f, 26f));

        Image dashIcon = BuildSlot("Dash", new Vector2(150f, 24f), out dashFill, out dashText);
        dashIcon.sprite = db.DashIcon;
        UIFactory.Stretch(dashIcon.rectTransform, 10f);
        Text dashKey = UIFactory.Text(dashIcon.transform.parent, "Espacio", 18, UIFactory.Cream);
        UIFactory.Place(dashKey.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(160f, 26f));

        potionIcon = BuildSlot("Potion", new Vector2(276f, 24f), out potionFill, out _);
        potionIcon.sprite = db.potion;
        potionFill.fillOrigin = (int)Image.Origin360.Top;

        // Controles
        Text help = UIFactory.Text(root, "WASD mover · Click atacar · Espacio dash · Click der./Q habilidad · E interactuar · B tienda · Esc pausa", 18, new Color(1f, 1f, 1f, 0.55f), TextAnchor.LowerRight);
        UIFactory.Place(help.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-20f, 14f), new Vector2(1100f, 30f));

        // Carteles
        var bannerRt = UIFactory.Rect("Banner", root);
        UIFactory.Place(bannerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1400f, 200f));
        bannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.alpha = 0f;
        bannerGroup.blocksRaycasts = false;
        bannerTitle = UIFactory.Text(bannerRt, "", 84, UIFactory.Gold);
        UIFactory.Place(bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 110f));
        bannerSub = UIFactory.Text(bannerRt, "", 30, UIFactory.Cream);
        UIFactory.Place(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -50f), new Vector2(1400f, 50f));

        toastRoot = UIFactory.Rect("Toasts", root);
        UIFactory.Place(toastRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1200f, 300f));

        prompt = UIFactory.Text(root, "", 26, UIFactory.Gold);
        prompt.rectTransform.sizeDelta = new Vector2(700f, 140f);
        prompt.rectTransform.pivot = new Vector2(0.5f, 0f);
        prompt.gameObject.SetActive(false);
    }

    private Image BuildSlot(string name, Vector2 pos, out Image fill, out Text label)
    {
        Image frame = UIFactory.Panel(root, name + " Slot", new Color(0.08f, 0.06f, 0.1f, 0.85f));
        UIFactory.Place(frame.rectTransform, Vector2.zero, Vector2.zero, pos + new Vector2(0f, 30f), new Vector2(104f, 104f));
        frame.raycastTarget = false;

        Image icon = UIFactory.Image(frame.transform, "Icon", null, Color.white);
        UIFactory.Stretch(icon.rectTransform, 18f);
        icon.preserveAspect = true;

        fill = UIFactory.Image(frame.transform, "Cooldown", db.white, new Color(0f, 0f, 0f, 0.65f));
        UIFactory.Stretch(fill.rectTransform, 4f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Radial360;
        fill.fillOrigin = (int)Image.Origin360.Top;
        fill.fillClockwise = false;

        label = UIFactory.Text(frame.transform, "", 34, Color.white);
        UIFactory.Stretch(label.rectTransform);
        return icon;
    }

    private void Update()
    {
        UpdateHearts();
        UpdateCurrency();
        UpdateWave();
        UpdateSlots();
        UpdateBanner();
        UpdateToasts();
        UpdatePopups();
        UpdateBoss();

        float a = damageFlash.color.a;
        if (a > 0f) damageFlash.color = new Color(1f, 0f, 0f, Mathf.Max(0f, a - Time.unscaledDeltaTime * 1.5f));
    }

    private void LateUpdate()
    {
        UpdateObjective();

        prompt.gameObject.SetActive(promptRequested);
        if (promptRequested && Camera.main != null)
        {
            Vector2 screen = Camera.main.WorldToScreenPoint(promptWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            prompt.rectTransform.anchoredPosition = local;
        }
        promptRequested = false;
    }

    // Texto del objetivo y flecha: en el borde de la pantalla si el objetivo está fuera de vista,
    // o rebotando encima de él si está visible.
    private void UpdateObjective()
    {
        GameManager gm = GameManager.Instance;
        Camera cam = Camera.main;
        PlayerController player = PlayerController.Instance;
        objectiveText.text = gm != null && !string.IsNullOrEmpty(gm.ObjectiveText) ? "<color=#ffd24d>Objetivo:</color> " + gm.ObjectiveText : "";

        bool show = gm != null && gm.ObjectiveTarget.HasValue && cam != null && player != null && !player.Dead && Time.timeScale > 0f;
        // Al llegar junto al objetivo (cofre, llave, puerta, jefe) la flecha desaparece: ya no hace falta.
        if (show && Vector2.Distance(player.transform.position, gm.ObjectiveTarget.Value) < 3.5f) show = false;
        objectiveArrow.gameObject.SetActive(show);
        if (!show) return;

        Vector2 target = gm.ObjectiveTarget.Value;
        Vector2 rootSize = root.rect.size;
        Vector3 vp = cam.WorldToViewportPoint(target + Vector2.up * 0.5f);
        bool onScreen = vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.08f && vp.y < 0.92f;

        if (onScreen)
        {
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) * 18f;
            Vector2 local = new Vector2((vp.x - 0.5f) * rootSize.x, (vp.y - 0.5f) * rootSize.y + 70f + bob);
            objectiveArrow.anchoredPosition = local;
            objectiveArrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
            objectiveDistance.text = "";
        }
        else
        {
            Vector2 dir = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z < 0f) dir = -dir;
            dir.Normalize();
            Vector2 half = rootSize * 0.5f - new Vector2(110f, 190f);
            float k = Mathf.Min(Mathf.Abs(half.x / Mathf.Max(Mathf.Abs(dir.x), 0.001f)), Mathf.Abs(half.y / Mathf.Max(Mathf.Abs(dir.y), 0.001f)));
            objectiveArrow.anchoredPosition = dir * k;
            objectiveArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            float d = Vector2.Distance(player.transform.position, target);
            objectiveDistance.text = Mathf.RoundToInt(d) + " m";
            objectiveDistance.rectTransform.localRotation = Quaternion.Inverse(objectiveArrow.localRotation);
            objectiveDistance.rectTransform.anchoredPosition = Vector2.left * 0f;
        }
        float pulse = 1f + 0.1f * Mathf.Sin(Time.unscaledTime * 8f);
        objectiveArrow.localScale = Vector3.one * pulse;
    }

    private void UpdateHearts()
    {
        while (hearts.Count < GameSession.MaxHearts)
        {
            Image h = UIFactory.Image(heartsRow, "Heart", db.heart, Color.white);
            UIFactory.Place(h.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(hearts.Count * 62f, 0f), new Vector2(56f, 56f));
            hearts.Add(h);
        }

        bool danger = GameSession.Hearts == 1;
        for (int i = 0; i < hearts.Count; i++)
        {
            bool visible = i < GameSession.MaxHearts;
            hearts[i].gameObject.SetActive(visible);
            bool full = i < GameSession.Hearts;
            hearts[i].sprite = full ? db.heart : db.heartEmpty;
            float s = full && danger ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            hearts[i].rectTransform.localScale = Vector3.one * s;
        }
    }

    private void UpdateCurrency()
    {
        coinsText.text = GameSession.Coins.ToString();
        keyIcon.gameObject.SetActive(GameSession.HasKey);

        coinPulse = Mathf.MoveTowards(coinPulse, 0f, Time.unscaledDeltaTime * 3f);
        coinIcon.localScale = Vector3.one * (1f + 0.35f * coinPulse);

        float t = GameSession.TimeAlive;
        timeText.text = string.Format("{0:00}:{1:00}", (int)(t / 60f), (int)(t % 60f));
        killsText.text = "Bajas: " + GameSession.Kills;

        int combo = GameManager.Instance != null ? GameManager.Instance.Combo : 0;
        comboText.text = combo >= 3 ? "COMBO x" + combo : "";
        comboText.rectTransform.localScale = Vector3.one * (1f + Mathf.Min(combo, 30) * 0.01f);
    }

    private void UpdateWave()
    {
        WaveDirector wd = WaveDirector.Instance;
        if (wd == null) return;

        string map = GameManager.Instance != null && GameManager.Instance.MapIndex == 2 ? "Castillo" : "Ruinas";
        switch (wd.CurrentPhase)
        {
            case WaveDirector.Phase.Intro:
                waveText.text = map.ToUpper();
                waveInfo.text = "La horda llega en " + Mathf.CeilToInt(wd.BreakRemaining) + " s   ·   [B] Tienda";
                break;
            case WaveDirector.Phase.Break:
                waveText.text = "DESCANSO";
                waveInfo.text = "Ronda " + (GameSession.Wave + 1) + " en " + Mathf.CeilToInt(wd.BreakRemaining) + " s   ·   [B] Tienda   ·   [Enter] Empezar ya";
                break;
            default:
                waveText.text = "RONDA " + GameSession.Wave;
                waveInfo.text = map + "   ·   Enemigos restantes: " + wd.EnemiesLeft;
                break;
        }
    }

    private void UpdateSlots()
    {
        PlayerController p = PlayerController.Instance;
        if (p == null) return;

        abilityIcon.sprite = db.AbilityIcon(p.Def.ability);
        abilityFill.fillAmount = p.AbilityRemaining / Mathf.Max(p.AbilityCooldown, 0.01f);
        abilityText.text = p.AbilityRemaining > 0f ? Mathf.CeilToInt(p.AbilityRemaining).ToString() : "";
        abilityKey.text = p.Def.abilityName;

        dashFill.fillAmount = p.DashRemaining / p.DashCooldown;
        dashText.text = "";

        float buff = Mathf.Max(p.SpeedBuffRemaining, p.FuryRemaining);
        potionIcon.transform.parent.gameObject.SetActive(buff > 0f);
        potionIcon.sprite = p.SpeedBuffRemaining > 0f ? db.potion : db.mana;
        potionFill.fillAmount = 1f - buff / (p.SpeedBuffRemaining > 0f ? 8f : 5f);
    }

    private void UpdateBoss()
    {
        WaveDirector wd = WaveDirector.Instance;
        Enemy boss = wd != null ? wd.Boss : null;
        bool show = boss != null && boss.Alive;
        bossBar.gameObject.SetActive(show);
        if (!show) return;
        bossFill.fillAmount = boss.HealthPercent;
        bossName.text = boss.Def.name.ToUpper();
    }

    private void UpdateBanner()
    {
        if (bannerTime <= 0f) return;
        bannerTime -= Time.unscaledDeltaTime;
        float t = bannerTime;
        bannerGroup.alpha = Mathf.Clamp01(t / 0.6f) * Mathf.Clamp01((3.6f - t) / 0.25f);
        float s = 1f + 0.25f * Mathf.Clamp01((t - 3.3f) / 0.3f);
        bannerGroup.transform.localScale = Vector3.one * s;
    }

    private void UpdateToasts()
    {
        for (int i = toasts.Count - 1; i >= 0; i--)
        {
            var (text, time) = toasts[i];
            time -= Time.unscaledDeltaTime;
            if (time <= 0f)
            {
                Destroy(text.gameObject);
                toasts.RemoveAt(i);
                continue;
            }
            toasts[i] = (text, time);
            Color c = text.color;
            c.a = Mathf.Clamp01(time / 0.5f);
            text.color = c;
        }
        for (int i = 0; i < toasts.Count; i++)
        {
            RectTransform rt = toasts[i].text.rectTransform;
            float targetY = (toasts.Count - 1 - i) * 42f;
            rt.anchoredPosition = new Vector2(0f, Mathf.Lerp(rt.anchoredPosition.y, targetY, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime)));
        }
    }

    private void UpdatePopups()
    {
        Camera cam = Camera.main;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            PopupEntry p = popups[i];
            p.time += Time.deltaTime;
            if (p.time > 0.9f || cam == null)
            {
                Destroy(p.text.gameObject);
                popups.RemoveAt(i);
                continue;
            }
            Vector2 world = p.world + Vector2.up * (p.time * 0.8f);
            Vector2 screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRoot, screen, null, out Vector2 local);
            p.text.rectTransform.anchoredPosition = local;
            Color c = p.text.color;
            c.a = Mathf.Clamp01((0.9f - p.time) / 0.3f);
            p.text.color = c;
            float s = p.time < 0.1f ? Mathf.Lerp(1.5f, 1f, p.time / 0.1f) : 1f;
            p.text.rectTransform.localScale = Vector3.one * s;
        }
    }

    // ---------- API estática ----------

    public static void Toast(string message, Color color)
    {
        if (instance == null) return;
        if (instance.toasts.Count > 0 && instance.toasts[instance.toasts.Count - 1].text.text == message)
        {
            instance.toasts[instance.toasts.Count - 1] = (instance.toasts[instance.toasts.Count - 1].text, 2.5f);
            return;
        }
        Text t = UIFactory.Text(instance.toastRoot, message, 28, color);
        UIFactory.Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -40f), new Vector2(1200f, 40f));
        instance.toasts.Add((t, 2.5f));
        if (instance.toasts.Count > 5)
        {
            Destroy(instance.toasts[0].text.gameObject);
            instance.toasts.RemoveAt(0);
        }
    }

    public static void Banner(string title, string subtitle)
    {
        if (instance == null) return;
        instance.bannerTitle.text = title;
        instance.bannerSub.text = subtitle;
        instance.bannerTime = 3.6f;
    }

    public static void Prompt(Vector2 worldPos, string text, Color color)
    {
        if (instance == null) return;
        instance.promptRequested = true;
        instance.promptWorld = worldPos;
        instance.prompt.text = text;
        instance.prompt.color = color;
    }

    public static void Popup(Vector2 worldPos, string text, Color color)
    {
        if (instance == null) return;
        if (instance.popups.Count > 60) return;
        Text t = UIFactory.Text(instance.popupRoot, text, 30, color);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        t.rectTransform.sizeDelta = new Vector2(300f, 40f);
        instance.popups.Add(new PopupEntry { text = t, world = worldPos + Random.insideUnitCircle * 0.15f });
    }

    public static void PulseCurrency(PickupType type)
    {
        if (instance == null) return;
        if (type == PickupType.Coin || type == PickupType.Gem) instance.coinPulse = 1f;
    }

    public static void FlashDamage()
    {
        if (instance == null) return;
        instance.damageFlash.color = new Color(1f, 0f, 0f, 0.3f);
    }

    public static void SetVisible(bool visible)
    {
        if (instance == null) return;
        instance.root.gameObject.SetActive(visible);
    }
}
