using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Ruleta europea (un solo cero) con el tablero completo: plenos, caballos (2), calles (3), esquinas (4),
// línea (6), docenas, columnas, rojo/negro, par/impar y falta/pasa. Pago = 36 / números - 1 a 1.
// La rueda y el tablero se dibujan en pixel art con perspectiva; la bola gira, frena, rebota y cae en su casilla.
public class Roulette : MonoBehaviour
{
    private static readonly HashSet<int> Reds = new HashSet<int> { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

    public static bool IsRed(int n) => Reds.Contains(n);
    public static Color PocketColor(int n) => n == 0 ? Casino.GreenPocket : IsRed(n) ? Casino.RedPocket : Casino.BlackPocket;

    // Tablero en píxeles (cada píxel se ve como un bloque de Scale x Scale).
    private const int Scale = 3;
    private const int TW = 23, TH = 30, TO = 21;           // ancho de casilla, alto de número, alto de fila exterior
    private const float CellW = TW * Scale, CellH = TH * Scale, OutsideH = TO * Scale;
    private static readonly Vector2 BoardTopLeft = new Vector2(-92f, 352f);
    private static readonly Vector2 WheelCenter = new Vector2(-506f, 150f);

    private const float SpinTime = 8f;
    private const float IdleWheelSpeed = 18f;
    private const float PocketRadius = 0.68f;
    private const int MaxHistory = 13;

    private class Bet
    {
        public int[] numbers;
        public string name;
        public Vector2 center;
        public int amount;
        public RectTransform marker;
    }

    public bool Busy => spinning;

    private RectTransform board, history;
    private PixelCanvas wheelCanvas;
    private WheelRenderer wheelRenderer;
    private CoinInput amountInput;
    private readonly Dictionary<string, Bet> bets = new Dictionary<string, Bet>();
    private readonly Dictionary<string, Bet> zones = new Dictionary<string, Bet>();   // todas las apuestas posibles del tablero
    private readonly Dictionary<int, Image> cellHighlights = new Dictionary<int, Image>();
    private List<KeyValuePair<string, int>> lastBets = new List<KeyValuePair<string, int>>();
    private readonly List<int> results = new List<int>();
    private Text totalText, resultText;

    // Estado de la rueda y la bola (ángulos en grados, sentido horario; la bola se guarda relativa a la rueda).
    private float wheelDeg, ballRel, ballR = PocketRadius, ballHop;
    private bool hasBall;
    private bool spinning;
    private float spinT, relStart, relTarget;
    private int lastFret;
    private float lastBounce;
    private bool landed;
    private int pendingResult, pendingWin, pendingStake;

    public void Build(RectTransform screen)
    {
        wheelCanvas = new PixelCanvas(256, 168);
        wheelRenderer = new WheelRenderer(wheelCanvas, 128f, 78f, 118f);
        RawImage wheelImg = wheelCanvas.Show(screen, "Wheel", Scale);
        UIFactory.Place(wheelImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), WheelCenter, new Vector2(256 * Scale, 168 * Scale));
        RenderWheel();

        BuildBoard(screen);

        Text histLabel = UIFactory.Text(screen, "Últimos:", 24, UIFactory.Cream, TextAnchor.MiddleLeft);
        UIFactory.Place(histLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-92f, 388f), new Vector2(120f, 40f));
        history = UIFactory.Rect("History", screen);
        UIFactory.Place(history, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 388f), new Vector2(860f, 40f));
        var hl = history.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 6f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = hl.childControlHeight = false;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;

        float boardMid = BoardTopLeft.x + CellW * 7f;
        amountInput = CoinInput.Create(screen, new Vector2(boardMid, -118f));

        Btn(screen, "BORRAR", 30, ClearBets, new Vector2(boardMid - 380f, -222f), new Vector2(200f, 76f));
        Btn(screen, "REPETIR", 30, RepeatBets, new Vector2(boardMid - 170f, -222f), new Vector2(200f, 76f));
        Btn(screen, "DOBLAR TODO", 28, DoubleBets, new Vector2(boardMid + 50f, -222f), new Vector2(220f, 76f));
        Btn(screen, "¡GIRAR!", 46, Spin, new Vector2(boardMid + 330f, -222f), new Vector2(300f, 96f));

        resultText = UIFactory.Text(screen, "Escribe cuántas monedas apostar y haz clic en el tablero", 32, UIFactory.Cream);
        resultText.lineSpacing = 1.1f;
        UIFactory.Place(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(WheelCenter.x, -150f), new Vector2(760f, 90f));
        totalText = UIFactory.Text(screen, "", 32, UIFactory.Gold);
        UIFactory.Place(totalText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(WheelCenter.x, -240f), new Vector2(560f, 50f));

        Text pays = UIFactory.Text(screen, "Pagos:  Pleno 35:1  ·  Caballo 17:1  ·  Calle 11:1  ·  Esquina 8:1  ·  Línea 5:1  ·  Docena / Columna 2:1  ·  Rojo, Negro, Par, Impar, 1-18, 19-36  1:1", 23, UIFactory.Gold);
        UIFactory.Place(pays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -350f), new Vector2(1780f, 34f));
        Text help = UIFactory.Text(screen, "Clic izq. apuesta la cantidad escrita  ·  Clic der. retira esa apuesta  ·  Los bordes entre números son caballos, calles, esquinas y líneas", 22, new Color(1f, 1f, 1f, 0.6f));
        UIFactory.Place(help.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -400f), new Vector2(1780f, 34f));

        RefreshTotal();
    }

    private static void Btn(Transform parent, string label, int size, System.Action onClick, Vector2 pos, Vector2 dim)
    {
        Button b = UIFactory.Button(parent, label, size, onClick, dim);
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, dim);
    }

    // ---------- Tablero ----------

    private void BuildBoard(RectTransform screen)
    {
        PixelCanvas art = PaintBoard();
        RawImage img = art.Show(screen, "Board Art", Scale);
        UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), BoardTopLeft, new Vector2(art.W * Scale, art.H * Scale));

        board = UIFactory.Rect("Board", screen);
        UIFactory.Place(board, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), BoardTopLeft, new Vector2(CellW * 14f, CellH * 3f + OutsideH * 2f));

        Cell("0", new[] { 0 }, 0f, 0f, CellW, CellH * 3f);
        for (int n = 1; n <= 36; n++)
        {
            int c = (n - 1) / 3, r = 2 - (n - 1) % 3;
            Cell(n.ToString(), new[] { n }, CellW * (c + 1), CellH * r, CellW, CellH);
        }
        for (int r = 0; r < 3; r++)
        {
            int mod = r == 0 ? 0 : r == 1 ? 2 : 1;
            Cell((3 - r) + "ª columna", Enumerable.Range(1, 36).Where(n => n % 3 == mod).ToArray(), CellW * 13f, CellH * r, CellW, CellH);
        }
        for (int d = 0; d < 3; d++)
            Cell((d + 1) + "ª docena", Enumerable.Range(d * 12 + 1, 12).ToArray(), CellW * (1 + d * 4), CellH * 3f, CellW * 4f, OutsideH);

        float y = CellH * 3f + OutsideH, w = CellW * 2f;
        Cell("Falta (1-18)", Enumerable.Range(1, 18).ToArray(), CellW, y, w, OutsideH);
        Cell("Par", Enumerable.Range(1, 36).Where(n => n % 2 == 0).ToArray(), CellW + w, y, w, OutsideH);
        Cell("Rojo", Reds.ToArray(), CellW + w * 2f, y, w, OutsideH);
        Cell("Negro", Enumerable.Range(1, 36).Where(n => !IsRed(n)).ToArray(), CellW + w * 3f, y, w, OutsideH);
        Cell("Impar", Enumerable.Range(1, 36).Where(n => n % 2 == 1).ToArray(), CellW + w * 4f, y, w, OutsideH);
        Cell("Pasa (19-36)", Enumerable.Range(19, 18).ToArray(), CellW + w * 5f, y, w, OutsideH);

        // Apuestas en los bordes (van encima de las casillas).
        for (int c = 0; c < 12; c++)
        {
            float x0 = CellW * (c + 1);
            for (int r = 0; r < 3; r++)
            {
                int n = 3 * c + 3 - r;
                if (r < 2) Edge(new[] { n, n - 1 }, x0 + CellW * 0.5f, CellH * (r + 1), 44f, 22f);               // caballo vertical
                if (c < 11) Edge(new[] { n, n + 3 }, x0 + CellW, CellH * (r + 0.5f), 22f, 56f);                   // caballo horizontal
                if (r < 2 && c < 11) Edge(new[] { n, n - 1, n + 3, n + 2 }, x0 + CellW, CellH * (r + 1), 24f, 24f); // esquina
            }
            Edge(new[] { 3 * c + 1, 3 * c + 2, 3 * c + 3 }, x0 + CellW * 0.5f, CellH * 3f, 44f, 22f);              // calle
            if (c < 11) Edge(Enumerable.Range(3 * c + 1, 6).ToArray(), x0 + CellW, CellH * 3f, 24f, 24f);       // línea
        }
        for (int r = 0; r < 3; r++) Edge(new[] { 0, 3 - r }, CellW, CellH * (r + 0.5f), 22f, 56f);               // caballos con el 0
        Edge(new[] { 0, 2, 3 }, CellW, CellH, 24f, 24f);
        Edge(new[] { 0, 1, 2 }, CellW, CellH * 2f, 24f, 24f);
        Edge(new[] { 0, 1, 2, 3 }, CellW, CellH * 3f, 24f, 24f);                                                  // cuatro primeros
    }

    // Dibuja el paño del tablero: casillas en relieve, líneas doradas, cifras en pixel art y el canto de madera.
    private static PixelCanvas PaintBoard()
    {
        int bw = TW * 14, bh = TH * 3 + TO * 2;
        var c = new PixelCanvas(bw + 3, bh + 9);
        c.Clear(new Color32(0, 0, 0, 0));

        Color32 white = Color.white, shadow = new Color(0f, 0f, 0f, 0.55f);
        for (int y = bh; y < bh + 6; y++)          // canto frontal de madera (grosor del tablero)
            for (int x = 0; x < bw; x++)
                c.Set(x, y, PixelCanvas.Shade(Casino.Wood, y == bh ? 1.25f : 1f - (y - bh) * 0.1f, x, y));
        c.Rect(3, bh + 6, bw, 3, new Color(0f, 0f, 0f, 0.35f));   // sombra en la mesa
        c.Rect(bw, 3, 3, bh + 3, new Color(0f, 0f, 0f, 0.35f));

        c.Rect(0, 0, bw, bh, PixelCanvas.Shade(Casino.GoldTrim, 0.8f, 0, 0));
        c.Bevel(1, 1, TW - 1, TH * 3 - 1, Casino.GreenPocket);
        c.TextCentered("0", TW / 2 + 1, TH * 3 / 2, white, 2, shadow);
        for (int n = 1; n <= 36; n++)
        {
            int col = (n - 1) / 3, row = 2 - (n - 1) % 3;
            int x = TW * (col + 1), y = TH * row;
            c.Bevel(x + 1, y + 1, TW - 1, TH - 1, PocketColor(n));
            c.TextCentered(n.ToString(), x + TW / 2 + 1, y + TH / 2, white, 2, shadow);
        }
        for (int r = 0; r < 3; r++)
        {
            c.Bevel(TW * 13 + 1, TH * r + 1, TW - 1, TH - 1, Casino.Felt, 1.3f, 0.65f, true);
            c.TextCentered("2:1", TW * 13 + TW / 2 + 1, TH * r + TH / 2, white, 2, shadow);
        }
        string[] dozens = { "1-12", "13-24", "25-36" };
        for (int d = 0; d < 3; d++)
        {
            int x = TW * (1 + d * 4), y = TH * 3;
            c.Bevel(x + 1, y + 1, TW * 4 - 1, TO - 1, Casino.Felt, 1.3f, 0.65f, true);
            c.TextCentered(dozens[d], x + TW * 2 + 1, y + TO / 2 + 1, white, 2, shadow);
        }
        string[] outside = { "1-18", "PAR", "", "", "IMPAR", "19-36" };
        for (int i = 0; i < 6; i++)
        {
            int x = TW + i * TW * 2, y = TH * 3 + TO;
            c.Bevel(x + 1, y + 1, TW * 2 - 1, TO - 1, Casino.Felt, 1.3f, 0.65f, true);
            if (outside[i] != "") c.TextCentered(outside[i], x + TW + 1, y + TO / 2 + 1, white, 2, shadow);
            else Diamond(c, x + TW, y + TO / 2 + 1, i == 2 ? Casino.RedPocket : Casino.BlackPocket);
        }
        // Esquinas vacías del paño.
        c.Bevel(1, TH * 3 + 1, TW - 1, TO * 2 - 1, Color.Lerp(Color.black, Casino.Felt, 0.8f), 1.1f, 0.7f, true);
        c.Bevel(TW * 13 + 1, TH * 3 + 1, TW - 1, TO * 2 - 1, Color.Lerp(Color.black, Casino.Felt, 0.8f), 1.1f, 0.7f, true);
        c.Apply();
        return c;
    }

    private static void Diamond(PixelCanvas c, int cx, int cy, Color color)
    {
        for (int dy = -6; dy <= 6; dy++)
        {
            int half = Mathf.RoundToInt(10f * (1f - Mathf.Abs(dy) / 6.5f));
            for (int dx = -half; dx <= half; dx++)
            {
                bool edge = Mathf.Abs(dx) >= half || Mathf.Abs(dy) == 6;
                Color col = edge ? new Color(0.95f, 0.85f, 0.6f) : color;   // contorno claro para que el negro se vea sobre el paño
                float k = edge ? 1f : dy < 0 && dx < 0 ? 1.35f : dy > 0 && dx > 0 ? 0.75f : 1f;
                c.Set(cx + dx, cy + dy, PixelCanvas.Shade(col, k, cx + dx, cy + dy));
            }
        }
    }

    private static RectTransform PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        return UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
    }

    private void Cell(string name, int[] nums, float x, float y, float w, float h)
    {
        Image zone = UIFactory.Image(board, "Cell " + name, PixelZDatabase.I.white, new Color(1f, 0.9f, 0.4f, 0f));
        PlaceTL(zone.rectTransform, x + 2f, y + 2f, w - 3f, h - 3f);
        zone.raycastTarget = true;
        if (nums.Length == 1) cellHighlights[nums[0]] = zone;
        Zone(zone, nums, nums.Length == 1 ? "Pleno al " + name : name, new Vector2(x + w * 0.5f, y + h * 0.5f));
    }

    private void Edge(int[] nums, float cx, float cy, float w, float h)
    {
        Image hit = UIFactory.Image(board, "Edge", PixelZDatabase.I.white, new Color(1f, 0.9f, 0.4f, 0f));
        PlaceTL(hit.rectTransform, cx - w * 0.5f, cy - h * 0.5f, w, h);
        hit.raycastTarget = true;
        string kind = nums.Length == 2 ? "Caballo" : nums.Length == 3 ? "Calle" : nums.Length == 4 ? "Esquina" : "Línea";
        Zone(hit, nums, kind + " " + string.Join("-", nums.OrderBy(n => n)), new Vector2(cx, cy));
    }

    private void Zone(Image img, int[] nums, string name, Vector2 center)
    {
        string key = string.Join(",", nums.OrderBy(n => n));
        zones[key] = new Bet { numbers = nums, name = name, center = center };
        var zone = img.gameObject.AddComponent<BetZone>();
        zone.onAdd = () => AddBet(key, amountInput.Value);
        zone.onRemove = () => RemoveBet(key);
        zone.onEnter = () => Hover(nums, img, true);
        zone.onExit = () => Hover(nums, img, false);
    }

    // Al pasar el ratón se iluminan los números que cubre la apuesta.
    private void Hover(int[] nums, Image zoneImage, bool on)
    {
        if (nums.Length > 1) zoneImage.color = new Color(1f, 0.9f, 0.4f, on ? 0.35f : 0f);
        foreach (int n in nums)
        {
            if (!cellHighlights.TryGetValue(n, out Image img)) continue;
            img.color = new Color(1f, 0.9f, 0.4f, on ? 0.35f : IsLastResult(n) ? 0.55f : 0f);
        }
    }

    private bool IsLastResult(int n) => !spinning && results.Count > 0 && results[0] == n;

    // ---------- Apuestas ----------

    private void AddBet(string key, int amount)
    {
        if (spinning || !zones.TryGetValue(key, out Bet zone)) return;
        if (GameSession.Coins < amount)
        {
            Casino.Toast(GameSession.Coins <= 0 ? "No te quedan monedas" : "Sólo tienes " + GameSession.Coins + " monedas", Casino.Warning);
            return;
        }
        GameSession.Coins -= amount;

        if (!bets.TryGetValue(key, out Bet bet))
        {
            bet = new Bet { numbers = zone.numbers, name = zone.name, center = zone.center };
            bets[key] = bet;
        }
        bet.amount += amount;
        if (bet.marker != null) Destroy(bet.marker.gameObject);
        bet.marker = Casino.CoinStack(board, bet.amount, 34f);
        PlaceTL(bet.marker, bet.center.x - 17f, bet.center.y - 17f, 34f, 34f);
        RefreshTotal();
    }

    private void RemoveBet(string key)
    {
        if (spinning || !bets.TryGetValue(key, out Bet bet)) return;
        GameSession.Coins += bet.amount;
        if (bet.marker != null) Destroy(bet.marker.gameObject);
        bets.Remove(key);
        RefreshTotal();
    }

    private void ClearBets()
    {
        if (spinning) return;
        foreach (string key in bets.Keys.ToList()) RemoveBet(key);
    }

    private void RepeatBets()
    {
        if (spinning) return;
        if (lastBets.Count == 0)
        {
            Casino.Toast("Aún no hay una apuesta anterior", Casino.Warning);
            return;
        }
        int needed = lastBets.Sum(b => b.Value);
        if (GameSession.Coins < needed)
        {
            Casino.Toast("Necesitas " + needed + " monedas para repetir", Casino.Warning);
            return;
        }
        foreach (var b in lastBets) AddBet(b.Key, b.Value);
    }

    private void DoubleBets()
    {
        if (spinning || bets.Count == 0) return;
        int needed = bets.Values.Sum(b => b.amount);
        if (GameSession.Coins < needed)
        {
            Casino.Toast("Necesitas " + needed + " monedas más para doblar", Casino.Warning);
            return;
        }
        foreach (var b in bets.ToList()) AddBet(b.Key, b.Value.amount);
    }

    private void RefreshTotal()
    {
        int total = bets.Values.Sum(b => b.amount);
        totalText.text = total > 0 ? "En la mesa: " + total + " monedas" : "Sin apuestas";
    }

    // ---------- Tirada ----------

    private void Spin()
    {
        if (spinning) return;
        if (bets.Count == 0)
        {
            Casino.Toast("Primero apuesta en alguna casilla del tablero", Casino.Warning);
            return;
        }

        pendingResult = Random.Range(0, 37);
        pendingStake = bets.Values.Sum(b => b.amount);
        pendingWin = 0;
        foreach (Bet b in bets.Values)
            if (b.numbers.Contains(pendingResult)) pendingWin += b.amount * (36 / b.numbers.Length);

        // El premio se cobra ya (por si se cierra la tienda a mitad), pero el saldo mostrado espera al final.
        Casino.HeldCoins = GameSession.Coins;
        GameSession.Coins += pendingWin;
        GameSession.CoinsCollected += Mathf.Max(0, pendingWin - pendingStake);
        lastBets = bets.Select(b => new KeyValuePair<string, int>(b.Key, b.Value.amount)).ToList();

        // La bola da entre 6 y 7 vueltas (respecto a la rueda) en sentido contrario y acaba en su casilla.
        float pocketAngle = WheelRenderer.PocketAngle(pendingResult);
        relStart = hasBall ? ballRel : Random.Range(0f, 360f);
        relTarget = relStart - (Mathf.Repeat(relStart - pocketAngle, 360f) + 360f * 6f);
        hasBall = true;
        spinT = 0f;
        spinning = true;
        landed = false;
        lastBounce = 0f;
        lastFret = Mathf.FloorToInt(relStart / WheelRenderer.Seg);
        foreach (Image img in cellHighlights.Values) img.color = new Color(1f, 0.9f, 0.4f, 0f);
        resultText.text = "No va más...";
        resultText.color = UIFactory.Cream;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (spinning)
        {
            spinT += dt / SpinTime;
            float k = Mathf.Clamp01(spinT);
            wheelDeg += (IdleWheelSpeed + 120f * (1f - k) * (1f - k)) * dt;
            float e = 1f - Mathf.Pow(1f - k, 3f);
            ballRel = Mathf.Lerp(relStart, relTarget, e);
            float ballSpeed = 3f * Mathf.Pow(1f - k, 2f);   // velocidad relativa (1 = muy rápida)

            // Gira por la pista exterior, cae hacia las casillas rebotando y se asienta.
            if (k < 0.42f)
            {
                ballR = Mathf.Lerp(0.935f, 0.92f, k / 0.42f);
                ballHop = 0f;
                // Rueda por la pista: zumbido continuo que se apaga al ir frenando.
                Sfx.RollingLoop(Mathf.Clamp01(ballSpeed) * 0.55f, 0.75f + 0.4f * Mathf.Clamp01(ballSpeed));
            }
            else if (k < 0.8f)
            {
                float s = (k - 0.42f) / 0.38f;
                float bounce = Mathf.Abs(Mathf.Sin(s * Mathf.PI * 4.5f)) * Mathf.Pow(1f - s, 1.5f);
                ballR = Mathf.Lerp(0.92f, PocketRadius, Mathf.SmoothStep(0f, 1f, s)) + 0.05f * bounce;
                ballHop = 7f * bounce;
                Sfx.RollingLoop(Mathf.Clamp01(1f - s * 2.5f) * 0.4f, 0.8f);

                // Al caer va chocando con los separadores (tic) y rebota.
                int fret = Mathf.FloorToInt(ballRel / WheelRenderer.Seg);
                if (fret != lastFret)
                {
                    lastFret = fret;
                    Sfx.Play(Sfx.Clip.RouletteTick, 0.35f + 0.5f * (1f - s));
                }
                if (bounce < 0.05f && lastBounce >= 0.05f) Sfx.Play(Sfx.Clip.RouletteTick, 0.9f * (1f - s) + 0.2f);
                lastBounce = bounce;
            }
            else
            {
                ballR = PocketRadius;
                ballHop = 0f;
                if (!landed)
                {
                    landed = true;
                    Sfx.RollingLoop(0f, 1f);
                    Sfx.Play(Sfx.Clip.RouletteDrop, 0.9f);
                }
            }
            if (k >= 1f) FinishSpin();
        }
        else wheelDeg += IdleWheelSpeed * dt;
        RenderWheel();
    }

    private void RenderWheel()
    {
        wheelRenderer.Render(wheelDeg, wheelDeg + ballRel, ballR, ballHop, hasBall);
    }

    private void FinishSpin()
    {
        spinning = false;
        Sfx.RollingLoop(0f, 1f);
        Casino.HeldCoins = null;
        int n = pendingResult;
        ballRel = relTarget;
        ballR = PocketRadius;
        ballHop = 0f;

        foreach (Bet b in bets.Values)
            if (b.marker != null) Destroy(b.marker.gameObject);
        bets.Clear();
        RefreshTotal();

        results.Insert(0, n);
        if (results.Count > MaxHistory) results.RemoveAt(results.Count - 1);
        RebuildHistory();
        if (cellHighlights.TryGetValue(n, out Image hl)) hl.color = new Color(1f, 0.9f, 0.4f, 0.55f);

        string color = n == 0 ? "VERDE" : IsRed(n) ? "ROJO" : "NEGRO";
        string extra = n == 0 ? "" : (n % 2 == 0 ? " · PAR" : " · IMPAR") + (n <= 18 ? " · 1-18" : " · 19-36");
        string head = "Salió el <color=#ffd24d>" + n + " " + color + "</color>" + extra + "\n";
        int net = pendingWin - pendingStake;
        if (pendingWin > 0)
        {
            resultText.text = head + "¡Cobras " + pendingWin + " monedas! (" + (net >= 0 ? "+" : "") + net + ")";
            resultText.color = new Color(0.6f, 1f, 0.6f);
            Sfx.Play(Sfx.Clip.Coin);
        }
        else
        {
            resultText.text = head + "Pierdes " + pendingStake + " monedas";
            resultText.color = new Color(1f, 0.6f, 0.55f);
        }
    }

    private void RebuildHistory()
    {
        for (int i = history.childCount - 1; i >= 0; i--) Destroy(history.GetChild(i).gameObject);
        for (int i = 0; i < results.Count; i++)
        {
            int n = results[i];
            Image box = UIFactory.Panel(history, "Result", PocketColor(n), i == 0);
            box.rectTransform.sizeDelta = new Vector2(56f, 38f);
            Text t = UIFactory.Text(box.transform, n.ToString(), 24, Color.white);
            UIFactory.Stretch(t.rectTransform);
        }
    }

    // Al salir de la ruleta: si estaba girando se termina al instante; lo apostado sin jugar se devuelve.
    private void OnDisable()
    {
        if (spinning) FinishSpin();
        foreach (Bet b in bets.Values)
        {
            GameSession.Coins += b.amount;
            if (b.marker != null) Destroy(b.marker.gameObject);
        }
        bets.Clear();
        if (totalText != null) RefreshTotal();
    }
}

// Dibuja la rueda de la ruleta vista en perspectiva, píxel a píxel: aro de madera con canto, pista de la bola,
// anillo de números, casillas con separadores dorados y el cono central con su torreta. También la bola y su sombra.
public class WheelRenderer
{
    public static readonly int[] Order =
    {
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
        5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
    };
    public static readonly float Seg = 360f / Order.Length;

    private const float Tilt = 0.5f;          // aplastamiento vertical: cuánto se inclina la rueda hacia el jugador
    private const float RimOut = 1.06f;

    public static float PocketAngle(int number) => System.Array.IndexOf(Order, number) * Seg;

    private readonly PixelCanvas c;
    private readonly float cx, cy, R, ry, depth;

    public WheelRenderer(PixelCanvas canvas, float centerX, float centerY, float radius)
    {
        c = canvas;
        cx = centerX;
        cy = centerY;
        R = radius;
        ry = radius * Tilt;
        depth = radius * 0.11f;
    }

    public void Render(float wheelDeg, float ballDeg, float ballR, float hop, bool showBall, bool numbers = true)
    {
        Color32[] px = c.Pixels;
        for (int y = 0; y < c.H; y++)
        {
            float py = cy - (y + 0.5f);
            for (int x = 0; x < c.W; x++)
            {
                float u = (x + 0.5f - cx) / R, v = py / ry;
                float r = Mathf.Sqrt(u * u + v * v);
                Color32 col;
                if (r <= RimOut) col = Top(u, v, r, wheelDeg, x, y);
                else
                {
                    float v2 = (py + depth) / ry;
                    float r2 = Mathf.Sqrt(u * u + v2 * v2);
                    if (r2 <= RimOut)   // canto de la rueda (le da volumen)
                    {
                        float k = 0.55f + 0.3f * (1f - Mathf.Abs(u)) + ((y & 1) == 0 ? 0.05f : 0f);
                        col = PixelCanvas.Shade(Casino.Wood, k * 0.8f, x, y);
                    }
                    else
                    {
                        float v3 = (py + depth + 4f) / ry;
                        col = Mathf.Sqrt(u * u * 0.95f + v3 * v3) <= RimOut + 0.02f ? new Color32(0, 0, 0, 90) : new Color32(0, 0, 0, 0);
                    }
                }
                px[(c.H - 1 - y) * c.W + x] = col;
            }
        }

        // Números sobre su anillo (siempre derechos para que se lean).
        for (int i = 0; numbers && i < Order.Length; i++)
        {
            float a = (wheelDeg + i * Seg) * Mathf.Deg2Rad;
            int nx = Mathf.RoundToInt(cx + Mathf.Sin(a) * R * 0.8f);
            int ny = Mathf.RoundToInt(cy - Mathf.Cos(a) * ry * 0.8f);
            c.TextCentered(Order[i].ToString(), nx, ny, Color.white);
        }

        if (showBall)
        {
            float a = ballDeg * Mathf.Deg2Rad;
            float bx = cx + Mathf.Sin(a) * R * ballR;
            float by = cy - Mathf.Cos(a) * ry * ballR;
            c.Disc(bx + 1.5f, by + 2f, 3.8f, new Color(0f, 0f, 0f, 0.5f));
            DrawBall(bx, by - hop, 4.3f);
        }
        c.Apply();
    }

    private void DrawBall(float bx, float by, float rad)
    {
        for (int y = Mathf.FloorToInt(by - rad); y <= Mathf.CeilToInt(by + rad); y++)
            for (int x = Mathf.FloorToInt(bx - rad); x <= Mathf.CeilToInt(bx + rad); x++)
            {
                float dx = (x + 0.5f - bx) / rad, dy = (y + 0.5f - by) / rad;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;
                float light = Mathf.Clamp01(1f - ((dx + 0.45f) * (dx + 0.45f) + (dy + 0.45f) * (dy + 0.45f)) * 0.8f);
                Color col = Color.Lerp(new Color(0.55f, 0.55f, 0.62f), Color.white, 0.35f + 0.65f * light);
                if (d > 0.7f) col *= 0.75f;
                col.a = 1f;
                c.Set(x, y, col);
            }
    }

    private Color32 Top(float u, float v, float r, float wheelDeg, int x, int y)
    {
        float light = Mathf.Clamp(-u * 0.5f + v * 0.5f, -1f, 1f);   // luz desde arriba a la izquierda

        if (r > 0.99f)    // aro exterior de madera, más oscuro en el borde
        {
            float t = (r - 0.99f) / (RimOut - 0.99f);
            return PixelCanvas.Shade(Casino.Wood, 1.15f - 0.45f * t + 0.15f * light, x, y);
        }
        if (r > 0.97f) return PixelCanvas.Shade(Casino.GoldTrim, 0.95f + 0.25f * light, x, y);
        if (r > 0.885f)   // pista de la bola, madera pulida con brillo
        {
            float shine = 1f - Mathf.Abs(r - 0.93f) / 0.045f;
            return PixelCanvas.Shade(new Color(0.78f, 0.52f, 0.28f), 0.8f + 0.25f * shine + 0.2f * v, x, y);
        }
        if (r > 0.865f) return PixelCanvas.Shade(new Color(0.3f, 0.17f, 0.08f), 1f, x, y);

        float theta = Mathf.Atan2(u, v) * Mathf.Rad2Deg - wheelDeg;
        float within = Mathf.Repeat(theta + Seg * 0.5f, Seg);
        float fretDist = Mathf.Min(within, Seg - within) * Mathf.Deg2Rad * r;
        int pocket = (int)(Mathf.Repeat(theta + Seg * 0.5f, 360f) / Seg) % Order.Length;
        Color pc = Roulette.PocketColor(Order[pocket]);

        if (r > 0.74f)    // anillo de números
        {
            if (fretDist < 0.011f) return PixelCanvas.Shade(Casino.GoldTrim, 1f + 0.2f * light, x, y);
            return PixelCanvas.Shade(pc, 1f + 0.15f * light, x, y);
        }
        if (r > 0.6f)     // casillas hundidas (más oscuras, con la pared exterior en sombra)
        {
            if (fretDist < 0.016f) return PixelCanvas.Shade(Casino.GoldTrim, 1.05f + 0.2f * light, x, y);
            float wall = r > 0.715f ? 0.7f : 1f;
            return PixelCanvas.Shade(pc, 0.62f * wall + 0.1f * light, x, y);
        }
        if (r > 0.58f) return PixelCanvas.Shade(Casino.GoldTrim, 0.9f + 0.2f * light, x, y);

        // Cono central con brazos dorados que giran con la rueda.
        if (r < 0.09f)
        {
            float hl = 1f - Mathf.Clamp01(((u + 0.03f) * (u + 0.03f) + (v - 0.03f) * (v - 0.03f)) / 0.006f);
            return PixelCanvas.Shade(Casino.GoldTrim, 0.9f + 0.5f * hl, x, y);
        }
        float arm = Mathf.Repeat(theta, 90f);
        float armDist = Mathf.Min(arm, 90f - arm) * Mathf.Deg2Rad * r;
        if (r < 0.44f && armDist < 0.028f) return PixelCanvas.Shade(Casino.GoldTrim, 0.85f + 0.3f * light, x, y);
        if (r >= 0.44f && r < 0.5f && armDist < 0.05f) return PixelCanvas.Shade(Casino.GoldTrim, 1.1f + 0.2f * light, x, y);
        float dome = 1f - r / 0.58f;
        return PixelCanvas.Shade(new Color(0.5f, 0.28f, 0.12f), 0.65f + 0.45f * dome * (0.7f + 0.5f * light), x, y);
    }
}
