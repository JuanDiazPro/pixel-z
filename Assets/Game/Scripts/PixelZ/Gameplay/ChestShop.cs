using UnityEngine;
using UnityEngine.InputSystem;

// Cofre del centro del mapa 1: se compra con monedas (tecla E) y suelta la llave del castillo.
public class ChestShop : MonoBehaviour
{
    public int price = GameSession.ChestPrice;
    public float interactRadius = 1.4f;
    public Sprite openSprite;

    private SpriteRenderer sr;
    private float pulse;

    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (GameSession.ChestOpened) SetOpenVisual();
    }

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || player.Dead || Time.timeScale == 0f) return;

        if (!GameSession.ChestOpened)
        {
            // Brillo suave para que el jugador lo encuentre.
            pulse += Time.deltaTime;
            if (sr != null) sr.color = Color.Lerp(Color.white, new Color(1f, 0.92f, 0.6f), 0.5f + 0.5f * Mathf.Sin(pulse * 3f));
        }

        float dist = Vector2.Distance(player.transform.position, transform.position);
        if (dist > interactRadius) return;

        Vector2 promptPos = (Vector2)transform.position + Vector2.up * 1.3f;
        if (GameSession.ChestOpened)
        {
            GameHUD.Prompt(promptPos, GameSession.HasKey ? "Cofre vacío" : "¡Recoge la llave!", Color.white);
            return;
        }

        bool canAfford = GameSession.Coins >= price;
        string text = "COFRE DEL GUARDIÁN\nContiene la llave del castillo\n[E] Comprar por " + price + " monedas";
        if (!canAfford) text += "\n(te faltan " + (price - GameSession.Coins) + ")";
        GameHUD.Prompt(promptPos, text, canAfford ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.55f, 0.5f));

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame) TryPurchase(player);
    }

    public bool TryPurchase(PlayerController player)
    {
        if (GameSession.ChestOpened) return false;
        if (GameSession.Coins < price)
        {
            GameHUD.Toast("No tienes suficientes monedas (" + GameSession.Coins + "/" + price + ")", new Color(1f, 0.5f, 0.45f));
            return false;
        }

        GameSession.Coins -= price;
        GameSession.ChestOpened = true;
        SetOpenVisual();

        string layer = player.SortingLayer;
        FX.Glow(transform.position, new Color(1f, 0.85f, 0.3f, 0.9f), 0.5f, 5f, 0.7f, layer);
        FX.Burst((Vector2)transform.position + Vector2.up * 0.4f, new Color(1f, 0.85f, 0.3f), 24, 5f, 0.08f, layer);
        FX.Shake(0.15f, 0.2f);

        Vector2 keyPos = (Vector2)transform.position + Vector2.down * 0.9f;
        Pickup.Spawn(PickupType.Key, keyPos, 1, layer, true);
        GameHUD.Toast("¡El cofre se abrió! Recoge la llave", new Color(1f, 0.85f, 0.3f));
        return true;
    }

    private void SetOpenVisual()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            if (openSprite != null) sr.sprite = openSprite;
            sr.color = Color.white;
        }
    }
}
