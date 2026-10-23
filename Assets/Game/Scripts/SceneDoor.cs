using UnityEngine;
using UnityEngine.InputSystem;

// Puerta que lleva a otra escena.
// Cuando el jugador entra en el trigger aparece el icono de la tecla E sobre la puerta;
// al pulsar E se carga la escena destino y el jugador aparece en el SpawnPoint indicado.
// Si requiresKey está activo hace falta la llave del cofre (GameSession.HasKey).
[RequireComponent(typeof(Collider2D))]
public class SceneDoor : MonoBehaviour
{
    [Header("Destino")]
    public string targetScene;
    public string targetSpawnId;

    [Header("Llave")]
    public bool requiresKey;
    public string label = "Entrar";

    [Header("Precio (monedas, 0 = gratis)")]
    public int price;

    [Header("Icono")]
    public SpriteRenderer promptIcon;
    public float bobHeight = 0.08f;
    public float bobSpeed = 4f;
    public float fadeSpeed = 8f;

    private int playersInside;   // en cooperativo pueden estar los dos dentro
    private bool playerInside => playersInside > 0;
    private Vector3 iconBasePos;
    private float iconAlpha;

    private bool Locked => requiresKey && !GameSession.HasKey;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        if (promptIcon != null)
        {
            iconBasePos = promptIcon.transform.localPosition;
            SetIconAlpha(0f);
        }
    }

    private void Update()
    {
        UpdateIcon();

        if (!playerInside || SceneTransition.IsBusy || Time.timeScale == 0f) return;
        if (PlayerController.All.Count > 0 && !PlayerController.AnyAlive) return;

        if (GameManager.Instance != null)
        {
            Vector2 promptPos = (promptIcon != null ? promptIcon.transform.position : transform.position) + Vector3.up * 0.7f;
            if (Locked) GameHUD.Prompt(promptPos, "PUERTA SELLADA\nNecesitas la llave del cofre", new Color(1f, 0.55f, 0.5f));
            else if (price > 0 && GameSession.Coins < price)
                GameHUD.Prompt(promptPos, label + " · " + price + " monedas\n(te faltan " + (price - GameSession.Coins) + ")", new Color(1f, 0.55f, 0.5f));
            else if (price > 0) GameHUD.Prompt(promptPos, GameInput.InteractLabel + " " + label + " · " + price + " monedas", new Color(1f, 0.85f, 0.3f));
            else GameHUD.Prompt(promptPos, GameInput.InteractLabel + " " + label, new Color(1f, 0.85f, 0.3f));
        }

        if (!GameInput.AnyInteractPressed) return;

        if (Locked)
        {
            GameHUD.Toast("La puerta está sellada. Compra el cofre del centro para conseguir la llave.", new Color(1f, 0.55f, 0.5f));
            return;
        }
        if (price > 0)
        {
            if (GameSession.Coins < price)
            {
                GameHUD.Toast("Necesitas " + price + " monedas para volver (tienes " + GameSession.Coins + ")", new Color(1f, 0.55f, 0.5f));
                return;
            }
            GameSession.Coins -= price;
            GameHUD.Toast("-" + price + " monedas", new Color(1f, 0.85f, 0.3f));
        }
        SceneTransition.Go(targetScene, targetSpawnId);
    }

    private void UpdateIcon()
    {
        if (promptIcon == null) return;

        float target = playerInside ? 1f : 0f;
        iconAlpha = Mathf.MoveTowards(iconAlpha, target, fadeSpeed * Time.deltaTime);
        SetIconAlpha(iconAlpha);

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        promptIcon.transform.localPosition = iconBasePos + new Vector3(0f, bob, 0f);
    }

    private void SetIconAlpha(float a)
    {
        Color c = promptIcon.color;
        c.a = a;
        promptIcon.color = c;
        promptIcon.enabled = a > 0f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playersInside++;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playersInside = Mathf.Max(0, playersInside - 1);
    }
}
