using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Controlador del Knight para el mapa top-down de Cainos.
// WASD = mover, Espacio = saltar, Click izquierdo = atacar.
// El collider queda en los pies (raíz) y sólo el sprite ("Visual") sube al saltar,
// así el personaje sigue chocando con paredes y props aunque esté en el aire.
[RequireComponent(typeof(Rigidbody2D))]
public class KnightController : MonoBehaviour
{
    [Header("Movimiento")]
    public float speed = 3f;

    [Header("Salto")]
    public float jumpHeight = 0.6f;
    public float jumpDuration = 0.5f;

    [Header("Ataque")]
    public float attackDuration = 0.4f;
    public float hitDelay = 0.15f;
    public float attackRadius = 0.5f;
    public Vector2 attackOffset = new Vector2(0.8f, 0.5f);

    [Header("Referencias")]
    public Transform visual;
    public SpriteRenderer body;
    public Animator animator;
    public Transform shadow;

    private Rigidbody2D rb;
    private SpriteRenderer layerRenderer;   // SpriteRenderer de la raíz: StairsLayerTrigger le cambia el sorting layer
    private SortingGroup sortingGroup;

    private Vector2 input;
    private bool facingLeft;
    private float jumpTimer = -1f;
    private float attackTimer = -1f;
    private bool hitDone;
    private string currentState;
    private Vector3 shadowScale;

    private bool IsJumping => jumpTimer >= 0f;
    private bool IsAttacking => attackTimer >= 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        layerRenderer = GetComponent<SpriteRenderer>();
        sortingGroup = GetComponent<SortingGroup>();
        if (shadow != null) shadowScale = shadow.localScale;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        input = Vector2.zero;
        if (kb != null)
        {
            if (kb.aKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed) input.x += 1f;
            if (kb.sKey.isPressed) input.y -= 1f;
            if (kb.wKey.isPressed) input.y += 1f;
        }
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (!IsAttacking && input.x != 0f) facingLeft = input.x < 0f;
        body.flipX = facingLeft;

        if (kb != null && kb.spaceKey.wasPressedThisFrame && !IsJumping) jumpTimer = 0f;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsAttacking)
        {
            attackTimer = 0f;
            hitDone = false;
            currentState = null;    // fuerza reiniciar la animación de ataque
        }

        UpdateJump();
        UpdateAttack();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        // En el suelo el ataque detiene al personaje; en el aire conserva el impulso.
        bool rooted = IsAttacking && !IsJumping;
        rb.linearVelocity = rooted ? Vector2.zero : input * speed;
    }

    private void LateUpdate()
    {
        // Mantiene el SortingGroup en el mismo sorting layer que asignan las escaleras de Cainos.
        if (sortingGroup != null && layerRenderer != null && sortingGroup.sortingLayerID != layerRenderer.sortingLayerID)
            sortingGroup.sortingLayerID = layerRenderer.sortingLayerID;
    }

    private void UpdateJump()
    {
        float height = 0f;
        if (IsJumping)
        {
            jumpTimer += Time.deltaTime;
            float t = jumpTimer / jumpDuration;
            if (t >= 1f) jumpTimer = -1f;
            else height = 4f * jumpHeight * t * (1f - t);
        }

        visual.localPosition = new Vector3(0f, height, 0f);
        if (shadow != null) shadow.localScale = shadowScale * (1f - 0.4f * (height / Mathf.Max(jumpHeight, 0.01f)));
    }

    private void UpdateAttack()
    {
        if (!IsAttacking) return;

        attackTimer += Time.deltaTime;
        if (!hitDone && attackTimer >= hitDelay)
        {
            hitDone = true;
            DoHit();
        }
        if (attackTimer >= attackDuration) attackTimer = -1f;
    }

    private void DoHit()
    {
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(AttackPoint(), attackRadius))
        {
            if (hit.attachedRigidbody == rb) continue;
            hit.SendMessage("OnHit", this, SendMessageOptions.DontRequireReceiver);
        }
    }

    private Vector2 AttackPoint()
    {
        Vector2 offset = attackOffset;
        if (facingLeft) offset.x = -offset.x;
        return (Vector2)transform.position + offset;
    }

    private void UpdateAnimation()
    {
        string state;
        if (IsAttacking) state = IsJumping ? "JumpAttack" : "Attack";
        else if (IsJumping) state = "Jump";
        else if (input != Vector2.zero) state = "Walk";
        else state = "Idle";

        if (state == currentState) return;
        currentState = state;
        animator.Play(state, 0, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPoint(), attackRadius);
    }
}
