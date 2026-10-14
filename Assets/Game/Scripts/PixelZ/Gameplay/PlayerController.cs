using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Soldado jugable (Tiny RPG 01).
// WASD mover · Click izq. (mantener) atacar hacia el ratón · Espacio/Shift dash · Click der./Q habilidad.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public PlayerClassDef Def { get; private set; }
    public bool Dead { get; private set; }
    public Vector2 Center => (Vector2)transform.position + Vector2.up * 0.45f;
    public string SortingLayer => group != null ? group.sortingLayerName : "Layer 1";
    public int SortingOrder => group != null ? group.sortingOrder : 2;

    public float AbilityCooldown => Def.abilityCooldown * Mathf.Pow(0.88f, GameSession.CooldownLevel);
    public float AbilityRemaining => abilityTimer;
    public float DashCooldown => 1.1f;
    public float DashRemaining => dashCd;
    public float SpeedBuffRemaining => speedBuff;
    public float FuryRemaining => furyTime;
    public float ShieldRemaining => shieldTime;
    public int Damage => Def.damage + GameSession.DamageLevel;
    public float MagnetRadius => 1.6f + 0.8f * GameSession.MagnetLevel;

    private const int MeleeHitFrame = 3;
    private const int BowHitFrame = 6;

    private Rigidbody2D rb;
    private SpriteRenderer body;
    private SpriteAnimator anim;
    private SortingGroup group;
    private SpriteRenderer layerRenderer;
    private CharacterArt art;

    private Vector2 input;
    private Vector2 aim = Vector2.right;
    private bool facingLeft;

    private bool attacking;
    private bool hitDone;
    private int combo;
    private float attackTimer;
    private Vector2 attackDir;
    private bool abilityAttack;

    private float abilityTimer;
    private float dashCd, dashTime;
    private Vector2 dashDir;
    private float invuln, hurtTime;
    private Vector2 knock;
    private float speedBuff, furyTime, shieldTime;
    private Transform shieldFx;
    private float afterImageTimer;

    public static PlayerController Create(Vector3 position, int physicsLayer, string sortingLayer, int sortingOrder, PlayerClassDef def)
    {
        var go = new GameObject("Player - " + def.name);
        go.tag = "Player";
        go.layer = physicsLayer;
        go.transform.position = position;

        // SpriteRenderer vacío en la raíz: lo necesitan las escaleras de Cainos para cambiar de nivel.
        var rootSr = go.AddComponent<SpriteRenderer>();
        rootSr.sortingLayerName = sortingLayer;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.5f, 0.32f);
        col.offset = new Vector2(0f, 0.14f);

        // Mismo orden que los props de Cainos (2) para ordenarse por altura con pasto, árboles y cofres.
        var group = go.AddComponent<SortingGroup>();
        group.sortingLayerName = sortingLayer;
        group.sortingOrder = sortingOrder;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = sortingLayer;
        sr.color = def.tint;
        var anim = visual.AddComponent<SpriteAnimator>();
        anim.target = sr;

        var pc = go.AddComponent<PlayerController>();
        pc.Def = def;
        pc.body = sr;
        pc.anim = anim;
        return pc;
    }

    private void Awake()
    {
        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        group = GetComponent<SortingGroup>();
        layerRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        art = PixelZDatabase.I.soldier;
        anim.Play(art.idle, 8f, true);
        invuln = 1.5f;   // protección breve al aparecer
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Dead || Time.timeScale == 0f) return;

        ReadInput();
        TickTimers();
        UpdateAttack();
        UpdateAnimation();
        UpdateVisualEffects();
    }

    private void FixedUpdate()
    {
        if (Dead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 v;
        if (dashTime > 0f) v = dashDir * CurrentSpeed * 3.3f;
        else v = input * CurrentSpeed * (attacking ? 0.6f : 1f);

        rb.linearVelocity = v + knock;
        knock = Vector2.MoveTowards(knock, Vector2.zero, 40f * Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        // Las escaleras de Cainos cambian el sorting layer del SpriteRenderer raíz; el grupo lo sigue.
        if (group != null && layerRenderer != null && group.sortingLayerID != layerRenderer.sortingLayerID)
            group.sortingLayerID = layerRenderer.sortingLayerID;
    }

    private float CurrentSpeed =>
        Def.speed * (1f + 0.08f * GameSession.SpeedLevel) * (speedBuff > 0f ? 1.6f : 1f) * (furyTime > 0f ? 1.25f : 1f);

    private float AttackCooldown =>
        Def.attackCooldown * Mathf.Pow(0.92f, GameSession.AttackSpeedLevel) * (furyTime > 0f ? 0.5f : 1f);

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        input = Vector2.zero;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
        }
        if (input.sqrMagnitude > 1f) input.Normalize();

        Camera cam = Camera.main;
        if (mouse != null && cam != null)
        {
            Vector3 mp = mouse.position.ReadValue();
            mp.z = -cam.transform.position.z;
            Vector2 world = cam.ScreenToWorldPoint(mp);
            Vector2 d = world - Center;
            if (d.sqrMagnitude > 0.01f) aim = d.normalized;
        }

        bool dashPressed = kb != null && (kb.spaceKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame);
        if (dashPressed && dashCd <= 0f && dashTime <= 0f) StartDash();

        bool abilityPressed = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (kb != null && kb.qKey.wasPressedThisFrame);
        if (abilityPressed && abilityTimer <= 0f) UseAbility();

        bool attackHeld = mouse != null && mouse.leftButton.isPressed && !UIBlocksMouse();
        if (attackHeld && !attacking && attackTimer <= 0f) StartAttack(false);

        if (!attacking && input.x != 0f) facingLeft = input.x < 0f;
        body.flipX = facingLeft;
    }

    private static bool UIBlocksMouse()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    private void TickTimers()
    {
        float dt = Time.deltaTime;
        attackTimer -= dt;
        abilityTimer = Mathf.Max(0f, abilityTimer - dt);
        dashCd = Mathf.Max(0f, dashCd - dt);
        dashTime -= dt;
        invuln -= dt;
        hurtTime -= dt;
        speedBuff = Mathf.Max(0f, speedBuff - dt);
        furyTime = Mathf.Max(0f, furyTime - dt);
        shieldTime = Mathf.Max(0f, shieldTime - dt);
    }

    // ---------- Ataque ----------

    private void StartAttack(bool fromAbility)
    {
        attacking = true;
        hitDone = false;
        abilityAttack = fromAbility;
        attackDir = aim;
        facingLeft = aim.x < 0f;

        Sprite[] frames;
        float duration;
        if (Def.ranged)
        {
            frames = art.attack3;
            duration = Mathf.Min(0.42f, AttackCooldown * 0.95f);
        }
        else
        {
            frames = combo % 2 == 0 ? art.attack1 : art.attack2;
            duration = Mathf.Min(0.36f, AttackCooldown * 0.95f);
        }
        combo++;
        if (fromAbility) duration = 0.3f;

        anim.Play(frames, frames.Length / Mathf.Max(duration, 0.12f), false, true);
        attackTimer = AttackCooldown;
    }

    private void UpdateAttack()
    {
        if (!attacking) return;

        int hitFrame = Def.ranged ? BowHitFrame : MeleeHitFrame;
        if (!hitDone && anim.Frame >= hitFrame)
        {
            hitDone = true;
            if (!abilityAttack)
            {
                if (Def.ranged) ShootArrow(attackDir, 0);
                else MeleeHit();
            }
        }

        if (anim.IsDone) attacking = false;
    }

    private void MeleeHit()
    {
        float radius = Def.attackRadius;
        Vector2 center = Center + attackDir * (radius * 0.75f);
        int hits = 0;
        foreach (Enemy e in Enemy.All.ToArray())
        {
            if (!e.Alive) continue;
            if (Vector2.Distance(e.HitCenter, center) <= radius + e.HitRadius)
            {
                e.TakeDamage(Damage, (e.HitCenter - Center).normalized * Def.knockback);
                hits++;
            }
        }
        Breakable.HitArea(center, radius, Center);
        FX.Burst(center, new Color(1f, 1f, 1f, 0.8f), 4, 2f, 0.06f, SortingLayer);
        if (hits > 0) FX.Shake(0.05f, 0.08f);
    }

    private void ShootArrow(Vector2 dir, int extraPierce)
    {
        Projectile.SpawnPlayerArrow(Center + dir * 0.35f, dir, Damage, 1 + extraPierce, Def.knockback, SortingLayer);
    }

    // ---------- Habilidades ----------

    private void UseAbility()
    {
        abilityTimer = AbilityCooldown;
        string layer = SortingLayer;

        switch (Def.ability)
        {
            case AbilityType.Whirlwind:
            {
                StartAttack(true);
                anim.Play(art.attack2, 24f, false, true);
                float radius = 2.1f;
                foreach (Enemy e in Enemy.All.ToArray())
                    if (e.Alive && Vector2.Distance(e.HitCenter, Center) <= radius + e.HitRadius)
                        e.TakeDamage(Damage * 2, (e.HitCenter - Center).normalized * 7f);
                Breakable.HitArea(Center, radius, Center);
                FX.Ring(Center, new Color(1f, 0.95f, 0.7f, 0.9f), 0.6f, radius * 2.2f, 0.35f, layer);
                FX.Burst(Center, Color.white, 16, 6f, 0.08f, layer);
                FX.Shake(0.15f, 0.2f);
                break;
            }
            case AbilityType.ArrowRain:
            {
                StartAttack(true);
                const int count = 16;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count;
                    ShootArrow(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 2);
                }
                FX.Ring(Center, new Color(0.7f, 1f, 0.6f, 0.9f), 0.5f, 3f, 0.3f, layer);
                break;
            }
            case AbilityType.Shield:
            {
                shieldTime = 3f;
                float radius = 2.6f;
                foreach (Enemy e in Enemy.All.ToArray())
                    if (e.Alive && Vector2.Distance(e.HitCenter, Center) <= radius + e.HitRadius)
                        e.TakeDamage(Damage, (e.HitCenter - Center).normalized * 10f);
                Breakable.HitArea(Center, radius, Center);
                FX.Ring(Center, new Color(0.5f, 0.8f, 1f, 1f), 0.6f, radius * 2.3f, 0.45f, layer);
                FX.Glow(Center, new Color(0.4f, 0.7f, 1f, 0.7f), 1f, 5f, 0.5f, layer);
                FX.Shake(0.18f, 0.25f);
                break;
            }
            case AbilityType.Fury:
            {
                furyTime = 5f;
                attackTimer = 0f;
                FX.Ring(Center, new Color(1f, 0.3f, 0.2f, 1f), 0.5f, 4f, 0.4f, layer);
                FX.Burst(Center, new Color(1f, 0.35f, 0.2f), 18, 5f, 0.09f, layer);
                break;
            }
        }
    }

    // ---------- Dash ----------

    private void StartDash()
    {
        dashDir = input != Vector2.zero ? input : aim;
        dashTime = 0.16f;
        dashCd = DashCooldown;
        FX.Burst(transform.position + Vector3.up * 0.2f, new Color(1f, 1f, 1f, 0.6f), 8, 2.5f, 0.07f, SortingLayer);
    }

    // ---------- Vida ----------

    public bool IsInvulnerable => invuln > 0f || dashTime > 0f || shieldTime > 0f || SceneTransition.IsBusy;

    public bool TakeHit(int damage, Vector2 sourcePos)
    {
        if (Dead || IsInvulnerable || GameManager.Instance == null || GameManager.Instance.IsOver) return false;

        GameSession.Hearts = Mathf.Max(0, GameSession.Hearts - damage);
        invuln = 1.3f;
        hurtTime = 0.25f;
        knock = ((Vector2)transform.position - sourcePos).normalized * 7f;

        FX.Shake(0.25f, 0.25f);
        FX.Burst(Center, new Color(1f, 0.2f, 0.2f), 10, 3.5f, 0.08f, SortingLayer);
        GameHUD.Popup(Center + Vector2.up * 0.6f, "-" + damage + " ♥", new Color(1f, 0.3f, 0.3f));
        GameHUD.FlashDamage();

        if (GameSession.Hearts <= 0) Die();
        else if (!attacking) anim.Play(art.hurt, 14f, false, true);
        return true;
    }

    public void Heal(int amount)
    {
        GameSession.Hearts = Mathf.Min(GameSession.MaxHearts, GameSession.Hearts + amount);
        FX.Glow(Center, new Color(1f, 0.3f, 0.4f, 0.7f), 0.5f, 2.5f, 0.5f, SortingLayer);
        GameHUD.Popup(Center + Vector2.up * 0.6f, "+" + amount + " ♥", new Color(1f, 0.45f, 0.55f));
    }

    public void ApplySpeedBuff(float seconds)
    {
        speedBuff = Mathf.Max(speedBuff, seconds);
        FX.Ring(Center, new Color(1f, 0.3f, 0.3f, 1f), 0.4f, 2.5f, 0.4f, SortingLayer);
        GameHUD.Popup(Center + Vector2.up * 0.6f, "¡Velocidad!", new Color(1f, 0.5f, 0.4f));
    }

    private void Die()
    {
        Dead = true;
        attacking = false;
        rb.linearVelocity = Vector2.zero;
        body.color = Def.tint;
        anim.Play(art.death, 7f, false, true);
        FX.Shake(0.4f, 0.4f);
        GameManager.Instance.OnPlayerDied();
    }

    // ---------- Animación / efectos ----------

    private void UpdateAnimation()
    {
        if (attacking) return;
        if (hurtTime > 0f && !anim.IsDone && anim.Current == art.hurt) return;

        if (input != Vector2.zero || dashTime > 0f)
            anim.Play(art.walk, 10f * (CurrentSpeed / 3.2f), true);
        else
            anim.Play(art.idle, 8f, true);
    }

    private void UpdateVisualEffects()
    {
        // Parpadeo de invulnerabilidad tras recibir daño.
        Color c = Def.tint;
        if (invuln > 0f && hurtTime < 1.2f && shieldTime <= 0f)
            c.a = Mathf.Repeat(Time.time * 12f, 1f) > 0.5f ? 0.35f : 1f;
        if (furyTime > 0f) c = Color.Lerp(c, new Color(1f, 0.25f, 0.2f, c.a), 0.4f + 0.2f * Mathf.Sin(Time.time * 20f));
        if (speedBuff > 0f) c = Color.Lerp(c, new Color(1f, 0.6f, 0.6f, c.a), 0.25f);
        body.color = c;

        // Estelas durante el dash o con la poción de velocidad.
        if (dashTime > 0f || speedBuff > 0f || furyTime > 0f)
        {
            afterImageTimer -= Time.deltaTime;
            if (afterImageTimer <= 0f && rb.linearVelocity.sqrMagnitude > 0.5f)
            {
                afterImageTimer = dashTime > 0f ? 0.03f : 0.09f;
                SpawnAfterImage(speedBuff > 0f || furyTime > 0f ? new Color(1f, 0.35f, 0.3f, 0.45f) : new Color(1f, 1f, 1f, 0.45f));
            }
        }

        // Burbuja del escudo divino.
        if (shieldTime > 0f)
        {
            if (shieldFx == null)
            {
                var go = new GameObject("Shield FX");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.up * 0.45f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PixelZDatabase.I.ring;
                sr.sortingOrder = 20;
                shieldFx = go.transform;
            }
            float pulse = 1.5f + 0.1f * Mathf.Sin(Time.time * 10f);
            shieldFx.localScale = Vector3.one * pulse;
            shieldFx.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.85f, 1f, shieldTime < 0.8f ? Mathf.Repeat(Time.time * 8f, 1f) : 0.85f);
        }
        else if (shieldFx != null)
        {
            Destroy(shieldFx.gameObject);
            shieldFx = null;
        }
    }

    private void SpawnAfterImage(Color color)
    {
        var go = new GameObject("AfterImage");
        go.transform.position = body.transform.position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = body.sprite;
        sr.flipX = body.flipX;
        sr.color = color;
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = -1;
        go.AddComponent<FXScaleFade>().Init(1f, 1f, 0.25f);
    }
}
