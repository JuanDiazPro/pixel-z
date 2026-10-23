using System.Collections.Generic;
using UnityEngine;

public enum PickupType { Coin, Gem, Heart, Potion, Key }

// Objeto que el jugador recoge al pasar por encima (monedas, gemas, corazones, poción, llave).
public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();

    public PickupType Type { get; private set; }

    private int value;
    private float life;
    private float age;
    private Vector2 popVelocity;
    private float height, heightVelocity;
    private bool magnetized;
    private float magnetSpeed;
    private Transform visual;
    private SpriteRenderer sr;
    private SpriteRenderer glow;

    private const float PotionDuration = 8f;

    public static Pickup Spawn(PickupType type, Vector2 position, int value, string sortingLayer, bool pop = true)
    {
        PixelZDatabase db = PixelZDatabase.I;
        var go = new GameObject("Pickup - " + type);
        go.transform.position = position;

        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(go.transform, false);
        var glow = glowGo.AddComponent<SpriteRenderer>();
        FX.Unlit(glow);
        glow.sprite = db.glow;
        glow.sortingLayerName = sortingLayer;
        glow.sortingOrder = 8;

        var visualGo = new GameObject("Visual");
        visualGo.transform.SetParent(go.transform, false);
        var sr = visualGo.AddComponent<SpriteRenderer>();
        FX.Unlit(sr);
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = 10;

        float scale = 1f;
        Color glowColor;
        switch (type)
        {
            case PickupType.Coin: sr.sprite = db.coin; scale = 0.7f; glowColor = new Color(1f, 0.85f, 0.2f, 0.35f); break;
            case PickupType.Gem: sr.sprite = db.gem; scale = 0.9f; glowColor = new Color(0.3f, 0.9f, 1f, 0.5f); break;
            case PickupType.Heart: sr.sprite = db.heart; scale = 1f; glowColor = new Color(1f, 0.2f, 0.3f, 0.55f); break;
            case PickupType.Potion: sr.sprite = db.potion; scale = 1f; glowColor = new Color(1f, 0.2f, 0.2f, 0.55f); break;
            default: sr.sprite = db.key; scale = 1.2f; glowColor = new Color(1f, 0.9f, 0.4f, 0.7f); break;
        }
        visualGo.transform.localScale = Vector3.one * scale;
        glow.color = glowColor;
        glowGo.transform.localScale = Vector3.one * scale * 1.2f;

        var p = go.AddComponent<Pickup>();
        p.Type = type;
        p.value = value;
        p.visual = visualGo.transform;
        p.sr = sr;
        p.glow = glow;
        p.life = type == PickupType.Key ? float.MaxValue : type == PickupType.Coin || type == PickupType.Gem ? 25f : 35f;
        if (pop)
        {
            p.popVelocity = Random.insideUnitCircle * Random.Range(0.8f, 2.2f);
            p.heightVelocity = Random.Range(3f, 4.5f);
        }
        All.Add(p);
        return p;
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        life -= dt;
        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Salto inicial al soltarse.
        if (heightVelocity != 0f || height > 0f)
        {
            heightVelocity -= 14f * dt;
            height += heightVelocity * dt;
            transform.position += (Vector3)(popVelocity * dt);
            if (height <= 0f)
            {
                height = 0f;
                heightVelocity = 0f;
                popVelocity = Vector2.zero;
            }
        }

        float bob = height > 0f ? height : 0.06f * Mathf.Sin(age * 4f + transform.position.x);
        visual.localPosition = new Vector3(0f, 0.25f + bob, 0f);
        glow.transform.localPosition = visual.localPosition;
        glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, (0.35f + 0.15f * Mathf.Sin(age * 5f)) * (Type == PickupType.Coin ? 0.7f : 1f));

        // Parpadeo antes de desaparecer.
        sr.enabled = life > 4f || Mathf.Repeat(life * 6f, 1f) > 0.35f;

        PlayerController player = PlayerController.Instance;
        if (player == null || player.Dead || age < 0.35f) return;

        Vector2 target = player.Center;
        float dist = Vector2.Distance(target, (Vector2)transform.position + Vector2.up * 0.25f);
        float magnetRadius = Type == PickupType.Coin || Type == PickupType.Gem ? player.MagnetRadius : 0.9f;

        if (dist < magnetRadius) magnetized = true;
        if (magnetized)
        {
            magnetSpeed = Mathf.Min(magnetSpeed + 30f * dt, 16f);
            Vector2 dir = (target - (Vector2)transform.position).normalized;
            transform.position += (Vector3)(dir * magnetSpeed * dt);
        }

        if (dist < 0.45f) Collect(player);
    }

    private void Collect(PlayerController player)
    {
        string layer = player.SortingLayer;
        switch (Type)
        {
            case PickupType.Coin:
                GameSession.AddCoins(value);
                GameHUD.Popup(player.Center + Vector2.up * 0.7f, "+" + value, new Color(1f, 0.85f, 0.25f));
                Sfx.Play(Sfx.Clip.Coin, 0.7f);
                break;
            case PickupType.Gem:
                // Una gema es una moneda que vale más (GemValue): va al mismo saldo que se gasta en la tienda.
                GameSession.AddCoins(value);
                GameHUD.Popup(player.Center + Vector2.up * 0.7f, "+" + value, new Color(1f, 0.85f, 0.25f));
                FX.Burst(transform.position, new Color(0.5f, 1f, 1f), 8, 2.5f, 0.06f, layer);
                Sfx.Play(Sfx.Clip.Coin);
                break;
            case PickupType.Heart:
                if (GameSession.Hearts < GameSession.MaxHearts) player.Heal(1);
                else
                {
                    GameSession.AddCoins(5);
                    GameHUD.Popup(player.Center + Vector2.up * 0.7f, "Vida llena +5", new Color(1f, 0.85f, 0.25f));
                }
                break;
            case PickupType.Potion:
                player.ApplySpeedBuff(PotionDuration);
                Sfx.Play(Sfx.Clip.Potion);
                GameHUD.Toast("¡Poción de velocidad! (" + PotionDuration + " s)", new Color(1f, 0.5f, 0.45f));
                break;
            case PickupType.Key:
                GameSession.HasKey = true;
                GameHUD.Banner("¡LLAVE OBTENIDA!", "Ve a la puerta del sur del mapa para entrar al castillo");
                FX.Ring(transform.position, new Color(1f, 0.9f, 0.4f), 0.4f, 4f, 0.6f, layer);
                break;
        }
        GameHUD.PulseCurrency(Type);
        Destroy(gameObject);
    }
}

// Reglas de botín de los enemigos.
public static class Loot
{
    private static int killsSinceHeart;

    public static void DropFor(Enemy enemy, string sortingLayer)
    {
        EnemyDef def = enemy.Def;
        Vector2 pos = enemy.transform.position;
        int wave = Mathf.Max(1, GameSession.Wave);

        // Monedas: valor total repartido en un máximo de 6 monedas visibles.
        int coinValue = Random.Range(def.coinMin, def.coinMax + 1) + wave / 6;
        int pieces = Mathf.Min(coinValue, def.boss ? 10 : 4);
        int baseValue = coinValue / pieces;
        int remainder = coinValue % pieces;
        for (int i = 0; i < pieces; i++)
            Pickup.Spawn(PickupType.Coin, pos, baseValue + (i < remainder ? 1 : 0), sortingLayer);

        // Gemas (valen más que las monedas).
        float gemChance = Mathf.Min(def.gemChance + wave * 0.004f, 1f);
        int gems = def.boss ? 3 + wave / 10 : (Random.value < gemChance ? 1 : 0);
        for (int i = 0; i < gems; i++)
            Pickup.Spawn(PickupType.Gem, pos, GameSession.GemValue, sortingLayer);

        // Corazones: raros, más probables cuando el jugador va mal. Con 1 corazón hay "seguro de pena".
        killsSinceHeart++;
        int hearts = GameSession.Hearts;
        bool missing = hearts < GameSession.MaxHearts;
        bool heartOnGround = Pickup.All.Exists(p => p != null && p.Type == PickupType.Heart);
        float heartChance = !missing ? 0.005f : hearts == 1 ? 0.12f : 0.035f;
        if (def.boss && missing) heartChance = 1f;
        if (hearts == 1 && killsSinceHeart >= 18) heartChance = 1f;
        if (heartOnGround && !def.boss) heartChance *= 0.2f;
        if (Random.value < heartChance)
        {
            killsSinceHeart = 0;
            Pickup.Spawn(PickupType.Heart, pos, 1, sortingLayer);
        }

        // Poción de velocidad: rara, algo más frecuente con poca vida.
        bool potionOnGround = Pickup.All.Exists(p => p != null && p.Type == PickupType.Potion);
        float potionChance = hearts == 1 ? 0.06f : 0.02f;
        if (!potionOnGround && Random.value < potionChance)
            Pickup.Spawn(PickupType.Potion, pos, 1, sortingLayer);
    }

    public static void ResetPity()
    {
        killsSinceHeart = 0;
    }
}
