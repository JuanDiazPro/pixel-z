using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Enemigo genérico: persigue al jugador, ataca cuerpo a cuerpo o a distancia y suelta botín al morir.
public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    public EnemyDef Def { get; private set; }
    public bool Alive => state != State.Dead;
    public Vector2 HitCenter => (Vector2)transform.position + Vector2.up * 0.4f * Def.scale;
    public float HitRadius => 0.32f * Def.scale;
    public float HealthPercent => hp / maxHp;

    private enum State { Spawning, Chase, Attack, Special, Dead }

    private State state;
    private float stateTime;
    private float hp, maxHp, speed, attackCooldown, animMult = 1f;
    private float attackTimer;
    private bool hitDone;
    private float stun;
    private Vector2 knock;
    private float flash;
    private Vector2 strafe;
    private float strafeTimer;
    private float stuckTime, unstickTime;
    private float specialTimer = 4f;
    private int volleysFired;
    private float ringOffset;
    private Transform warning;
    private float unstickSign = 1f;

    private Rigidbody2D rb;
    private Collider2D col;
    private SortingGroup group;
    private SpriteRenderer body;
    private SpriteAnimator anim;
    private CharacterArt art;
    private SpriteRenderer rootRenderer;
    private Transform barRoot, barFill;

    // Los enemigos usan la misma capa física que el nivel donde aparecen (Layer 1/2/3), como el jugador:
    // chocan con muros, props y bordes de ese nivel y cambian de nivel al cruzar las escaleras.
    public static Enemy Spawn(EnemyDef def, NavPoint point, float hpMult, float speedMult, float attackSpeedMult)
    {
        Vector2 position = point.position;
        var go = new GameObject("Enemy - " + def.name);
        go.transform.position = position;
        go.layer = point.layer;
        string sortingLayer = SortingLayerFor(point.layer);

        // SpriteRenderer vacío en la raíz: las escaleras de Cainos lo usan para cambiar el sorting layer.
        var rootSr = go.AddComponent<SpriteRenderer>();
        rootSr.sortingLayerName = sortingLayer;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.mass = def.boss ? 30f : def.scale;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.22f * def.scale;
        col.offset = new Vector2(0f, 0.12f * def.scale);

        var group = go.AddComponent<SortingGroup>();
        group.sortingLayerName = sortingLayer;
        group.sortingOrder = PlayerController.Instance != null ? PlayerController.Instance.SortingOrder : 2;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * def.scale;
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.color = def.tint;
        var anim = visual.AddComponent<SpriteAnimator>();
        anim.target = sr;

        var e = go.AddComponent<Enemy>();
        e.Def = def;
        e.rb = rb;
        e.col = col;
        e.group = group;
        e.rootRenderer = rootSr;
        e.body = sr;
        e.anim = anim;
        e.art = PixelZDatabase.I.Art(def.art);
        e.maxHp = e.hp = def.hp * hpMult;
        e.speed = def.speed * speedMult;
        e.attackCooldown = def.attackCooldown / attackSpeedMult;
        e.animMult = Mathf.Clamp(attackSpeedMult, 0.65f, 1.4f);   // en las primeras rondas el golpe se ve venir
        e.attackTimer = Random.Range(0.3f, 1f);
        e.CreateHealthBar();
        e.anim.Play(e.art.idle, 8f, true);
        All.Add(e);

        // Sin empujarse con el jugador: le persiguen y le pegan, pero no le bloquean el paso.
        if (PlayerController.Instance != null)
            foreach (Collider2D pc in PlayerController.Instance.GetComponents<Collider2D>())
                Physics2D.IgnoreCollision(col, pc);

        FX.Ring(position + Vector2.up * 0.2f, def.boss ? new Color(1f, 0.2f, 0.3f, 0.9f) : new Color(0.6f, 0.2f, 0.8f, 0.8f),
                0.3f, def.boss ? 5f : 1.6f, 0.5f, group.sortingLayerName);
        return e;
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    private static string SortingLayerFor(int physicsLayer)
    {
        string name = LayerMask.LayerToName(physicsLayer);
        foreach (SortingLayer l in SortingLayer.layers)
            if (l.name == name) return name;
        return PlayerController.Instance != null ? PlayerController.Instance.SortingLayer : "Layer 1";
    }

    private void CreateHealthBar()
    {
        barRoot = new GameObject("HealthBar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0f, 1.05f * Def.scale + 0.1f, 0f);

        Sprite white = PixelZDatabase.I.white;
        float width = Def.boss ? 1.6f : 0.7f;

        var bg = new GameObject("BG").AddComponent<SpriteRenderer>();
        bg.transform.SetParent(barRoot, false);
        bg.sprite = white;
        FX.Unlit(bg);
        bg.color = new Color(0f, 0f, 0f, 0.7f);
        bg.transform.localScale = new Vector3(width + 0.06f, 0.12f, 1f);
        bg.sortingOrder = 30;

        var fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
        fill.transform.SetParent(barRoot, false);
        fill.sprite = white;
        FX.Unlit(fill);
        fill.color = Def.boss ? new Color(0.9f, 0.2f, 0.9f) : new Color(0.9f, 0.2f, 0.2f);
        fill.sortingOrder = 31;
        barFill = fill.transform;
        barFill.localScale = new Vector3(width, 0.07f, 1f);

        barRoot.gameObject.SetActive(Def.boss);
    }

    private void Update()
    {
        stateTime += Time.deltaTime;
        attackTimer -= Time.deltaTime;
        stun -= Time.deltaTime;

        PlayerController player = PlayerController.Instance;
        // Sigue al sorting layer que ponen las escaleras al cambiar de nivel.
        if (rootRenderer != null && group.sortingLayerID != rootRenderer.sortingLayerID) group.sortingLayerID = rootRenderer.sortingLayerID;

        UpdateFlash();
        UpdateHealthBar();

        switch (state)
        {
            case State.Spawning:
            {
                float k = Mathf.Clamp01(stateTime / 0.55f);
                Color c = body.color;
                c.a = k;
                body.color = c;
                body.transform.localScale = Vector3.one * Def.scale * Mathf.Lerp(0.4f, 1f, k);
                if (k >= 1f) SetState(State.Chase);
                break;
            }
            case State.Chase:
                UpdateChase(player);
                break;
            case State.Attack:
                UpdateAttack(player);
                break;
            case State.Special:
                UpdateSpecial(player);
                break;
            case State.Dead:
                if (stateTime > 0.7f)
                {
                    Color c = body.color;
                    c.a = Mathf.Clamp01(1f - (stateTime - 0.7f) / 0.5f);
                    body.color = c;
                }
                if (stateTime > 1.2f) Destroy(gameObject);
                break;
        }
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || state == State.Spawning)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desired = Vector2.zero;
        PlayerController player = PlayerController.Instance;
        if (state == State.Chase && stun <= 0f && player != null && !player.Dead)
        {
            Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
            float dist = toPlayer.magnitude;
            Vector2 dir = FlowField.Instance != null ? FlowField.Instance.DirectionTo(transform.position, gameObject.layer, player.transform.position) : toPlayer.normalized;

            if (Def.ranged)
            {
                if (dist > Def.preferredDistance) desired = dir;
                else if (dist < Def.preferredDistance - 1.5f) desired = -toPlayer.normalized * 0.7f;
                else desired = strafe * 0.5f;
            }
            else if (dist > StopDistance)
            {
                desired = dir;
            }
        }

        // Si lleva un rato empujando contra algo sin moverse, rodea el obstáculo por un lado.
        if (desired.sqrMagnitude > 0.01f && rb.linearVelocity.sqrMagnitude < 0.02f * speed * speed) stuckTime += Time.fixedDeltaTime;
        else stuckTime = Mathf.Max(0f, stuckTime - Time.fixedDeltaTime);
        if (stuckTime > 0.4f)
        {
            stuckTime = 0f;
            unstickTime = 0.5f;
            unstickSign = Random.value < 0.5f ? -1f : 1f;
        }
        if (unstickTime > 0f)
        {
            unstickTime -= Time.fixedDeltaTime;
            desired = ((Vector2)(Quaternion.Euler(0f, 0f, 80f * unstickSign) * desired)).normalized;
        }

        rb.linearVelocity = desired * speed + knock;
        knock = Vector2.MoveTowards(knock, Vector2.zero, (Def.boss ? 60f : 25f) * Time.fixedDeltaTime);
    }

    private float StopDistance => 0.45f * Def.scale;

    private void UpdateChase(PlayerController player)
    {
        if (player == null || player.Dead)
        {
            anim.Play(art.idle, 8f, true);
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
        float dist = toPlayer.magnitude;
        if (stun <= 0f) body.flipX = toPlayer.x < 0f;

        strafeTimer -= Time.deltaTime;
        if (strafeTimer <= 0f)
        {
            strafeTimer = Random.Range(1f, 2f);
            strafe = Vector2.Perpendicular(toPlayer.normalized) * (Random.value < 0.5f ? -1f : 1f);
        }

        // Archidemonio: de vez en cuando lanza un anillo de bolas de fuego (como la lluvia de flechas, pero lento).
        if (Def.fireRing && stun <= 0f)
        {
            specialTimer -= Time.deltaTime;
            if (specialTimer <= 0f && dist < 11f)
            {
                StartSpecial();
                return;
            }
        }

        bool inRange = Def.ranged ? dist < Def.preferredDistance + 1.5f : dist < Def.attackRange + 0.25f;
        if (inRange && attackTimer <= 0f && stun <= 0f)
        {
            SetState(State.Attack);
            hitDone = false;
            Sprite[] frames = (Random.value < 0.5f || art.attack2 == null) ? art.attack1 : art.attack2;
            anim.Play(frames, Def.attackAnimFps * animMult, false, true);
            return;
        }

        if (stun > 0f) return;
        bool moving = rb.linearVelocity.sqrMagnitude > 0.05f;
        anim.Play(moving ? art.walk : art.idle, moving ? 9f * Mathf.Clamp(speed / 1.6f, 0.7f, 1.8f) : 8f, true);
    }

    private void UpdateAttack(PlayerController player)
    {
        int hitFrame = Mathf.Max(1, anim.Current.Length / 2);
        if (!hitDone && anim.Frame >= hitFrame)
        {
            hitDone = true;
            if (player != null && !player.Dead)
            {
                if (Def.ranged)
                {
                    Vector2 from = HitCenter;
                    Vector2 dir = (player.Center - from).normalized;
                    Projectile.SpawnEnemyShot(Def, from + dir * 0.3f, dir, Def.damage, group.sortingLayerName);
                }
                else
                {
                    Vector2 facing = body.flipX ? Vector2.left : Vector2.right;
                    Vector2 hitPoint = (Vector2)transform.position + facing * 0.3f * Def.scale;
                    // Sólo golpea si está en el mismo nivel (no a través de un desnivel).
                    if (gameObject.layer == player.gameObject.layer && Vector2.Distance(player.transform.position, hitPoint) <= Def.attackRange + 0.2f)
                        player.TakeHit(Def.damage, transform.position);
                    if (Def.boss) FX.Shake(0.12f, 0.15f);
                }
            }
        }

        if (anim.IsDone)
        {
            attackTimer = attackCooldown * Random.Range(0.85f, 1.15f);
            SetState(State.Chase);
        }
    }

    // ---------- Ataque especial de jefe ----------

    private int LocalWave => WaveDirector.Instance != null ? Mathf.Max(1, WaveDirector.Instance.LocalWave) : 1;

    private void StartSpecial()
    {
        SetState(State.Special);
        volleysFired = 0;
        ringOffset = Random.value * Mathf.PI * 2f;
        anim.Play(art.attack2 ?? art.attack1, 5f, false, true);

        // Aviso: un círculo rojo que crece alrededor del jefe antes de disparar.
        var go = new GameObject("Boss Warning");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.4f * Def.scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PixelZDatabase.I.ring;
        sr.sortingOrder = 29;
        FX.Unlit(sr);
        warning = go.transform;
        GameHUD.Popup(HitCenter + Vector2.up * Def.scale, "¡!", new Color(1f, 0.4f, 0.2f));
    }

    private void UpdateSpecial(PlayerController player)
    {
        const float windup = 0.9f;
        bool doubleRing = LocalWave >= 10;

        if (warning != null)
        {
            float k = Mathf.Clamp01(stateTime / windup);
            warning.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.6f, k) * Def.scale * 0.6f;
            warning.GetComponent<SpriteRenderer>().color = new Color(1f, 0.35f, 0.1f, Mathf.Repeat(stateTime * 8f, 1f) > 0.5f ? 0.9f : 0.4f);
        }

        if (volleysFired == 0 && stateTime >= windup)
        {
            FireRing(0f);
            if (warning != null) Destroy(warning.gameObject);
        }
        else if (volleysFired == 1 && doubleRing && stateTime >= windup + 0.6f)
        {
            FireRing(0.5f);   // segundo anillo desplazado medio hueco
        }

        float end = windup + (doubleRing ? 1.1f : 0.5f);
        if (stateTime >= end)
        {
            specialTimer = Random.Range(5f, 9f);
            attackTimer = 0.8f;
            SetState(State.Chase);
        }
    }

    private void FireRing(float halfStep)
    {
        volleysFired++;
        int w = LocalWave;
        int count = Mathf.Min(10 + w / 3, 18);
        float speed = Mathf.Min(3f + 0.06f * w, 4.5f);   // las flechas del arquero van a 12: esto es lento y esquivable
        Vector2 origin = HitCenter;
        string layer = group.sortingLayerName;

        for (int i = 0; i < count; i++)
        {
            float a = ringOffset + (i + halfStep) * Mathf.PI * 2f / count;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Projectile.SpawnFireball(origin + dir * 0.5f * Def.scale, dir, speed, 1, layer);
        }
        FX.Ring(origin, new Color(1f, 0.45f, 0.1f, 0.9f), 0.5f, 4f, 0.4f, layer);
        FX.Shake(0.15f, 0.2f);
    }

    public void TakeDamage(float damage, Vector2 knockback)
    {
        if (!Alive) return;

        hp -= damage;
        flash = 0.12f;
        knock = knockback * (Def.boss ? 0.15f : 1f / Mathf.Max(0.8f, Def.scale));
        GameHUD.Popup(HitCenter + Vector2.up * 0.5f * Def.scale, Mathf.CeilToInt(damage).ToString(), new Color(1f, 0.95f, 0.6f));
        FX.Burst(HitCenter, new Color(0.85f, 0.1f, 0.1f), 5, 2.5f, 0.07f, group.sortingLayerName);
        barRoot.gameObject.SetActive(true);

        if (hp <= 0f)
        {
            Die();
            return;
        }

        if (!Def.boss && state != State.Attack)
        {
            stun = 0.18f;
            anim.Play(art.hurt, 16f, false, true);
        }
    }

    private void Die()
    {
        if (warning != null) Destroy(warning.gameObject);
        SetState(State.Dead);
        All.Remove(this);
        col.enabled = false;
        barRoot.gameObject.SetActive(false);
        anim.Play(art.death, 8f, false, true);
        body.color = Def.tint;

        string layer = group.sortingLayerName;
        FX.Burst(HitCenter, new Color(0.7f, 0.05f, 0.1f), Def.boss ? 30 : 10, Def.boss ? 6f : 3.5f, 0.09f, layer);
        if (Def.boss)
        {
            FX.Explosion(HitCenter, 1.2f, layer);
            FX.Shake(0.4f, 0.5f);
        }

        Loot.DropFor(this, layer);
        if (GameManager.Instance != null) GameManager.Instance.OnEnemyKilled(this);
    }

    private void SetState(State s)
    {
        state = s;
        stateTime = 0f;
    }

    private void UpdateFlash()
    {
        if (state == State.Spawning || state == State.Dead) return;
        flash -= Time.deltaTime;
        Color c = flash > 0f ? new Color(1f, 0.35f, 0.35f) : Def.tint;
        c.a = 1f;
        body.color = c;
        body.transform.localScale = Vector3.one * Def.scale * (flash > 0f ? 1.08f : 1f);
    }

    private void UpdateHealthBar()
    {
        if (barFill == null) return;
        float width = Def.boss ? 1.6f : 0.7f;
        float k = Mathf.Clamp01(hp / maxHp);
        barFill.localScale = new Vector3(width * k, 0.07f, 1f);
        barFill.localPosition = new Vector3(-width * (1f - k) * 0.5f, 0f, 0f);
    }
}
