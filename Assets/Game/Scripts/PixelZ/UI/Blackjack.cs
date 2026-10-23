using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Blackjack contra el crupier con un zapato de 6 barajas. El crupier se planta en 17.
// Ganar paga 1 a 1, Blackjack 3 a 2 y el empate devuelve la apuesta. Se puede doblar y dividir (hasta 4 manos).
// Las cartas salen del zapato una a una con animación; la lógica corre en un ejecutor propio que, si se cierra
// el casino a mitad de mano, termina la jugada al instante (el jugador se planta y el crupier juega).
public class Blackjack : MonoBehaviour
{
    private enum Phase { Betting, Player, Dealer, Done }

    private const int Decks = 6;
    private const int MaxHands = 4;
    private const int TableScale = 4;
    private const float CardScale = 3.5f;
    private static readonly float CardW = CardArt.W * CardScale, CardH = CardArt.H * CardScale;
    private static readonly Vector2 ShoePos = new Vector2(660f, 300f);
    private const float DealerY = 238f, PlayerY = -25f;

    private class CardView
    {
        public int card;
        public bool faceUp;
        public RectTransform rt;
        public RawImage face;
    }

    private class Hand
    {
        public readonly List<int> cards = new List<int>();
        public readonly List<CardView> views = new List<CardView>();
        public int bet;
        public bool done, splitAces, fromSplit;
        public string tag;
        public Color tagColor;
    }

    public bool Busy => running.Count > 0;

    private readonly List<int> shoe = new List<int>();
    private readonly List<Hand> hands = new List<Hand>();
    private readonly List<int> dealer = new List<int>();
    private readonly List<CardView> dealerViews = new List<CardView>();
    private Phase phase = Phase.Betting;
    private int current;

    private RectTransform cardLayer, handUI;
    private Text dealerTotal, messageText;
    private CoinInput amountInput;
    private Button dealButton, hitButton, standButton, doubleButton, splitButton;

    // Ejecutor de rutinas (como corrutinas, pero se puede terminar de golpe aunque el objeto esté desactivado).
    private readonly Stack<IEnumerator> running = new Stack<IEnumerator>();
    private float waitUntil;
    private bool instant;
    private float Dt => instant ? 10f : Time.unscaledDeltaTime;

    public void Build(RectTransform screen)
    {
        PixelCanvas table = PaintTable();
        RawImage tableImg = table.Show(screen, "Table", TableScale);
        UIFactory.Place(tableImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 415f), new Vector2(table.W * TableScale, table.H * TableScale));

        Text rule = UIFactory.Text(screen, "BLACKJACK PAGA 3 A 2  ·  EL CRUPIER SE PLANTA EN 17", 24, new Color(1f, 0.85f, 0.4f, 0.8f));
        UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 135f), new Vector2(1200f, 34f));
        messageText = UIFactory.Text(screen, "", 36, UIFactory.Cream);
        UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 96f), new Vector2(1500f, 50f));

        Text dLabel = UIFactory.Text(screen, "CRUPIER", 28, UIFactory.Cream);
        UIFactory.Place(dLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360f, DealerY + 25f), new Vector2(240f, 40f));
        dealerTotal = UIFactory.Text(screen, "", 38, UIFactory.Gold);
        UIFactory.Place(dealerTotal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360f, DealerY - 22f), new Vector2(240f, 50f));

        handUI = UIFactory.Stretch(UIFactory.Rect("Hand Labels", screen));
        cardLayer = UIFactory.Stretch(UIFactory.Rect("Cards", screen));

        amountInput = CoinInput.Create(screen, new Vector2(-330f, -325f));
        dealButton = Btn(screen, "REPARTIR", 40, () => Run(DealRoutine()), new Vector2(560f, -325f), new Vector2(320f, 92f));
        hitButton = Btn(screen, "PEDIR", 36, () => Run(HitRoutine()), new Vector2(-405f, -435f), new Vector2(250f, 84f));
        standButton = Btn(screen, "PLANTARSE", 36, () => Run(StandRoutine()), new Vector2(-135f, -435f), new Vector2(270f, 84f));
        doubleButton = Btn(screen, "DOBLAR", 36, () => Run(DoubleRoutine()), new Vector2(135f, -435f), new Vector2(250f, 84f));
        splitButton = Btn(screen, "DIVIDIR", 36, () => Run(SplitRoutine()), new Vector2(405f, -435f), new Vector2(250f, 84f));

        SetMessage("Escribe cuántas monedas apostar y pulsa REPARTIR", UIFactory.Cream);
        RefreshLabels();
    }

    private static Button Btn(Transform parent, string label, int size, System.Action onClick, Vector2 pos, Vector2 dim)
    {
        Button b = UIFactory.Button(parent, label, size, onClick, dim);
        UIFactory.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, dim);
        return b;
    }

    // ---------- Mesa en pixel art ----------

    private static PixelCanvas PaintTable()
    {
        var c = new PixelCanvas(450, 172);
        c.Clear(new Color32(0, 0, 0, 0));
        const float cx = 225f, top = 6f, rx = 220f, ry = 150f, depth = 8f;
        Color leather = new Color(0.32f, 0.15f, 0.09f);
        for (int y = 0; y < c.H; y++)
            for (int x = 0; x < c.W; x++)
            {
                float dx = (x + 0.5f - cx) / rx;
                float e = Mathf.Sqrt(dx * dx + Sq((y + 0.5f - top) / ry));
                if (y < top) continue;
                if (e <= 0.92f && y >= top + 4)
                {
                    float k = 1.12f - 0.4f * e + 0.06f * PixelCanvas.Noise(x, y);
                    bool print = y > 40 && Mathf.Abs(e - 0.6f) * ry < 0.7f;
                    c.Set(x, y, print ? PixelCanvas.Shade(Casino.GoldTrim, 0.75f, x, y) : PixelCanvas.Shade(Casino.Felt, k, x, y));
                }
                else if (e <= 1f || y < top + 4)
                {
                    // Borde acolchado: brillo en el centro del rodillo y más luz por arriba.
                    float t = y < top + 4 && e <= 1f ? (y - top) / 4f : Mathf.Clamp01((e - 0.92f) / 0.08f);
                    if (e > 1f) continue;
                    c.Set(x, y, PixelCanvas.Shade(leather, 0.55f + 0.75f * Mathf.Sin(t * Mathf.PI), x, y));
                }
                else if (Mathf.Sqrt(dx * dx + Sq((y + 0.5f - top - depth) / ry)) <= 1f)
                    c.Set(x, y, PixelCanvas.Shade(Casino.Wood, 0.75f - 0.25f * Mathf.Abs(dx) + ((x / 6) % 2 == 0 ? 0.05f : 0f), x, y));
                else if (Mathf.Sqrt(dx * dx + Sq((y + 0.5f - top - depth - 6f) / ry)) <= 1.01f)
                    c.Set(x, y, new Color(0f, 0f, 0f, 0.35f));
            }

        // Zapato de cartas (de donde salen las cartas) y bandeja de monedas del crupier.
        c.Bevel(371, 20, 42, 24, new Color(0.45f, 0.08f, 0.1f));
        c.Bevel(375, 13, 38, 8, new Color(0.62f, 0.14f, 0.16f));
        for (int y = 22; y < 40; y++) c.Rect(365, y, 6, 1, (y & 1) == 0 ? new Color(0.95f, 0.93f, 0.86f) : new Color(0.7f, 0.68f, 0.62f));
        c.Bevel(37, 18, 34, 22, new Color(0.3f, 0.12f, 0.08f));
        c.Bevel(176, 9, 98, 12, new Color(0.12f, 0.08f, 0.06f), 0.6f, 1.4f);
        for (int s = 0; s < 8; s++)
            for (int k = 0; k < 2 + (s * 7) % 3; k++)
            {
                Color coin = s % 3 == 0 ? new Color(0.85f, 0.85f, 0.9f) : Casino.GoldTrim;
                float px = 183f + s * 12f, py = 16f - k * 2f;
                c.Disc(px, py + 0.6f, 4.2f, coin * 0.55f + new Color(0, 0, 0, 0.45f));
                c.Disc(px, py, 4.2f, coin);
                c.Disc(px - 1.3f, py - 1f, 1.4f, new Color(1f, 1f, 0.85f, 0.7f));
            }
        c.Apply();
        return c;
    }

    private static float Sq(float v) => v * v;

    // ---------- Ejecutor ----------

    private void Run(IEnumerator routine)
    {
        if (running.Count > 0) return;
        running.Push(routine);
        Pump();
    }

    private void Update()
    {
        Pump();
        RefreshButtons();
    }

    private void Pump()
    {
        while (running.Count > 0)
        {
            if (!instant && Time.unscaledTime < waitUntil) return;
            IEnumerator top = running.Peek();
            if (!top.MoveNext())
            {
                running.Pop();
                continue;
            }
            object y = top.Current;
            if (y is IEnumerator sub) { running.Push(sub); continue; }
            if (y is float seconds)
            {
                if (!instant) { waitUntil = Time.unscaledTime + seconds; return; }
                continue;
            }
            if (!instant) return;   // null: siguiente fotograma
        }
    }

    // Al cerrar el casino a mitad de mano: se termina todo al instante y se paga lo que toque.
    private void OnDisable()
    {
        if (hitButton == null) return;
        instant = true;
        Pump();
        if (phase == Phase.Player)
        {
            foreach (Hand h in hands) h.done = true;
            running.Push(DealerRoutine());
            Pump();
        }
        instant = false;
    }

    // ---------- Baraja ----------

    private int Draw()
    {
        if (shoe.Count < 30)
        {
            shoe.Clear();
            for (int i = 0; i < 52 * Decks; i++) shoe.Add(i % 52);
            for (int i = shoe.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (shoe[i], shoe[j]) = (shoe[j], shoe[i]);
            }
            Casino.Toast("El crupier baraja el zapato", UIFactory.Cream);
        }
        int card = shoe[shoe.Count - 1];
        shoe.RemoveAt(shoe.Count - 1);
        return card;
    }

    private static int Rank(int card) => card % 13;
    private static int Value(int card) => Mathf.Min(Rank(card) + 1, 10);

    public static int Total(List<int> hand, out bool soft)
    {
        int total = 0, aces = 0;
        foreach (int c in hand)
        {
            if (Rank(c) == 0) { aces++; total += 11; }
            else total += Value(c);
        }
        while (total > 21 && aces > 0) { total -= 10; aces--; }
        soft = aces > 0;
        return total;
    }

    private static int Total(List<int> hand) => Total(hand, out _);
    private bool IsNatural(Hand h) => hands.Count == 1 && !h.fromSplit && h.cards.Count == 2 && Total(h.cards) == 21;
    private static bool DealerNatural(List<int> d) => d.Count == 2 && Total(d) == 21;

    private Hand Current => current < hands.Count ? hands[current] : null;

    private bool CanSplit(Hand h) =>
        h != null && h.cards.Count == 2 && Value(h.cards[0]) == Value(h.cards[1]) && hands.Count < MaxHands && GameSession.Coins >= h.bet;

    // ---------- Jugadas ----------

    private IEnumerator DealRoutine()
    {
        if (phase == Phase.Player || phase == Phase.Dealer) yield break;
        int bet = amountInput.Value;
        if (bet > GameSession.Coins)
        {
            Casino.Toast(GameSession.Coins <= 0 ? "No te quedan monedas" : "Sólo tienes " + GameSession.Coins + " monedas", Casino.Warning);
            yield break;
        }

        // Retira las cartas de la mano anterior.
        foreach (CardView v in AllViews()) Destroy(v.rt.gameObject);
        hands.Clear();
        dealer.Clear();
        dealerViews.Clear();

        GameSession.Coins -= bet;
        var hand = new Hand { bet = bet };
        hands.Add(hand);
        current = 0;
        phase = Phase.Player;
        SetMessage("Repartiendo...", UIFactory.Cream);
        RefreshLabels();

        yield return DealTo(hand);
        yield return DealToDealer(true);
        yield return DealTo(hand);
        yield return DealToDealer(false);

        if (Value(dealer[0]) >= 10 || Rank(dealer[0]) == 0)
        {
            SetMessage("El crupier mira su carta oculta...", UIFactory.Cream);
            yield return 0.7f;
        }
        if (DealerNatural(dealer) || IsNatural(hand))
        {
            hand.done = true;
            yield return DealerRoutine();
            yield break;
        }
        yield return Advance();
    }

    private IEnumerator HitRoutine()
    {
        Hand h = Current;
        if (phase != Phase.Player || h == null) yield break;
        yield return DealTo(h);
        if (Total(h.cards) >= 21) h.done = true;
        if (Total(h.cards) > 21) yield return 0.4f;
        yield return Advance();
    }

    private IEnumerator StandRoutine()
    {
        Hand h = Current;
        if (phase != Phase.Player || h == null) yield break;
        h.done = true;
        yield return Advance();
    }

    private IEnumerator DoubleRoutine()
    {
        Hand h = Current;
        if (phase != Phase.Player || h == null || h.cards.Count != 2) yield break;
        if (GameSession.Coins < h.bet)
        {
            Casino.Toast("No tienes monedas para doblar", Casino.Warning);
            yield break;
        }
        GameSession.Coins -= h.bet;
        h.bet *= 2;
        RefreshLabels();
        yield return DealTo(h, 90f);
        h.done = true;
        yield return 0.3f;
        yield return Advance();
    }

    private IEnumerator SplitRoutine()
    {
        Hand h = Current;
        if (phase != Phase.Player || !CanSplit(h)) yield break;
        GameSession.Coins -= h.bet;
        var second = new Hand { bet = h.bet, fromSplit = true };
        h.fromSplit = true;
        h.splitAces = second.splitAces = Rank(h.cards[0]) == 0;
        second.cards.Add(h.cards[1]);
        second.views.Add(h.views[1]);
        h.cards.RemoveAt(1);
        h.views.RemoveAt(1);
        hands.Insert(current + 1, second);
        RefreshLabels();
        yield return Relayout();
        yield return Advance();
    }

    // Pasa a la siguiente mano que necesite decisión (repartiendo la segunda carta a las manos divididas).
    private IEnumerator Advance()
    {
        while (true)
        {
            Hand h = hands[current];
            if (h.cards.Count == 1)
            {
                yield return DealTo(h);
                if (h.splitAces || Total(h.cards) >= 21) h.done = true;
            }
            if (!h.done)
            {
                RefreshLabels();
                string which = hands.Count > 1 ? "Mano " + (current + 1) + ": " : "";
                SetMessage(which + "¿Pides carta o te plantas?", UIFactory.Cream);
                yield break;
            }
            if (current + 1 >= hands.Count)
            {
                yield return DealerRoutine();
                yield break;
            }
            current++;
            RefreshLabels();
            yield return 0.25f;
        }
    }

    private IEnumerator DealerRoutine()
    {
        phase = Phase.Dealer;
        current = hands.Count;
        RefreshLabels();
        SetMessage("Juega el crupier...", UIFactory.Cream);
        if (dealerViews.Count > 1 && !dealerViews[1].faceUp) yield return Flip(dealerViews[1]);
        RefreshLabels();
        yield return 0.4f;

        bool anyAlive = hands.Any(h => Total(h.cards) <= 21) && !(hands.Count == 1 && IsNatural(hands[0]));
        if (anyAlive && !DealerNatural(dealer))
        {
            while (Total(dealer) < 17)
            {
                yield return DealToDealer(true);
                RefreshLabels();
                yield return 0.35f;
            }
        }
        Settle();
    }

    private void Settle()
    {
        phase = Phase.Done;
        int d = Total(dealer);
        bool dBJ = DealerNatural(dealer);
        int totalBet = 0, totalPaid = 0;
        foreach (Hand h in hands)
        {
            int p = Total(h.cards);
            bool pBJ = IsNatural(h);
            int pay;
            if (p > 21) { pay = 0; h.tag = "SE PASA"; }
            else if (pBJ && dBJ) { pay = h.bet; h.tag = "EMPATE"; }
            else if (pBJ) { pay = h.bet + h.bet * 3 / 2; h.tag = "¡BLACKJACK!"; }
            else if (dBJ) { pay = 0; h.tag = "PIERDE"; }
            else if (d > 21 || p > d) { pay = h.bet * 2; h.tag = "GANA"; }
            else if (p == d) { pay = h.bet; h.tag = "EMPATE"; }
            else { pay = 0; h.tag = "PIERDE"; }
            int net = pay - h.bet;
            h.tag += net > 0 ? "  +" + net : net < 0 ? "  " + net : "";
            h.tagColor = net > 0 ? new Color(0.55f, 1f, 0.55f) : net < 0 ? new Color(1f, 0.55f, 0.5f) : UIFactory.Cream;
            totalBet += h.bet;
            totalPaid += pay;
        }

        GameSession.Coins += totalPaid;
        GameSession.CoinsCollected += Mathf.Max(0, totalPaid - totalBet);
        int result = totalPaid - totalBet;
        string head = dBJ ? "El crupier tiene Blackjack. " : d > 21 ? "¡El crupier se pasa con " + d + "! " : "El crupier tiene " + d + ". ";
        if (result > 0) { SetMessage(head + "Ganas " + result + " monedas", new Color(0.6f, 1f, 0.6f)); Sfx.Play(Sfx.Clip.Coin); }
        else if (result < 0) SetMessage(head + "Pierdes " + (-result) + " monedas", new Color(1f, 0.6f, 0.55f));
        else SetMessage(head + (hands.Count > 1 ? "Quedas igual" : "Recuperas tu apuesta"), UIFactory.Cream);
        RefreshLabels();
    }

    // ---------- Animaciones de cartas ----------

    private IEnumerator DealTo(Hand h, float rotation = 0f)
    {
        int card = Draw();
        h.cards.Add(card);
        CardView v = NewView(card);
        h.views.Add(v);
        RefreshLabels();
        yield return Fly(v, HandCardPos(hands.IndexOf(h), h.views.Count - 1), rotation, true);
    }

    private IEnumerator DealToDealer(bool faceUp)
    {
        int card = Draw();
        dealer.Add(card);
        CardView v = NewView(card);
        dealerViews.Add(v);
        yield return Fly(v, DealerCardPos(dealerViews.Count - 1), 0f, faceUp);
        RefreshLabels();
    }

    private Vector2 DealerCardPos(int i) => new Vector2(-150f + i * 96f, DealerY);

    private Vector2 HandCardPos(int handIndex, int i)
    {
        int n = Mathf.Max(1, hands.Count);
        float spacing = n <= 1 ? 0f : n == 2 ? 520f : n == 3 ? 460f : 400f;
        float hx = (handIndex - (n - 1) * 0.5f) * spacing;
        return new Vector2(hx - 40f + i * 30f, PlayerY + i * 8f);
    }

    private CardView NewView(int card)
    {
        RectTransform rt = UIFactory.Rect("Card", cardLayer);
        UIFactory.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ShoePos, new Vector2(CardW, CardH));
        RawImage shadow = UIFactory.Rect("Shadow", rt).gameObject.AddComponent<RawImage>();
        shadow.texture = CardArt.Back;
        shadow.color = new Color(0f, 0f, 0f, 0.4f);
        shadow.raycastTarget = false;
        UIFactory.Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(7f, -7f), new Vector2(CardW, CardH));
        RawImage face = UIFactory.Rect("Face", rt).gameObject.AddComponent<RawImage>();
        face.texture = CardArt.Back;
        face.raycastTarget = false;
        UIFactory.Stretch(face.rectTransform);
        rt.localEulerAngles = new Vector3(0f, 0f, 25f);
        return new CardView { card = card, rt = rt, face = face };
    }

    // La carta sale del zapato, describe un arco y, si va boca arriba, se voltea a mitad de camino.
    private IEnumerator Fly(CardView v, Vector2 target, float endRotation, bool faceUp)
    {
        Vector2 from = v.rt.anchoredPosition;
        float startRot = v.rt.localEulerAngles.z;
        if (!instant) Sfx.Play(Sfx.Clip.CardDeal, 0.8f);
        float t = 0f;
        const float duration = 0.34f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Dt / duration);
            float e = 1f - (1f - t) * (1f - t);
            v.rt.anchoredPosition = Vector2.Lerp(from, target, e) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 40f;
            v.rt.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(startRot, endRotation, e));
            if (faceUp)
            {
                float flip = Mathf.Abs(Mathf.Cos(Mathf.Clamp01((t - 0.2f) / 0.6f) * Mathf.PI));
                bool showFace = t >= 0.5f;
                if (showFace != v.faceUp)
                {
                    SetFace(v, showFace);
                    if (!instant) Sfx.Play(Sfx.Clip.CardFlip, 0.5f);
                }
                v.rt.localScale = new Vector3(Mathf.Max(0.05f, flip), 1f, 1f);
            }
            yield return null;
        }
        v.rt.localScale = Vector3.one;
        v.rt.anchoredPosition = target;
        v.rt.SetAsLastSibling();
    }

    private IEnumerator Flip(CardView v)
    {
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Dt / 0.3f);
            if (t >= 0.5f && !v.faceUp)
            {
                SetFace(v, true);
                if (!instant) Sfx.Play(Sfx.Clip.CardFlip, 0.8f);
            }
            v.rt.localScale = new Vector3(Mathf.Max(0.05f, Mathf.Abs(Mathf.Cos(t * Mathf.PI))), 1f + 0.08f * Mathf.Sin(t * Mathf.PI), 1f);
            yield return null;
        }
        v.rt.localScale = Vector3.one;
    }

    private static void SetFace(CardView v, bool up)
    {
        v.faceUp = up;
        v.face.texture = up ? CardArt.Face(v.card) : CardArt.Back;
    }

    // Recoloca todas las cartas del jugador (al dividir cambian las posiciones de las manos).
    private IEnumerator Relayout()
    {
        var moves = new List<(CardView v, Vector2 from, Vector2 to)>();
        for (int h = 0; h < hands.Count; h++)
            for (int i = 0; i < hands[h].views.Count; i++)
                moves.Add((hands[h].views[i], hands[h].views[i].rt.anchoredPosition, HandCardPos(h, i)));
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Dt / 0.3f);
            float e = Mathf.SmoothStep(0f, 1f, t);
            foreach (var m in moves) m.v.rt.anchoredPosition = Vector2.Lerp(m.from, m.to, e);
            yield return null;
        }
        // Orden de dibujado: de izquierda a derecha y cada carta encima de la anterior.
        foreach (Hand h in hands)
            foreach (CardView v in h.views) v.rt.SetAsLastSibling();
    }

    private IEnumerable<CardView> AllViews() => dealerViews.Concat(hands.SelectMany(h => h.views));

    // ---------- Textos y botones ----------

    private void SetMessage(string msg, Color color)
    {
        messageText.text = msg;
        messageText.color = color;
    }

    private void RefreshLabels()
    {
        if (handUI == null) return;
        for (int i = handUI.childCount - 1; i >= 0; i--) Destroy(handUI.GetChild(i).gameObject);

        bool holeHidden = dealerViews.Count > 1 && !dealerViews[1].faceUp;
        if (dealer.Count == 0) dealerTotal.text = "";
        else if (holeHidden || dealerViews.Count < dealer.Count) dealerTotal.text = (Rank(dealer[0]) == 0 ? "A" : Value(dealer[0]).ToString()) + " + ?";
        else dealerTotal.text = HandLabel(dealer, DealerNatural(dealer));

        for (int h = 0; h < hands.Count; h++)
        {
            Hand hand = hands[h];
            float hx = HandCardPos(h, 0).x + 40f;
            bool active = phase == Phase.Player && h == current;

            // Total de la mano y, al terminar, su resultado al lado.
            string label = hand.cards.Count > 0 ? HandLabel(hand.cards, IsNatural(hand)) : "";
            bool showTag = !string.IsNullOrEmpty(hand.tag) && phase == Phase.Done;
            if (showTag) label += "  <color=#" + ColorUtility.ToHtmlStringRGB(hand.tagColor) + ">" + hand.tag + "</color>";
            Text total = UIFactory.Text(handUI, label, showTag && hands.Count > 2 ? 26 : 32, active ? UIFactory.Gold : UIFactory.Cream);
            UIFactory.Place(total.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(hx, PlayerY - 108f), new Vector2(420f, 44f));

            RectTransform stack = Casino.CoinStack(handUI, hand.bet, 46f);
            UIFactory.Place(stack, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(hx, PlayerY - 178f), new Vector2(46f, 46f));

            if (active && hands.Count > 1)
            {
                Image marker = UIFactory.Image(handUI, "Turno", PixelZDatabase.I.white, UIFactory.Gold);
                UIFactory.Place(marker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(hx, PlayerY + 105f), new Vector2(20f, 20f));
                marker.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            }
        }
    }

    private static string HandLabel(List<int> hand, bool natural)
    {
        int t = Total(hand, out bool soft);
        if (natural) return "BLACKJACK";
        if (t > 21) return t + "  ¡PASADO!";
        return soft && t < 21 ? (t - 10) + " / " + t : t.ToString();
    }

    private void RefreshButtons()
    {
        bool idle = running.Count == 0;
        bool betting = phase == Phase.Betting || phase == Phase.Done;
        Hand h = Current;
        bool playing = idle && phase == Phase.Player && h != null && !h.done;
        SetEnabled(dealButton, idle && betting);
        SetEnabled(hitButton, playing);
        SetEnabled(standButton, playing);
        SetEnabled(doubleButton, playing && h.cards.Count == 2 && GameSession.Coins >= h.bet);
        SetEnabled(splitButton, playing && CanSplit(h));
        UIFactory.SetLabel(dealButton, phase == Phase.Done ? "OTRA MANO" : "REPARTIR");
    }

    private static void SetEnabled(Button b, bool on)
    {
        if (b.interactable == on) return;
        b.interactable = on;
        b.GetComponent<UIButtonFx>().normal = on ? UIFactory.ButtonColor : new Color(0.16f, 0.14f, 0.14f, 0.95f);
    }
}

// Cartas en pixel art (30x42 píxeles): canto inferior para dar grosor, índices en las esquinas y palo grande.
public static class CardArt
{
    public const int W = 30, H = 42;
    private static readonly Texture2D[] faces = new Texture2D[52];
    private static Texture2D back;

    private static readonly string[] Ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

    public static Texture2D Face(int card)
    {
        card %= 52;
        if (faces[card] == null)
        {
            var c = new PixelCanvas(W, H);
            c.Clear(new Color32(0, 0, 0, 0));
            Paint(c, card, 0, 0, false);
            c.Apply();
            faces[card] = c.Texture;
        }
        return faces[card];
    }

    public static Texture2D Back
    {
        get
        {
            if (back == null)
            {
                var c = new PixelCanvas(W, H);
                c.Clear(new Color32(0, 0, 0, 0));
                Paint(c, 0, 0, 0, true);
                c.Apply();
                back = c.Texture;
            }
            return back;
        }
    }

    public static void Paint(PixelCanvas c, int card, int ox, int oy, bool faceDown)
    {
        Color32 outline = new Color(0.12f, 0.1f, 0.12f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                bool corner = (x == 0 || x == W - 1) && (y == 0 || y == H - 1);
                if (corner) continue;
                bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1;
                Color col;
                if (edge) col = outline;
                else if (y >= H - 3) col = new Color(0.62f, 0.6f, 0.58f);          // grosor de la carta
                else if (faceDown)
                {
                    bool border = x <= 2 || x >= W - 3 || y <= 2 || y >= H - 5;
                    bool lattice = ((x + y) % 5 == 0) || ((x - y + 100) % 5 == 0);
                    col = border ? new Color(0.95f, 0.92f, 0.85f) : lattice ? new Color(0.85f, 0.3f, 0.3f) : new Color(0.58f, 0.1f, 0.13f);
                }
                else col = y < 3 || x < 2 ? new Color(1f, 1f, 0.97f) : new Color(0.95f, 0.93f, 0.87f);
                c.Set(ox + x, oy + y, col);
            }
        if (faceDown)
        {
            c.Suit(1, ox + W / 2 - 3, oy + H / 2 - 5, Casino.GoldTrim, true);
            return;
        }

        int suit = (card / 13) % 4;
        Color32 ink = suit == 1 || suit == 2 ? new Color(0.82f, 0.1f, 0.12f) : new Color(0.12f, 0.12f, 0.14f);
        string rank = Ranks[card % 13];
        c.Text(rank, ox + 3, oy + 3, ink);
        c.Suit(suit, ox + 3, oy + 10, ink, false);
        c.Text(rank, ox + W - 3 - PixelCanvas.TextWidth(rank), oy + H - 11, ink);
        bool court = card % 13 >= 10;
        if (court)   // figuras: marco dorado alrededor del palo
        {
            for (int x = 9; x <= 22; x++) { c.Set(ox + x, oy + 11, Casino.GoldTrim); c.Set(ox + x, oy + 32, Casino.GoldTrim); }
            for (int y = 11; y <= 32; y++) { c.Set(ox + 9, oy + y, Casino.GoldTrim); c.Set(ox + 22, oy + y, Casino.GoldTrim); }
        }
        c.Suit(suit, ox + W / 2 - 7, oy + H / 2 - 8, ink, true, 2);
    }
}
