using System.Collections.Generic;
using UnityEngine;

// Flechas del arquero y disparos de enemigos a distancia. Colisiones por distancia (sin física).
public class Projectile : MonoBehaviour
{
    private bool fromPlayer;
    private Vector2 dir;
    private float speed, damage, life, radius, knockback;
    private int pierce;
    private readonly HashSet<Enemy> alreadyHit = new HashSet<Enemy>();
    private string sortingLayer;
    private bool explodes;

    public static void SpawnPlayerArrow(Vector2 pos, Vector2 dir, float damage, int pierce, float knockback, string sortingLayer)
    {
        var p = Create("Arrow", pos, dir, PixelZDatabase.I.arrow, null, sortingLayer, 1f);
        p.fromPlayer = true;
        p.speed = 12f;
        p.damage = damage;
        p.pierce = pierce;
        p.life = 0.9f;
        p.radius = 0.2f;
        p.knockback = knockback;
    }

    public static void SpawnEnemyShot(EnemyDef def, Vector2 pos, Vector2 dir, int damage, string sortingLayer)
    {
        Projectile p;
        if (def.art == EnemyArt.Demon)
        {
            p = Create("Fireball", pos, dir, null, PixelZDatabase.I.fireball, sortingLayer, 0.55f);
            p.explodes = true;
        }
        else
        {
            p = Create("Thrown Spear", pos, dir, PixelZDatabase.I.arrow, null, sortingLayer, 1.1f);
            p.GetComponent<SpriteRenderer>().color = new Color(1f, 0.6f, 0.9f);
        }
        p.fromPlayer = false;
        p.speed = def.projectileSpeed;
        p.damage = damage;
        p.life = 3f;
        p.radius = 0.28f;
    }

    // Bola de fuego lenta del ataque especial de los jefes.
    public static void SpawnFireball(Vector2 pos, Vector2 dir, float speed, int damage, string sortingLayer)
    {
        Projectile p = Create("Boss Fireball", pos, dir, null, PixelZDatabase.I.fireball, sortingLayer, 0.8f);
        p.explodes = true;
        p.fromPlayer = false;
        p.speed = speed;
        p.damage = damage;
        p.life = 7f;
        p.radius = 0.25f;
    }

    private static Projectile Create(string name, Vector2 pos, Vector2 dir, Sprite sprite, Sprite[] frames, string sortingLayer, float scale)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        FX.Unlit(sr);
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = 40;
        if (frames != null && frames.Length > 0)
        {
            var anim = go.AddComponent<SpriteAnimator>();
            anim.target = sr;
            anim.Play(frames, 12f, true);
        }
        var p = go.AddComponent<Projectile>();
        p.dir = dir.normalized;
        p.sortingLayer = sortingLayer;
        return p;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        Vector2 pos = (Vector2)transform.position + dir * speed * dt;
        transform.position = pos;

        if (life <= 0f)
        {
            Kill(false);
            return;
        }

        GameManager gm = GameManager.Instance;
        if (gm != null && gm.ProjectileWallMask != 0)
        {
            Collider2D wall = Physics2D.OverlapPoint(pos, gm.ProjectileWallMask);
            if (wall != null && !wall.isTrigger)
            {
                Kill(true);
                return;
            }
        }

        if (fromPlayer)
        {
            if (Breakable.HitPoint(pos, radius, pos - dir))
            {
                Kill(true);
                return;
            }
            foreach (Enemy e in Enemy.All.ToArray())
            {
                if (!e.Alive || alreadyHit.Contains(e)) continue;
                if (Vector2.Distance(e.HitCenter, pos) > radius + e.HitRadius) continue;

                alreadyHit.Add(e);
                e.TakeDamage(damage, dir * knockback);
                pierce--;
                if (pierce <= 0)
                {
                    Kill(true);
                    return;
                }
            }
        }
        else
        {
            foreach (PlayerController player in PlayerController.All)
            {
                if (player.Dead || Vector2.Distance(player.Center, pos) >= radius + 0.3f) continue;
                player.TakeHit((int)damage, pos - dir);
                Kill(true);
                return;
            }
        }
    }

    private void Kill(bool impact)
    {
        if (impact)
        {
            if (explodes) FX.Explosion(transform.position, 0.6f, sortingLayer);
            else FX.Burst(transform.position, new Color(0.9f, 0.85f, 0.7f), 4, 1.5f, 0.05f, sortingLayer);
        }
        Destroy(gameObject);
    }
}
