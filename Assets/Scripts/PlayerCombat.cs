using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kilic saldirisi ve 2'li kombo: 1. vurus "Attack" (yukaridan), saldiri sirasinda tekrar basilirsa
/// 2. vurus "Slash" (yatay). Yerdeyken saldiri sirasinda karakter durur (soulslike), havadayken
/// hareket devam eder.
///
/// Tuslar: Sol tik / J / gamepad X
///
/// Hasar: vurus aninda onundeki alanda kalan objelerin Health bilesenine hasar verir
/// (Health yoksa SendMessage("TakeDamage", int) dener). Dusmana Health eklemen yeterli.
/// Her vurus stamina harcar; stamina yoksa saldiri baslamaz, kombo devam etmez.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputAction attackAction = new InputAction("Attack", InputActionType.Button);

    [Header("Kombo")]
    [Tooltip("1. vurusun suresi (sn). Attack klibinin uzunluguyla ayni: 4 kare / 12 fps = 0.33")]
    [SerializeField] private float attackDuration = 0.33f;
    [Tooltip("2. vurusun suresi (sn). Slash klibi: 4 kare / 12 fps = 0.33")]
    [SerializeField] private float slashDuration = 0.33f;
    [Tooltip("Kombo bittikten sonra yeni saldiriya kadar bekleme (sn).")]
    [SerializeField] private float attackCooldown = 0.15f;

    [Header("Stamina")]
    [SerializeField, Min(0f)] private float attackStaminaCost = 15f;
    [SerializeField, Min(0f)] private float slashStaminaCost = 18f;

    [Header("Hasar")]
    [Tooltip("1. vurusun (Attack) hasari")]
    [SerializeField, Min(0)] private int attackDamage = 15;
    [Tooltip("2. vurusun (Slash) hasari")]
    [SerializeField, Min(0)] private int slashDamage = 20;
    [Tooltip("Vurulan dusmani geri itme gucu")]
    [SerializeField, Min(0f)] private float knockback = 4f;
    [Tooltip("Saldiri basladiktan kac sn sonra vurus gerceklesir (kilicin indigi kare).")]
    [SerializeField] private float hitDelay = 0.17f;
    [Tooltip("Vurus alaninin merkezi, karakterin ayagina gore (x: one dogru, y: yukari).")]
    [SerializeField] private Vector2 attackOffset = new Vector2(1.3f, 1.6f);
    [SerializeField] private float attackRadius = 1.1f;
    [Tooltip("Hangi katmanlardaki objeler vurulabilir. Dusmanlara 'Enemy' katmani verip sadece onu secmen onerilir.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    private static readonly int AttackStateHash = Animator.StringToHash("Attack");
    private static readonly int SlashStateHash = Animator.StringToHash("Slash");

    private enum AttackState { None, Attack, Slash }

    private PlayerMovement movement;
    private Animator animator;
    private SwordSwingSound swingSound;

    private AttackState state = AttackState.None;
    private float stateStartTime;
    private float stateEndTime;
    private bool hitDone;
    private bool comboQueued;
    private float nextAttackTime;

    private readonly List<Collider2D> hitBuffer = new List<Collider2D>();
    private readonly HashSet<GameObject> hitThisSwing = new HashSet<GameObject>();

    public bool IsAttacking => state != AttackState.None;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animator = GetComponent<Animator>();

        if (attackAction == null)
            attackAction = new InputAction("Attack", InputActionType.Button);
        // Inspector'da girilmis tuslar korunur; eksik olanlar eklenir (sol fare tusu her zaman saldiri)
        AddBindingIfMissing(attackAction, "<Mouse>/leftButton");
        AddBindingIfMissing(attackAction, "<Keyboard>/j");
        AddBindingIfMissing(attackAction, "<Gamepad>/buttonWest");

        // Kilic sesini artik bu script tetikliyor
        swingSound = GetComponent<SwordSwingSound>();
        if (swingSound == null) swingSound = gameObject.AddComponent<SwordSwingSound>();
        swingSound.PlayOnInput = false;
    }

    private static void AddBindingIfMissing(InputAction action, string path)
    {
        foreach (var b in action.bindings)
            if (string.Equals(b.path, path, System.StringComparison.OrdinalIgnoreCase)) return;
        action.AddBinding(path);
    }

    private void OnEnable() { attackAction.Enable(); }
    private void OnDisable()
    {
        attackAction.Disable();
        if (movement != null) movement.MovementLocked = false;
        state = AttackState.None;
    }

    private void Update()
    {
        // Duraklatma menusu acikken saldiri okunmaz; zamanlayicilar zaten Time.time ile durur
        if (PauseMenu.BlocksGameplayInput) return;

        if (attackAction.WasPressedThisFrame())
        {
            if (state == AttackState.None)
            {
                if (Time.time >= nextAttackTime && !movement.IsDashing && !movement.IsClimbing && !movement.IsHurt && !movement.IsDead && !movement.ActionLock &&
                    movement.Stamina.TrySpend(attackStaminaCost))
                    StartAttack(AttackState.Attack);
            }
            else if (state == AttackState.Attack)
            {
                comboQueued = true;
            }
        }

        if (state == AttackState.None) return;

        // Vurulunca ya da olunce saldiri iptal
        if (movement.IsHurt || movement.IsDead)
        {
            EndAttack();
            return;
        }

        if (!hitDone && Time.time >= stateStartTime + hitDelay)
        {
            hitDone = true;
            DoHit();
        }

        if (Time.time >= stateEndTime)
        {
            if (state == AttackState.Attack && comboQueued && movement.Stamina.TrySpend(slashStaminaCost))
                StartAttack(AttackState.Slash);
            else
                EndAttack();
        }
    }

    private void StartAttack(AttackState next)
    {
        state = next;
        comboQueued = false;
        hitDone = false;
        stateStartTime = Time.time;
        stateEndTime = Time.time + (next == AttackState.Attack ? attackDuration : slashDuration);

        // Yerdeyken saldiri karakteri sabitler, havada momentum korunur
        movement.MovementLocked = movement.IsGrounded;

        PlayState(next == AttackState.Attack ? AttackStateHash : SlashStateHash);
        if (swingSound != null) swingSound.PlaySwing();
    }

    private void EndAttack()
    {
        state = AttackState.None;
        movement.MovementLocked = false;
        nextAttackTime = Time.time + attackCooldown;
    }

    private void DoHit()
    {
        Vector2 center = GetAttackCenter();
        var filter = new ContactFilter2D();
        filter.SetLayerMask(hitLayers);
        filter.useTriggers = true;

        hitThisSwing.Clear();
        Physics2D.OverlapCircle(center, attackRadius, filter, hitBuffer);
        foreach (Collider2D col in hitBuffer)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue; // kendimize vurmayalim
            // Birden fazla collider'i olan dusmana tek vurusta iki kez hasar vermeyelim
            GameObject target = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (!hitThisSwing.Add(target)) continue;
            int dmg = state == AttackState.Slash ? slashDamage : attackDamage;
            var health = col.GetComponentInParent<Health>();
            if (health != null)
                health.TakeDamage(new DamageInfo(dmg, transform.position, knockback));
            else
                col.SendMessageUpwards("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);
        }
    }

    private Vector2 GetAttackCenter()
    {
        int facing = movement != null ? movement.FacingDirection : 1;
        return (Vector2)transform.position + new Vector2(attackOffset.x * facing, attackOffset.y);
    }

    private void PlayState(int stateHash)
    {
        if (animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, stateHash))
            animator.Play(stateHash, 0, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (movement == null) movement = GetComponent<PlayerMovement>();
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackCenter(), attackRadius);
    }
}
