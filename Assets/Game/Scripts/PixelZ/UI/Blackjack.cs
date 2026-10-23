using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Blackjack contra el crupier con un zapato de 6 barajas. El crupier se planta en 17.
// Ganar paga 1 a 1, Blackjack 3 a 2 y el empate devuelve la apuesta. Se puede doblar con las dos primeras cartas.
public class Blackjack : MonoBehaviour
{
    private enum State { Betting, Playing, Dealer, Done }

    private const int Decks = 6;
    private const float CardW = 130f, CardH = 186f;
    private static readonly string[] Ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

    public bool Busy => state == State.Dealer;

    private readonly List<int> shoe = new List<int>();
    private readonly List<int> player = new List<int>();
    private readonly List<int> dealer = new List<int>();
    private State state = State.Betting;
    private int bet, baseBet, pendingBet;
    private bool holeHidden;
    private bool settled;
    private string outcome;
    private Color outcomeColor;

    private RectTransform dealerRow, playerRow;
    private Text dealerTotal, playerTotal, betText, messageText;
    private Button dealButton, hitButton, standButton, doubleButton, clearButton;

    public void Build(RectTransform screen)
    {
        Image table = UIFactory.Image(screen, "Table", Casino.Disc, Casino.Felt);
        UIFactory.Place(table.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1700f, 700f));
        Image edge = UIFactory.Image(table.transform, "Inner", Casino.Disc, new Color(0.07f, 0.38f, 0.19f));
        UIFactory.Stretch(edge.rectTransform, 22f);

        Text rule = UIFactory.Text(screen, "BLACKJACK PAGA 3 A 2  ·  EL CRUPIER SE PLANTA EN 17", 26, new Color(1f, 0.85f, 0.4f, 0.75f));
        UIFactory.Place(rule.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 40f));

        Text dLabel = UIFactory.Text(screen, "CRUPIER", 30, UIFactory.Cream);
        UIFactory.Place(dLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, 300f), new Vector2(260f, 44f));
        dealerTotal = UIFactory.Text(screen, "", 40, UIFactory.Gold);
        UIFactory.Place(dealerTotal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, 250f), new Vector2(260f, 50f));
        dealerRow = Row(screen, new Vector2(0f, 260f));

        Text pLabel = UIFactory.Text(screen, "TÚ", 30, UIFactory.Cream);
        UIFactory.Place(pLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, -90f), new Vector2(260f, 44f));
        playerTotal = UIFactory.Text(screen, "", 40, UIFactory.Gold);
        UIFactory.Place(playerTotal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560f, -140f), new Vector2(260f, 50f));
        playerRow = Row(screen, new Vector2(0f, -130f));

        messageText = UIFactory.Text(screen, "", 40, UIFactory.Cream);
        UIFactory.Place(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1400f, 56f));

        betText = UIFactory.Text(screen, "", 32, UIFactory.Gold);
        UIFactory.Place(betText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560f, -90f), new Vector2(360f, 50f));

        Casino.ChipSelector(screen, new Vector2(-560f, -330f));
        Button add = UIFactory.Button(screen, "APOSTAR FICHA", 26, AddChip, new Vector2(250f, 76f));
        UIFactory.Place((RectTransform)add.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, -330f), new Vector2(250f, 76f));
        clearButton = UIFactory.Button(screen, "BORRAR", 26, () => { if (state == State.Betting || state == State.Done) pendingBet = 0; Refresh(); }, new Vector2(170f, 76f));
        UIFactory.Place((RectTransform)clearButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(250f, -330f), new Vector2(170f, 76f));

        dealButton = UIFactory.Button(screen, "REPARTIR", 36, Deal, new Vector2(260f, 84f));
        UIFactory.Place((RectTransform)dealButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500f, -330f), new Vector2(260f, 84f));

        hitButton = UIFactory.Button(screen, "PEDIR", 36, Hit, new Vector2(240f, 84f));
        UIFactory.Place((RectTransform)hitButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-280f, -430f), new Vector2(240f, 84f));
        standButton = UIFactory.Button(screen, "PLANTARSE", 36, Stand, new Vector2(280f, 84f));
        UIFactory.Place((RectTransform)standButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -430f), new Vector2(280f, 84f));
        doubleButton = UIFactory.Button(screen, "DOBLAR", 36, DoubleDown, new Vector2(240f, 84f));
        UIFactory.Place((RectTransform)doubleButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(280f, -430f), new Vector2(240f, 84f));

        Refresh();
    }

    private static RectTransform Row(Transform parent, Vector2 pos)
    {
        RectTransform row = UIFactory.Rect("Cards", parent);
        UIFactory.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(900f, CardH));
        var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 16f;
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = hl.childControlHeight = false;
        hl.childForceExpandWidth = hl.childForceExpandHeight = false;
        return row;
    }

    // ---------- Cartas ----------

    private int Draw()
    {
        if (shoe.Count < 20)
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

    public static int Total(List<int> hand, out bool soft)
    {
        int total = 0, aces = 0;
        foreach (int c in hand)
        {
            int r = Rank(c);
            if (r == 0) { aces++; total += 11; }
            else total += Mathf.Min(r + 1, 10);
        }
        while (total > 21 && aces > 0) { total -= 10; aces--; }
        soft = aces > 0;
        return total;
    }

    private static int Total(List<int> hand) => Total(hand, out _);
    private static bool IsBlackjack(List<int> hand) => hand.Count == 2 && Total(hand) == 21;

    // ---------- Jugadas ----------

    private void AddChip()
    {
        if (state != State.Betting && state != State.Done) return;
        if (pendingBet + Casino.SelectedChip > GameSession.Coins)
        {
            Casino.Toast("No tienes monedas suficientes", new Color(1f, 0.6f, 0.5f));
            return;
        }
        pendingBet += Casino.SelectedChip;
        Refresh();
    }

    private void Deal()
    {
        if (state != State.Betting && state != State.Done) return;
        if (pendingBet <= 0)
        {
            Casino.Toast("Primero apuesta alguna ficha", new Color(1f, 0.6f, 0.5f));
            return;
        }
        if (pendingBet > GameSession.Coins) pendingBet = GameSession.Coins;
        if (pendingBet <= 0) return;

        bet = baseBet = pendingBet;
        GameSession.Coins -= bet;
        player.Clear();
        dealer.Clear();
        player.Add(Draw());
        dealer.Add(Draw());
        player.Add(Draw());
        dealer.Add(Draw());
        holeHidden = true;
        settled = false;
        outcome = null;
        state = State.Playing;

        // Con Blackjack de alguno se acaba la mano al momento.
        if (IsBlackjack(player) || IsBlackjack(dealer))
        {
            holeHidden = false;
            Settle();
        }
        Refresh();
    }

    private void Hit()
    {
        if (state != State.Playing) return;
        player.Add(Draw());
        int t = Total(player);
        if (t > 21)
        {
            holeHidden = false;
            Settle();
        }
        else if (t == 21) StartDealer();
        Refresh();
    }

    private void Stand()
    {
        if (state != State.Playing) return;
        StartDealer();
    }

    private void DoubleDown()
    {
        if (state != State.Playing || player.Count != 2) return;
        if (GameSession.Coins < bet)
        {
            Casino.Toast("No tienes monedas para doblar", new Color(1f, 0.6f, 0.5f));
            return;
        }
        GameSession.Coins -= bet;
        bet *= 2;
        player.Add(Draw());
        if (Total(player) > 21)
        {
            holeHidden = false;
            Settle();
            Refresh();
        }
        else StartDealer();
    }

    private void StartDealer()
    {
        state = State.Dealer;
        holeHidden = false;
        Refresh();
        StartCoroutine(DealerRoutine());
    }

    private IEnumerator DealerRoutine()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        while (Total(dealer) < 17)
        {
            dealer.Add(Draw());
            Sfx.Play(Sfx.Clip.MenuSelect, 0.5f);
            Refresh();
            yield return new WaitForSecondsRealtime(0.6f);
        }
        Settle();
        Refresh();
    }

    // Si se sale del blackjack a mitad de mano, el crupier termina su turno al instante y se paga lo que toque.
    private void OnDisable()
    {
        if (state == State.Playing || state == State.Dealer)
        {
            holeHidden = false;
            while (Total(dealer) < 17) dealer.Add(Draw());
            Settle();
        }
    }

    private void Settle()
    {
        if (settled) return;
        settled = true;
        state = State.Done;

        int p = Total(player), d = Total(dealer);
        bool pBJ = IsBlackjack(player), dBJ = IsBlackjack(dealer);
        int payout;
        if (p > 21) { payout = 0; outcome = "Te pasaste de 21. Pierdes " + bet; }
        else if (pBJ && dBJ) { payout = bet; outcome = "Los dos tenéis Blackjack. Empate"; }
        else if (pBJ) { payout = bet + bet * 3 / 2; outcome = "¡BLACKJACK! Ganas " + payout; }
        else if (dBJ) { payout = 0; outcome = "El crupier tiene Blackjack. Pierdes " + bet; }
        else if (d > 21) { payout = bet * 2; outcome = "¡El crupier se pasa! Ganas " + payout; }
        else if (p > d) { payout = bet * 2; outcome = "¡Ganas! " + p + " contra " + d + ". Cobras " + payout; }
        else if (p == d) { payout = bet; outcome = "Empate a " + p + ". Recuperas tu apuesta"; }
        else { payout = 0; outcome = d + " contra " + p + ". Gana el crupier, pierdes " + bet; }

        GameSession.Coins += payout;
        GameSession.CoinsCollected += Mathf.Max(0, payout - bet);
        outcomeColor = payout > bet ? new Color(0.6f, 1f, 0.6f) : payout == bet ? UIFactory.Cream : new Color(1f, 0.6f, 0.55f);
        if (payout > bet) Sfx.Play(Sfx.Clip.Coin);

        // La siguiente mano propone la misma apuesta (sin el doble) si alcanza.
        pendingBet = Mathf.Min(baseBet, GameSession.Coins);
    }

    // ---------- Pantalla ----------

    private void Refresh()
    {
        if (dealerRow == null) return;
        DrawHand(dealerRow, dealer, holeHidden);
        DrawHand(playerRow, player, false);

        bool inHand = state == State.Playing || state == State.Dealer;
        bool hasCards = player.Count > 0;
        playerTotal.text = hasCards ? HandLabel(player) : "";
        dealerTotal.text = !hasCards ? "" : holeHidden ? Mathf.Min(Rank(dealer[0]) + 1, 10) == 1 ? "A + ?" : Mathf.Min(Rank(dealer[0]) + 1, 10) + " + ?" : HandLabel(dealer);

        betText.text = inHand ? "Apuesta: " + bet : "Apuesta: " + pendingBet;
        if (state == State.Done && outcome != null)
        {
            messageText.text = outcome;
            messageText.color = outcomeColor;
        }
        else if (state == State.Betting)
        {
            messageText.text = "Elige ficha, pulsa APOSTAR FICHA y luego REPARTIR";
            messageText.color = UIFactory.Cream;
        }
        else if (state == State.Playing)
        {
            messageText.text = "¿Pides carta o te plantas?";
            messageText.color = UIFactory.Cream;
        }
        else
        {
            messageText.text = "Juega el crupier...";
            messageText.color = UIFactory.Cream;
        }

        SetEnabled(dealButton, !inHand && pendingBet > 0);
        SetEnabled(clearButton, !inHand && pendingBet > 0);
        SetEnabled(hitButton, state == State.Playing);
        SetEnabled(standButton, state == State.Playing);
        SetEnabled(doubleButton, state == State.Playing && player.Count == 2 && GameSession.Coins >= bet);
        UIFactory.SetLabel(dealButton, state == State.Done ? "OTRA MANO" : "REPARTIR");
    }

    private static string HandLabel(List<int> hand)
    {
        int t = Total(hand, out bool soft);
        if (IsBlackjack(hand)) return "BLACKJACK";
        if (t > 21) return t + " ¡PASADO!";
        return soft && t < 21 ? (t - 10) + " / " + t : t.ToString();
    }

    private static void SetEnabled(Button b, bool on)
    {
        b.interactable = on;
        var fx = b.GetComponent<UIButtonFx>();
        fx.normal = on ? UIFactory.ButtonColor : new Color(0.18f, 0.16f, 0.16f, 0.95f);
        b.GetComponentInChildren<Text>().color = on ? UIFactory.Cream : new Color(0.6f, 0.55f, 0.55f);
    }

    private static void DrawHand(RectTransform row, List<int> hand, bool hideSecond)
    {
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);
        for (int i = 0; i < hand.Count; i++) CardView(row, hand[i], hideSecond && i == 1);
    }

    private static void CardView(Transform parent, int card, bool faceDown)
    {
        Image body = UIFactory.Panel(parent, "Card", faceDown ? new Color(0.55f, 0.12f, 0.15f) : new Color(0.97f, 0.95f, 0.9f));
        body.rectTransform.sizeDelta = new Vector2(CardW, CardH);
        body.raycastTarget = false;
        if (faceDown)
        {
            Image pattern = UIFactory.Image(body.transform, "Back", PixelZDatabase.I.white, new Color(0.8f, 0.3f, 0.3f, 0.5f));
            UIFactory.Stretch(pattern.rectTransform, 14f);
            Text logo = UIFactory.Text(body.transform, "PZ", 44, UIFactory.Gold);
            UIFactory.Stretch(logo.rectTransform);
            return;
        }

        int suit = (card / 13) % 4;
        Color ink = suit == 1 || suit == 2 ? new Color(0.8f, 0.1f, 0.12f) : new Color(0.1f, 0.1f, 0.12f);
        string rank = Ranks[Rank(card)];
        Text corner = UIFactory.Text(body.transform, rank, 30, ink, TextAnchor.UpperLeft, false);
        UIFactory.Stretch(corner.rectTransform, 10f);
        SuitIcon(body.transform, suit, new Vector2(-CardW * 0.5f + 24f, CardH * 0.5f - 60f), 26f, ink);
        SuitIcon(body.transform, suit, Vector2.zero, 64f, ink);
        Text corner2 = UIFactory.Text(body.transform, rank, 30, ink, TextAnchor.LowerRight, false);
        UIFactory.Stretch(corner2.rectTransform, 10f);
    }

    // La fuente integrada no trae los símbolos de los palos, así que se dibujan con círculos y rombos.
    private static void SuitIcon(Transform parent, int suit, Vector2 pos, float size, Color ink)
    {
        RectTransform icon = UIFactory.Rect("Suit", parent);
        UIFactory.Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, Vector2.one * size);

        void Piece(Sprite sprite, float x, float y, float w, float h, float rot = 0f)
        {
            Image img = UIFactory.Image(icon, "Piece", sprite, ink);
            UIFactory.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y) * size, new Vector2(w, h) * size);
            img.rectTransform.localEulerAngles = new Vector3(0f, 0f, rot);
        }

        Sprite disc = Casino.Disc, square = PixelZDatabase.I.white;
        switch (suit)
        {
            case 0: // picas
                Piece(square, 0f, 0.1f, 0.55f, 0.55f, 45f);
                Piece(disc, -0.2f, -0.12f, 0.55f, 0.55f);
                Piece(disc, 0.2f, -0.12f, 0.55f, 0.55f);
                Piece(square, 0f, -0.32f, 0.12f, 0.36f);
                break;
            case 1: // corazones
                Piece(disc, -0.2f, 0.12f, 0.55f, 0.55f);
                Piece(disc, 0.2f, 0.12f, 0.55f, 0.55f);
                Piece(square, 0f, -0.1f, 0.55f, 0.55f, 45f);
                break;
            case 2: // diamantes
                Piece(square, 0f, 0f, 0.62f, 0.62f, 45f);
                break;
            default: // tréboles
                Piece(disc, 0f, 0.22f, 0.44f, 0.44f);
                Piece(disc, -0.22f, -0.08f, 0.44f, 0.44f);
                Piece(disc, 0.22f, -0.08f, 0.44f, 0.44f);
                Piece(square, 0f, -0.3f, 0.12f, 0.36f);
                break;
        }
    }
}
