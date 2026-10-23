using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Ruleta europea (un solo cero) con el tablero completo: plenos, caballos (2), calles (3), esquinas (4),
// línea (6), docenas, columnas, rojo/negro, par/impar y falta/pasa. Pago = 36 / números - 1 a 1.
public class Roulette : MonoBehaviour
{
    private static readonly int[] WheelOrder =
    {
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
        5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
    };
    private static readonly HashSet<int> Reds = new HashSet<int> { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

    public static bool IsRed(int n) => Reds.Contains(n);
    public static Color PocketColor(int n) => n == 0 ? Casino.GreenPocket : IsRed(n) ? Casino.RedPocket : Casino.BlackPocket;

    private const float CellW = 74f, CellH = 96f, OutsideH = 64f;
    private const float WheelRadius = 290f;
    private static readonly Vector2 WheelCenter = new Vector2(-600f, 40f);
    private const float SpinTime = 5f;
    private const int MaxHistory = 14;

    private class Bet
    {
        public int[] numbers;
        public string name;
        public Vector2 center;
        public int amount;
        public Image chip;
        public Text chipText;
    }

    public bool Busy => spinning;

    private RectTransform board, wheel, ball, history;
    private readonly Dictionary<string, Bet> bets = new Dictionary<string, Bet>();
    private readonly Dictionary<int, Image> cellHighlights = new Dictionary<int, Image>();
    private readonly Dictionary<string, Bet> zones = new Dictionary<string, Bet>();   // todas las apuestas posibles del tablero
    private List<KeyValuePair<string, int>> lastBets = new List<KeyValuePair<string, int>>();
    private readonly List<int> results = new List<int>();
    private Text totalText, resultText;

    private bool spinning;
    private float spinT, wheelFrom, wheelTo, ballTurns;
    private int pendingResult, pendingWin, pendingStake;

    public void Build(RectTransform screen)
    {
        BuildWheel(screen);
        BuildBoard(screen);

        history = UIFactory.Rect("History", screen);
        UIFactory.Place(history, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-300f, 375f), new Vector2(1036f, 44f));
        var hl = history.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 6f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = hl.childControlHeight = false;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        Text histLabel = UIFactory.Text(screen, "Últimos:", 24, UIFactory.Cream, TextAnchor.MiddleRight);
        UIFactory.Place(histLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-310f, 375f), new Vector2(140f, 40f));

        Casino.ChipSelector(screen, new Vector2(-130f, -180f));

        Button spin = UIFactory.Button(screen, "¡GIRAR!", 44, Spin, new Vector2(300f, 92f));
        UIFactory.Place((RectTransform)spin.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(586f, -180f), new Vector2(300f, 92f));
        Button clear = UIFactory.Button(screen, "BORRAR", 30, ClearBets, new Vector2(220f, 70f));
        UIFactory.Place((RectTransform)clear.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-180f, -300f), new Vector2(220f, 70f));
        Button repeat = UIFactory.Button(screen, "REPETIR", 30, RepeatBets, new Vector2(220f, 70f));
        UIFactory.Place((RectTransform)repeat.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60f, -300f), new Vector2(220f, 70f));
        Button twice = UIFactory.Button(screen, "x2 APUESTA", 30, DoubleBets, new Vector2(240f, 70f));
        UIFactory.Place((RectTransform)twice.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(310f, -300f), new Vector2(240f, 70f));

        totalText = UIFactory.Text(screen, "", 32, UIFactory.Gold);
        UIFactory.Place(totalText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-600f, -330f), new Vector2(560f, 50f));

        resultText = UIFactory.Text(screen, "Haz tus apuestas y pulsa ¡GIRAR!", 36, UIFactory.Cream);
        UIFactory.Place(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -395f), new Vector2(1700f, 56f));

        Text help = UIFactory.Text(screen, "Clic izq. pone una ficha  ·  Clic der. la quita  ·  Clic en los bordes entre números para caballos, calles, esquinas y líneas", 22, new Color(1f, 1f, 1f, 0.6f));
        UIFactory.Place(help.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -460f), new Vector2(1760f, 34f));

        RefreshTotal();
    }

    // ---------- Rueda ----------

    private void BuildWheel(RectTransform screen)
    {
        Vector2 center = WheelCenter;
        Image rim = UIFactory.Image(screen, "Wheel Rim", Casino.Disc, new Color(0.45f, 0.27f, 0.1f));
        UIFactory.Place(rim.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, Vector2.one * (WheelRadius * 2f + 44f));
        Image gold = UIFactory.Image(screen, "Wheel Gold", Casino.Disc, UIFactory.Gold);
        UIFactory.Place(gold.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, Vector2.one * (WheelRadius * 2f + 6f));

        wheel = UIFactory.Rect("Wheel", screen);
        UIFactory.Place(wheel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, Vector2.one * WheelRadius * 2f);

        float seg = 360f / WheelOrder.Length;
        for (int i = 0; i < WheelOrder.Length; i++)
        {
            int n = WheelOrder[i];
            Image wedge = UIFactory.Image(wheel, "Pocket " + n, Casino.Disc, PocketColor(n));
            UIFactory.Stretch(wedge.rectTransform);
            wedge.type = Image.Type.Filled;
            wedge.fillMethod = Image.FillMethod.Radial360;
            wedge.fillOrigin = (int)Image.Origin360.Top;
            wedge.fillClockwise = true;
            wedge.fillAmount = 1f / WheelOrder.Length - 0.0012f;   // deja ver una línea dorada entre casillas
            wedge.rectTransform.localEulerAngles = new Vector3(0f, 0f, -i * seg + seg * 0.5f);

            float a = i * seg * Mathf.Deg2Rad;
            Text label = UIFactory.Text(wheel, n.ToString(), 22, Color.white, TextAnchor.MiddleCenter, false);
            UIFactory.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * WheelRadius * 0.87f, new Vector2(44f, 30f));
            label.rectTransform.localEulerAngles = new Vector3(0f, 0f, -i * seg);
        }

        Image track = UIFactory.Image(wheel, "Ball Track", Casino.Disc, new Color(0.3f, 0.18f, 0.08f));
        UIFactory.Place(track.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * WheelRadius * 1.5f);
        Image hub = UIFactory.Image(wheel, "Hub", Casino.Disc, new Color(0.5f, 0.32f, 0.12f));
        UIFactory.Place(hub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * WheelRadius * 0.7f);
        Image cap = UIFactory.Image(wheel, "Cap", Casino.Disc, UIFactory.Gold);
        UIFactory.Place(cap.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * 60f);

        // Puntero fijo arriba: la casilla que queda debajo es la ganadora.
        Image pointer = UIFactory.Image(screen, "Pointer", PixelZDatabase.I.white, UIFactory.Gold);
        UIFactory.Place(pointer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center + new Vector2(0f, WheelRadius + 14f), new Vector2(30f, 30f));
        pointer.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

        Image ballImg = UIFactory.Image(screen, "Ball", Casino.Disc, Color.white);
        ball = ballImg.rectTransform;
        UIFactory.Place(ball, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(24f, 24f));
        PlaceBall(0f);
    }

    private void PlaceBall(float clockwiseDeg)
    {
        float a = clockwiseDeg * Mathf.Deg2Rad;
        ball.anchoredPosition = WheelCenter + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * WheelRadius * 0.77f;
    }

    // ---------- Tablero ----------

    private void BuildBoard(RectTransform screen)
    {
        board = UIFactory.Rect("Board", screen);
        UIFactory.Place(board, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(-300f, 330f), new Vector2(CellW * 14f, CellH * 3f + OutsideH * 2f));
        Image felt = UIFactory.Image(board, "Felt", PixelZDatabase.I.white, Casino.Felt);
        UIFactory.Stretch(felt.rectTransform, -10f);

        // Cero y números.
        Cell("0", new[] { 0 }, 0f, 0f, CellW, CellH * 3f, Casino.GreenPocket, 34);
        for (int n = 1; n <= 36; n++)
        {
            int c = (n - 1) / 3, r = 2 - (n - 1) % 3;
            Cell(n.ToString(), new[] { n }, CellW * (c + 1), CellH * r, CellW, CellH, PocketColor(n), 32);
        }

        // Columnas (2 a 1).
        for (int r = 0; r < 3; r++)
        {
            int mod = r == 0 ? 0 : r == 1 ? 2 : 1;
            int[] nums = Enumerable.Range(1, 36).Where(n => n % 3 == mod).ToArray();
            Cell("2:1", nums, CellW * 13f, CellH * r, CellW, CellH, Casino.Felt, 26, (3 - r) + "ª columna");
        }

        // Docenas.
        string[] dozens = { "1ª DOCENA", "2ª DOCENA", "3ª DOCENA" };
        for (int d = 0; d < 3; d++)
            Cell(dozens[d], Enumerable.Range(d * 12 + 1, 12).ToArray(), CellW * (1 + d * 4), CellH * 3f, CellW * 4f, OutsideH, Casino.Felt, 26);

        // Apuestas sencillas.
        float y = CellH * 3f + OutsideH, w = CellW * 2f;
        Cell("1-18", Enumerable.Range(1, 18).ToArray(), CellW, y, w, OutsideH, Casino.Felt, 26, "Falta (1-18)");
        Cell("PAR", Enumerable.Range(1, 36).Where(n => n % 2 == 0).ToArray(), CellW + w, y, w, OutsideH, Casino.Felt, 26);
        Cell("ROJO", Reds.ToArray(), CellW + w * 2f, y, w, OutsideH, Casino.RedPocket, 26);
        Cell("NEGRO", Enumerable.Range(1, 36).Where(n => !IsRed(n)).ToArray(), CellW + w * 3f, y, w, OutsideH, Casino.BlackPocket, 26);
        Cell("IMPAR", Enumerable.Range(1, 36).Where(n => n % 2 == 1).ToArray(), CellW + w * 4f, y, w, OutsideH, Casino.Felt, 26);
        Cell("19-36", Enumerable.Range(19, 18).ToArray(), CellW + w * 5f, y, w, OutsideH, Casino.Felt, 26, "Pasa (19-36)");

        // Apuestas en los bordes (van encima de las casillas).
        for (int c = 0; c < 12; c++)
        {
            float x0 = CellW * (c + 1);
            for (int r = 0; r < 3; r++)
            {
                int n = 3 * c + 3 - r;
                if (r < 2) Edge(new[] { n, n - 1 }, x0 + CellW * 0.5f, CellH * (r + 1), 48f, 22f);               // caballo vertical
                if (c < 11) Edge(new[] { n, n + 3 }, x0 + CellW, CellH * (r + 0.5f), 22f, 60f);                   // caballo horizontal
                if (r < 2 && c < 11) Edge(new[] { n, n - 1, n + 3, n + 2 }, x0 + CellW, CellH * (r + 1), 24f, 24f); // esquina
            }
            Edge(new[] { 3 * c + 1, 3 * c + 2, 3 * c + 3 }, x0 + CellW * 0.5f, CellH * 3f, 48f, 22f);              // calle
            if (c < 11) Edge(Enumerable.Range(3 * c + 1, 6).ToArray(), x0 + CellW, CellH * 3f, 24f, 24f);       // línea
        }
        for (int r = 0; r < 3; r++) Edge(new[] { 0, 3 - r }, CellW, CellH * (r + 0.5f), 22f, 60f);               // caballos con el 0
        Edge(new[] { 0, 2, 3 }, CellW, CellH, 24f, 24f);
        Edge(new[] { 0, 1, 2 }, CellW, CellH * 2f, 24f, 24f);
        Edge(new[] { 0, 1, 2, 3 }, CellW, CellH * 3f, 24f, 24f);                                                  // cuatro primeros
    }

    private static RectTransform PlaceTL(RectTransform rt, float x, float y, float w, float h)
    {
        return UIFactory.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, h));
    }

    private void Cell(string label, int[] nums, float x, float y, float w, float h, Color color, int fontSize, string betName = null)
    {
        Image bg = UIFactory.Image(board, "Cell " + label, PixelZDatabase.I.white, new Color(0.95f, 0.85f, 0.6f, 0.9f));
        PlaceTL(bg.rectTransform, x, y, w, h);
        Image face = UIFactory.Image(bg.transform, "Face", PixelZDatabase.I.white, color);
        UIFactory.Stretch(face.rectTransform, 2f);
        face.raycastTarget = true;
        Text t = UIFactory.Text(bg.transform, label, fontSize, Color.white);
        UIFactory.Stretch(t.rectTransform);

        Image glow = UIFactory.Image(bg.transform, "Highlight", PixelZDatabase.I.white, new Color(1f, 0.9f, 0.4f, 0f));
        UIFactory.Stretch(glow.rectTransform, 2f);
        if (nums.Length == 1) cellHighlights[nums[0]] = glow;

        string name = betName ?? (nums.Length == 1 ? "Pleno al " + label : label);
        Zone(face.gameObject, nums, name, new Vector2(x + w * 0.5f, y + h * 0.5f), glow);
    }

    private void Edge(int[] nums, float cx, float cy, float w, float h)
    {
        Image hit = UIFactory.Image(board, "Edge", PixelZDatabase.I.white, new Color(1f, 0.9f, 0.4f, 0f));
        PlaceTL(hit.rectTransform, cx - w * 0.5f, cy - h * 0.5f, w, h);
        hit.raycastTarget = true;
        string kind = nums.Length == 2 ? "Caballo" : nums.Length == 3 ? "Calle" : nums.Length == 4 ? "Esquina" : "Línea";
        string name = kind + " " + string.Join("-", nums.OrderBy(n => n));
        Zone(hit.gameObject, nums, name, new Vector2(cx, cy), hit);
    }

    private void Zone(GameObject go, int[] nums, string name, Vector2 center, Image hoverImage)
    {
        string key = string.Join(",", nums.OrderBy(n => n));
        zones[key] = new Bet { numbers = nums, name = name, center = center };
        var zone = go.AddComponent<BetZone>();
        zone.onAdd = () => AddBet(key, nums, name, center, Casino.SelectedChip);
        zone.onRemove = () => RemoveBet(key);
        zone.onEnter = () => Hover(nums, hoverImage, true);
        zone.onExit = () => Hover(nums, hoverImage, false);
    }

    // Al pasar el ratón se iluminan los números que cubre la apuesta.
    private void Hover(int[] nums, Image zoneImage, bool on)
    {
        if (zoneImage != null && nums.Length > 1 && !cellHighlights.ContainsValue(zoneImage))
            zoneImage.color = new Color(1f, 0.9f, 0.4f, on ? 0.55f : 0f);
        foreach (int n in nums)
        {
            if (!cellHighlights.TryGetValue(n, out Image img)) continue;
            bool lastResult = !spinning && results.Count > 0 && results[0] == n;
            img.color = new Color(1f, 0.9f, 0.4f, on ? 0.35f : lastResult ? 0.6f : 0f);
        }
    }

    // ---------- Apuestas ----------

    private void AddBet(string key, int[] nums, string name, Vector2 center, int amount)
    {
        if (spinning) return;
        if (GameSession.Coins < amount)
        {
            Casino.Toast("No tienes monedas suficientes", new Color(1f, 0.6f, 0.5f));
            return;
        }
        GameSession.Coins -= amount;

        if (!bets.TryGetValue(key, out Bet bet))
        {
            bet = new Bet { numbers = nums, name = name, center = center };
            bets[key] = bet;
        }
        bet.amount += amount;
        if (bet.chip != null) Destroy(bet.chip.gameObject);
        bet.chip = Casino.Chip(board, bet.amount, 42f, out bet.chipText);
        PlaceTL(bet.chip.rectTransform, center.x - 21f, center.y - 21f, 42f, 42f);
        if (bet.amount >= 1000) bet.chipText.fontSize = 13;
        RefreshTotal();
    }

    private void RemoveBet(string key)
    {
        if (spinning || !bets.TryGetValue(key, out Bet bet)) return;
        GameSession.Coins += bet.amount;
        if (bet.chip != null) Destroy(bet.chip.gameObject);
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
            Casino.Toast("Aún no hay una apuesta anterior", new Color(1f, 0.6f, 0.5f));
            return;
        }
        int needed = lastBets.Sum(b => b.Value);
        if (GameSession.Coins < needed)
        {
            Casino.Toast("Necesitas " + needed + " monedas para repetir", new Color(1f, 0.6f, 0.5f));
            return;
        }
        foreach (var b in lastBets) RePlace(b.Key, b.Value);
    }

    private void DoubleBets()
    {
        if (spinning || bets.Count == 0) return;
        int needed = bets.Values.Sum(b => b.amount);
        if (GameSession.Coins < needed)
        {
            Casino.Toast("Necesitas " + needed + " monedas más para doblar", new Color(1f, 0.6f, 0.5f));
            return;
        }
        foreach (var b in bets.ToList()) RePlace(b.Key, b.Value.amount);
    }

    private void RePlace(string key, int amount)
    {
        if (zones.TryGetValue(key, out Bet z)) AddBet(key, z.numbers, z.name, z.center, amount);
    }

    private void RefreshTotal()
    {
        int total = bets.Values.Sum(b => b.amount);
        totalText.text = total > 0 ? "Apostado: " + total : "Sin apuestas";
    }

    // ---------- Tirada ----------

    private void Spin()
    {
        if (spinning) return;
        if (bets.Count == 0)
        {
            Casino.Toast("Pon al menos una ficha en el tablero", new Color(1f, 0.6f, 0.5f));
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

        float seg = 360f / WheelOrder.Length;
        int index = System.Array.IndexOf(WheelOrder, pendingResult);
        wheelFrom = Mathf.Repeat(wheel.localEulerAngles.z, 360f);
        float target = index * seg + Random.Range(-seg * 0.3f, seg * 0.3f);
        wheelTo = target + 360f * 5f;
        while (wheelTo - wheelFrom < 360f * 4f) wheelTo += 360f;
        ballTurns = 7f;
        spinT = 0f;
        spinning = true;
        foreach (Image img in cellHighlights.Values) img.color = new Color(1f, 0.9f, 0.4f, 0f);
        resultText.text = "No va más...";
        resultText.color = UIFactory.Cream;
    }

    private void Update()
    {
        if (!spinning) return;
        spinT += Time.unscaledDeltaTime / SpinTime;
        float k = Mathf.Clamp01(spinT);
        float e = 1f - Mathf.Pow(1f - k, 3f);
        wheel.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(wheelFrom, wheelTo, e));
        // La bola gira en sentido contrario y acaba arriba, junto al puntero.
        PlaceBall(-ballTurns * 360f * (1f - e));
        if (k >= 1f) FinishSpin();
    }

    private void FinishSpin()
    {
        spinning = false;
        Casino.HeldCoins = null;
        int n = pendingResult;
        wheel.localEulerAngles = new Vector3(0f, 0f, Mathf.Repeat(wheelTo, 360f));
        PlaceBall(0f);

        foreach (Bet b in bets.Values)
            if (b.chip != null) Destroy(b.chip.gameObject);
        bets.Clear();
        RefreshTotal();

        results.Insert(0, n);
        if (results.Count > MaxHistory) results.RemoveAt(results.Count - 1);
        RebuildHistory();
        if (cellHighlights.TryGetValue(n, out Image hl)) hl.color = new Color(1f, 0.9f, 0.4f, 0.6f);

        string color = n == 0 ? "VERDE" : IsRed(n) ? "ROJO" : "NEGRO";
        string parity = n == 0 ? "" : n % 2 == 0 ? " · PAR" : " · IMPAR";
        string head = "Salió el <color=#ffd24d>" + n + " " + color + "</color>" + parity + "   ·   ";
        int net = pendingWin - pendingStake;
        if (pendingWin > 0)
        {
            resultText.text = head + "¡Ganas " + pendingWin + " monedas! (" + (net >= 0 ? "+" : "") + net + ")";
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
        foreach (int n in results)
        {
            Image box = UIFactory.Image(history, "Result", PixelZDatabase.I.white, PocketColor(n));
            box.rectTransform.sizeDelta = new Vector2(56f, 40f);
            Text t = UIFactory.Text(box.transform, n.ToString(), 24, Color.white);
            UIFactory.Stretch(t.rectTransform);
        }
    }

    // Al salir de la ruleta: si estaba girando se termina al instante; las fichas sin jugar se devuelven.
    private void OnDisable()
    {
        if (spinning) FinishSpin();
        foreach (Bet b in bets.Values)
        {
            GameSession.Coins += b.amount;
            if (b.chip != null) Destroy(b.chip.gameObject);
        }
        bets.Clear();
        if (totalText != null) RefreshTotal();
    }
}
