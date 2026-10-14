using System.Collections.Generic;
using UnityEngine;

// Cajas, jarrones y barriles que el jugador puede romper a golpes (o flechas).
// Sueltan al azar monedas, una gema, un corazón o una poción, y reaparecen pasado un rato.
public class Breakable : MonoBehaviour
{
    public static readonly List<Breakable> All = new List<Breakable>();

    public int maxHits = 2;
    public Color debrisColor = new Color(0.55f, 0.38f, 0.2f);

    private int hits;
    private bool broken;
    private float respawnTimer;
    private float wobble;
    private float fadeIn = -1f;
    private Vector3 baseScale;
    private SpriteRenderer[] renderers;
    private Collider2D[] colliders;
    private readonly List<GameObject> obstacleCopies = new List<GameObject>();

    private Bounds Bounds => renderers.Length > 0 ? renderers[0].bounds : new Bounds(transform.position, Vector3.one * 0.8f);
    private Vector2 Center => Bounds.center;
    private float Radius => Mathf.Max(Bounds.extents.x, Bounds.extents.y) * 0.8f;

    // Convierte los props de Cainos con nombre de caja/jarrón/barril en rompibles.
    public static void SetupAll()
    {
        All.Clear();
        foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>())
        {
            string n = sr.name;
            if (!n.StartsWith("PF Props")) continue;

            Breakable b = null;
            if (n.Contains("Crate")) { b = sr.gameObject.AddComponent<Breakable>(); b.maxHits = 2; b.debrisColor = new Color(0.6f, 0.42f, 0.22f); }
            else if (n.Contains("Barrel")) { b = sr.gameObject.AddComponent<Breakable>(); b.maxHits = 3; b.debrisColor = new Color(0.5f, 0.33f, 0.18f); }
            else if (n.Contains("Pot") || n.Contains("Jar") || n.Contains("Vase")) { b = sr.gameObject.AddComponent<Breakable>(); b.maxHits = 1; b.debrisColor = new Color(0.78f, 0.48f, 0.32f); }
        }
    }

    public void AddObstacleCopy(GameObject copy) => obstacleCopies.Add(copy);

    private void Awake()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>();
        colliders = GetComponentsInChildren<Collider2D>();
        baseScale = transform.localScale;
        All.Add(this);
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    // ---------- Golpes ----------

    public static void HitArea(Vector2 center, float radius, Vector2 from)
    {
        foreach (Breakable b in All.ToArray())
            if (!b.broken && Vector2.Distance(b.Center, center) <= radius + b.Radius)
                b.Hit(from);
    }

    // Para flechas: devuelve true si la flecha choca con un objeto rompible.
    public static bool HitPoint(Vector2 point, float radius, Vector2 from)
    {
        foreach (Breakable b in All)
        {
            if (b.broken || Vector2.Distance(b.Center, point) > radius + b.Radius) continue;
            b.Hit(from);
            return true;
        }
        return false;
    }

    private void Hit(Vector2 from)
    {
        hits++;
        wobble = 0.18f;
        string layer = renderers.Length > 0 ? renderers[0].sortingLayerName : FX.TopSortingLayer;
        FX.Burst(Center, debrisColor, 5, 2.2f, 0.07f, layer);
        if (hits >= maxHits) Break(layer);
    }

    private void Break(string layer)
    {
        broken = true;
        hits = 0;
        respawnTimer = Random.Range(60f, 100f);
        transform.localScale = baseScale;

        foreach (SpriteRenderer sr in renderers) sr.enabled = false;
        foreach (Collider2D c in colliders) c.enabled = false;
        foreach (GameObject o in obstacleCopies) if (o != null) o.SetActive(false);

        FX.Burst(Center, debrisColor, 16, 4.5f, 0.1f, layer);
        FX.Burst(Center, new Color(0.85f, 0.8f, 0.7f, 0.8f), 8, 2.5f, 0.06f, layer);
        FX.Shake(0.06f, 0.1f);
        DropLoot(layer);
    }

    private void DropLoot(string layer)
    {
        Vector2 pos = (Vector2)transform.position + Vector2.up * 0.2f;
        float r = Random.value;
        bool potionOnGround = Pickup.All.Exists(p => p != null && p.Type == PickupType.Potion);

        if (r < 0.45f)
        {
            int coins = Random.Range(1, 4);
            for (int i = 0; i < coins; i++) Pickup.Spawn(PickupType.Coin, pos, 1, layer);
        }
        else if (r < 0.65f)
        {
            Pickup.Spawn(PickupType.Gem, pos, GameSession.GemValue, layer);
        }
        else if (r < 0.83f)
        {
            if (GameSession.Hearts < GameSession.MaxHearts) Pickup.Spawn(PickupType.Heart, pos, 1, layer);
            else for (int i = 0; i < 2; i++) Pickup.Spawn(PickupType.Coin, pos, 1, layer);
        }
        else
        {
            if (!potionOnGround) Pickup.Spawn(PickupType.Potion, pos, 1, layer);
            else Pickup.Spawn(PickupType.Gem, pos, GameSession.GemValue, layer);
        }
    }

    // ---------- Animación y reaparición ----------

    private void Update()
    {
        if (wobble > 0f)
        {
            wobble -= Time.deltaTime;
            float k = Mathf.Sin(wobble * 60f) * (wobble / 0.18f) * 0.08f;
            transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), baseScale.z);
            if (wobble <= 0f) transform.localScale = baseScale;
        }

        if (fadeIn >= 0f)
        {
            fadeIn += Time.deltaTime;
            float a = Mathf.Clamp01(fadeIn / 0.6f);
            foreach (SpriteRenderer sr in renderers)
            {
                Color c = sr.color;
                c.a = a;
                sr.color = c;
            }
            if (a >= 1f) fadeIn = -1f;
        }

        if (!broken) return;
        respawnTimer -= Time.deltaTime;
        if (respawnTimer > 0f) return;

        // No reaparece encima del jugador ni de enemigos.
        PlayerController player = PlayerController.Instance;
        if (player != null && Vector2.Distance(player.transform.position, transform.position) < 3f) return;
        foreach (Enemy e in Enemy.All)
            if (Vector2.Distance(e.transform.position, transform.position) < 1f) return;

        broken = false;
        foreach (SpriteRenderer sr in renderers) sr.enabled = true;
        foreach (Collider2D c in colliders) c.enabled = true;
        foreach (GameObject o in obstacleCopies) if (o != null) o.SetActive(true);
        fadeIn = 0f;
    }
}
