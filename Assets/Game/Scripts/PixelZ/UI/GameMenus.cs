using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Pantallas que pausan el juego: Pausa, Tienda y Game Over.
public class GameMenus : MonoBehaviour
{
    public static GameMenus Instance { get; private set; }

    public bool PauseOpen => pauseRoot.activeSelf;
    public bool ShopOpen => shopRoot.activeSelf;
    public bool AnyOpen => PauseOpen || ShopOpen || gameOverRoot.activeSelf;

    private PixelZDatabase db;
    private GameObject pauseRoot, shopRoot, gameOverRoot;
    private Text shopCoins;
    private readonly List<ShopItem> items = new List<ShopItem>();
    private Text gameOverStats, gameOverRecord;

    private class ShopItem
    {
        public string name;
        public Func<string> description;
        public Sprite icon;
        public Func<int> level;
        public int maxLevel;
        public Func<int> price;
        public Func<bool> available;
        public Action buy;
        public Text title, desc, priceText;
        public Button button;
    }

    public static GameMenus Create()
    {
        Canvas canvas = UIFactory.CreateCanvas("Menus", 50);
        var menus = canvas.gameObject.AddComponent<GameMenus>();
        menus.Build(canvas.transform);
        Instance = menus;
        return menus;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build(Transform canvas)
    {
        db = PixelZDatabase.I;
        BuildPause(canvas);
        BuildShop(canvas);
        BuildGameOver(canvas);
    }

    // ---------- Pausa ----------

    private void BuildPause(Transform canvas)
    {
        RectTransform root = UIFactory.Stretch(UIFactory.Rect("Pause", canvas));
        root.gameObject.AddComponent<RectMask2D>();
        pauseRoot = root.gameObject;
        UIFactory.FullscreenImage(root, db.pauseBackground, Color.white);

        Image shade = UIFactory.Image(root, "Shade", db.white, new Color(0f, 0f, 0f, 0.25f));
        UIFactory.Stretch(shade.rectTransform);

        var column = UIFactory.Rect("Buttons", root);
        UIFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(460f, 400f));
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 22f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = layout.childControlWidth = false;

        UIFactory.Button(column, "CONTINUAR", 40, () => SetPause(false), new Vector2(440f, 84f));
        UIFactory.Button(column, "TIENDA", 40, () => { SetPause(false); TryOpenShop(); }, new Vector2(440f, 84f));
        UIFactory.Button(column, "REINICIAR", 40, Restart, new Vector2(440f, 84f));
        UIFactory.Button(column, "MENÚ PRINCIPAL", 40, GoToMenu, new Vector2(440f, 84f));

        pauseRoot.SetActive(false);
    }

    public void SetPause(bool paused)
    {
        if (gameOverRoot.activeSelf) return;
        if (paused && ShopOpen) SetShop(false);
        pauseRoot.SetActive(paused);
        ApplyTimeScale();
    }

    // ---------- Tienda ----------

    private void BuildShop(Transform canvas)
    {
        RectTransform root = UIFactory.Stretch(UIFactory.Rect("Shop", canvas));
        shopRoot = root.gameObject;
        Image shade = UIFactory.Image(root, "Shade", db.white, new Color(0f, 0f, 0f, 0.65f));
        UIFactory.Stretch(shade.rectTransform);
        shade.raycastTarget = true;

        Image panel = UIFactory.Panel(root, "Panel", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 960f));

        Text title = UIFactory.Text(panel.transform, "TIENDA DEL AVENTURERO", 60, UIFactory.Gold);
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1100f, 76f));

        Image coin = UIFactory.Image(panel.transform, "Coin", db.coin, Color.white);
        UIFactory.Place(coin.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 0.5f), new Vector2(-10f, -130f), new Vector2(46f, 46f));
        shopCoins = UIFactory.Text(panel.transform, "0", 40, UIFactory.Gold, TextAnchor.MiddleLeft);
        UIFactory.Place(shopCoins.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, -130f), new Vector2(300f, 50f));

        DefineItems();

        var list = UIFactory.Rect("Items", panel.transform);
        UIFactory.Place(list, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1160f, 600f));
        var grid = list.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(570f, 140f);
        grid.spacing = new Vector2(20f, 14f);
        grid.childAlignment = TextAnchor.UpperCenter;

        foreach (ShopItem item in items) BuildItemRow(list, item);

        Text hint = UIFactory.Text(panel.transform, "La tienda sólo abre entre rondas · [B] o [Esc] para cerrar", 22, new Color(1f, 1f, 1f, 0.6f));
        UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(1100f, 30f));
        Button close = UIFactory.Button(panel.transform, "CERRAR", 36, () => SetShop(false), new Vector2(360f, 76f));
        UIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(360f, 76f));

        shopRoot.SetActive(false);
    }

    private void DefineItems()
    {
        items.Add(new ShopItem
        {
            name = "Afilar arma", icon = db.sword, maxLevel = 8,
            description = () => "+1 daño por golpe (actual: " + (PlayerController.Instance != null ? PlayerController.Instance.Damage : 0) + ")",
            level = () => GameSession.DamageLevel, price = () => 20 + 15 * GameSession.DamageLevel,
            buy = () => GameSession.DamageLevel++,
        });
        items.Add(new ShopItem
        {
            name = "Entrenamiento", icon = db.star, maxLevel = 6,
            description = () => "+8% velocidad de ataque",
            level = () => GameSession.AttackSpeedLevel, price = () => 20 + 15 * GameSession.AttackSpeedLevel,
            buy = () => GameSession.AttackSpeedLevel++,
        });
        items.Add(new ShopItem
        {
            name = "Botas ligeras", icon = db.helmet, maxLevel = 5,
            description = () => "+8% velocidad de movimiento",
            level = () => GameSession.SpeedLevel, price = () => 18 + 12 * GameSession.SpeedLevel,
            buy = () => GameSession.SpeedLevel++,
        });
        items.Add(new ShopItem
        {
            name = "Concentración", icon = db.mana, maxLevel = 5,
            description = () => "-12% enfriamiento de la habilidad",
            level = () => GameSession.CooldownLevel, price = () => 25 + 15 * GameSession.CooldownLevel,
            buy = () => GameSession.CooldownLevel++,
        });
        items.Add(new ShopItem
        {
            name = "Imán de oro", icon = db.coin, maxLevel = 4,
            description = () => "Atrae monedas y gemas desde más lejos",
            level = () => GameSession.MagnetLevel, price = () => 15 + 10 * GameSession.MagnetLevel,
            buy = () => GameSession.MagnetLevel++,
        });
        items.Add(new ShopItem
        {
            name = "Corazón extra", icon = db.heart, maxLevel = GameSession.HeartsCap - GameSession.StartHearts,
            description = () => "+1 corazón máximo y te cura 1",
            level = () => GameSession.ExtraHeartLevel, price = () => 60 + 40 * GameSession.ExtraHeartLevel,
            buy = () =>
            {
                GameSession.ExtraHeartLevel++;
                GameSession.MaxHearts++;
                GameSession.Hearts = Mathf.Min(GameSession.Hearts + 1, GameSession.MaxHearts);
            },
        });
        items.Add(new ShopItem
        {
            name = "Manzana curativa", icon = db.apple, maxLevel = 0,
            description = () => "Recupera 1 corazón",
            level = () => 0, price = () => 25,
            available = () => GameSession.Hearts < GameSession.MaxHearts,
            buy = () => GameSession.Hearts = Mathf.Min(GameSession.Hearts + 1, GameSession.MaxHearts),
        });
        items.Add(new ShopItem
        {
            name = "Poción rápida", icon = db.potion, maxLevel = 0,
            description = () => "Velocidad x1.6 durante 8 s",
            level = () => 0, price = () => 20,
            buy = () => { if (PlayerController.Instance != null) PlayerController.Instance.ApplySpeedBuff(8f); },
        });
    }

    private void BuildItemRow(Transform parent, ShopItem item)
    {
        Image row = UIFactory.Panel(parent, item.name, new Color(0.14f, 0.1f, 0.12f, 0.95f));

        Image icon = UIFactory.Image(row.transform, "Icon", item.icon, Color.white);
        UIFactory.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(84f, 84f));

        item.title = UIFactory.Text(row.transform, item.name, 28, UIFactory.Gold, TextAnchor.UpperLeft);
        item.title.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.Place(item.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -12f), new Vector2(270f, 36f));
        item.desc = UIFactory.Text(row.transform, "", 20, UIFactory.Cream, TextAnchor.UpperLeft);
        UIFactory.Place(item.desc.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -50f), new Vector2(270f, 80f));

        item.button = UIFactory.Button(row.transform, "", 26, () => Buy(item), new Vector2(160f, 96f));
        UIFactory.Place((RectTransform)item.button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(160f, 96f));
        item.priceText = item.button.GetComponentInChildren<Text>();
    }

    private void Buy(ShopItem item)
    {
        if (!CanBuy(item, out _)) return;
        GameSession.Coins -= item.price();
        item.buy();
        GameHUD.Toast("Comprado: " + item.name, UIFactory.Gold);
        RefreshShop();
    }

    private bool CanBuy(ShopItem item, out string reason)
    {
        reason = null;
        if (item.maxLevel > 0 && item.level() >= item.maxLevel) { reason = "MÁX"; return false; }
        if (item.available != null && !item.available()) { reason = "LLENO"; return false; }
        if (GameSession.Coins < item.price()) { reason = item.price().ToString(); return false; }
        return true;
    }

    private void RefreshShop()
    {
        shopCoins.text = GameSession.Coins.ToString();
        foreach (ShopItem item in items)
        {
            string lvl = item.maxLevel > 0 ? "<color=#bbbbbb>Nivel " + item.level() + "/" + item.maxLevel + "</color>\n" : "";
            item.title.text = item.name;
            item.desc.text = lvl + item.description();
            bool ok = CanBuy(item, out string reason);
            item.priceText.text = ok ? item.price() + "\nComprar" : reason == "MÁX" || reason == "LLENO" ? reason : reason + "\n<size=18>sin monedas</size>";
            item.priceText.color = ok ? UIFactory.Gold : new Color(0.7f, 0.6f, 0.6f);
            item.button.interactable = ok;
            item.button.GetComponent<UIButtonFx>().normal = ok ? UIFactory.ButtonColor : new Color(0.18f, 0.16f, 0.16f, 0.95f);
        }
    }

    public bool TryOpenShop()
    {
        if (gameOverRoot.activeSelf) return false;
        if (WaveDirector.Instance != null && !WaveDirector.Instance.CanShop)
        {
            GameHUD.Toast("La tienda sólo abre entre rondas", new Color(1f, 0.6f, 0.5f));
            return false;
        }
        SetShop(true);
        return true;
    }

    public void SetShop(bool open)
    {
        shopRoot.SetActive(open);
        if (open) RefreshShop();
        ApplyTimeScale();
    }

    // ---------- Game Over ----------

    private void BuildGameOver(Transform canvas)
    {
        RectTransform root = UIFactory.Stretch(UIFactory.Rect("Game Over", canvas));
        root.gameObject.AddComponent<RectMask2D>();
        gameOverRoot = root.gameObject;

        Image shade = UIFactory.Image(root, "Shade", db.white, new Color(0.12f, 0f, 0.02f, 0.78f));
        UIFactory.Stretch(shade.rectTransform);
        shade.raycastTarget = true;

        Text title = UIFactory.Text(root, "GAME OVER", 140, new Color(1f, 0.3f, 0.25f));
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1400f, 170f));

        Image panel = UIFactory.Panel(root, "Stats", UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(760f, 470f));

        gameOverStats = UIFactory.Text(panel.transform, "", 38, UIFactory.Cream, TextAnchor.UpperLeft);
        gameOverStats.lineSpacing = 1.25f;
        UIFactory.Stretch(gameOverStats.rectTransform, 40f);

        gameOverRecord = UIFactory.Text(root, "", 40, UIFactory.Gold);
        UIFactory.Place(gameOverRecord.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -280f), new Vector2(1200f, 50f));

        var row = UIFactory.Rect("Buttons", root);
        UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(1000f, 100f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 40f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = layout.childControlWidth = false;
        UIFactory.Button(row, "REINTENTAR", 40, Restart, new Vector2(420f, 90f));
        UIFactory.Button(row, "MENÚ PRINCIPAL", 40, GoToMenu, new Vector2(420f, 90f));

        gameOverRoot.SetActive(false);
    }

    public void ShowGameOver()
    {
        pauseRoot.SetActive(false);
        shopRoot.SetActive(false);
        bool record = GameSession.SaveRecords();

        float t = GameSession.TimeAlive;
        string time = string.Format("{0:00}:{1:00}", (int)(t / 60f), (int)(t % 60f));
        string map = GameSession.MapReached >= 2 ? "Castillo" : "Ruinas";
        gameOverStats.text =
            "Personaje:  <color=#ffd24d>" + GameSession.Class.name + "</color>\n" +
            "Ronda alcanzada:  <color=#ffd24d>" + GameSession.Wave + "</color>   (" + map + ")\n" +
            "Monedas recogidas:  <color=#ffd24d>" + GameSession.CoinsCollected + "</color>\n" +
            "Gemas recogidas:  <color=#7ff2ff>" + GameSession.GemsCollected + "</color>\n" +
            "Enemigos eliminados:  <color=#ffd24d>" + GameSession.Kills + "</color>\n" +
            "Tiempo sobrevivido:  <color=#ffd24d>" + time + "</color>";
        gameOverRecord.text = record ? "¡NUEVO RÉCORD!" : "Récord: ronda " + GameSession.BestWave + "  ·  " + GameSession.BestCoins + " monedas";

        gameOverRoot.SetActive(true);
        GameHUD.SetVisible(false);
        ApplyTimeScale();
    }

    // ---------- Navegación ----------

    private void ApplyTimeScale()
    {
        Time.timeScale = AnyOpen ? 0f : 1f;
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        GameSession.NewRun(GameSession.ClassIndex);
        LoadingScreen.Load("Game");
    }

    private void GoToMenu()
    {
        Time.timeScale = 1f;
        GameSession.RunActive = false;
        LoadingScreen.Load("MainMenu", 1.2f);
    }
}
