using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Oyuncu hareketi: yurume, ziplama, dash (ileri yuvarlanma) ve backstep (geri adim).
/// Animator parametrelerini (Speed, VelocityY, IsGrounded) her kare gunceller.
/// Animator Controller'i kurmak icin: Unity menusu > Tools > FarToHell > Player Animator Kur
///
/// Tuslar (varsayilan):
///   Hareket : A/D veya Sol/Sag ok, gamepad sol cubuk
///   Zipla   : Space veya Yukari ok, gamepad A
///   Dash    : Sol Shift, gamepad B  (yon tusu basiliyken ileri yuvarlanir, bos durumda geri adim atar)
///
/// Can (Health) ve stamina (PlayerStamina) ayni objede olmali; yoksa oyun baslarken varsayilan degerlerle eklenir.
/// Ayarlanabilir eklemek icin: Tools > FarToHell > Can ve Stamina Kur
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour, IDamageBlocker
{
    [Header("Hareket")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpForce = 10f;
    [Tooltip("Ziplama tusu erken birakilirsa yukselis hizi bu oranla kesilir (kisa ziplama).")]
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;
    [Tooltip("Platformdan dustukten sonra hala ziplanabilen sure (sn).")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Yere degmeden hemen once basilan ziplamanin hatirlanma suresi (sn).")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("Gorsel")]
    [Tooltip("Sprite'lar saga bakacak sekilde cizildiyse isaretle. Iskelet sprite'lari sola bakiyor, o yuzden kapali.")]
    [SerializeField] private bool spriteFacesRight = false;

    [Header("Zemin Kontrolu")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Bu sureden kisa yer kesilmeleri animasyonda havada sayilmaz (yururken titremeyi onler).")]
    [SerializeField] private float animGroundedGrace = 0.12f;

    [Header("Dash / Backstep")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.5f;
    [SerializeField] private float backstepSpeed = 6f;
    [SerializeField] private float backstepDuration = 0.5f;
    [SerializeField] private float dashCooldown = 0.35f;
    [Tooltip("Havadayken dash atilabilsin mi?")]
    [SerializeField] private bool allowAirDash = false;
    [Tooltip("Dash sirasinda hasar alinmaz (soulslike i-frame). Hasar sistemi IsInvulnerable'a bakabilir.")]
    [SerializeField] private bool dashInvulnerable = true;
    [Tooltip("Ileri dash'in stamina maliyeti")]
    [SerializeField, Min(0f)] private float dashStaminaCost = 20f;
    [Tooltip("Geri adimin stamina maliyeti")]
    [SerializeField, Min(0f)] private float backstepStaminaCost = 12f;

    [Header("Hasar alinca")]
    [Tooltip("Vurulunca kontrolun kilitli kaldigi sure (sn)")]
    [SerializeField, Min(0f)] private float hurtStunDuration = 0.3f;
    [Tooltip("Geri itilirken yukari dogru eklenen hiz")]
    [SerializeField, Min(0f)] private float knockbackUpward = 4f;

    [Header("Olum")]
    [Tooltip("Olunce bu kadar sn sonra sahne bastan baslar (kontrol noktasi gelene kadar gecici)")]
    [SerializeField, Min(0f)] private float deathRestartDelay = 2.5f;
    [SerializeField] private bool restartSceneOnDeath = true;

    [Header("Input")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction jumpAction;
    [SerializeField] private InputAction dashAction = new InputAction("Dash", InputActionType.Button);

    // Animator
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int DashStateHash = Animator.StringToHash("DashForward");
    private static readonly int BackstepStateHash = Animator.StringToHash("Dash");

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private float horizontal;
    private bool isGrounded;
    private float lastGroundedTime = -10f;
    private float lastJumpPressedTime = -10f;

    private bool isDashing;
    private float dashEndTime;
    private float nextDashTime;
    private float dashDirection;
    private float currentDashSpeed;

    /// <summary>1 = saga, -1 = sola bakiyor.</summary>
    public int FacingDirection { get; private set; } = 1;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool IsInvulnerable => isDashing && dashInvulnerable;
    /// <summary>Saldiri gibi durumlarda yurumeyi/ziplamayi/dash'i kilitler (PlayerCombat kullanir).</summary>
    public bool MovementLocked { get; set; }

    private Health health;
    private PlayerStamina stamina;
    private bool isHurt;
    private float hurtEndTime;

    public bool IsDead => health != null && health.IsDead;
    public bool IsHurt => isHurt;
    /// <summary>Saldiri, sersemleme ya da olum yuzunden kontrol kilitli mi?</summary>
    public bool ControlsLocked => MovementLocked || isHurt || IsDead;
    /// <summary>Health bunu sorar: dash sirasinda hasar alinmaz.</summary>
    public bool BlocksDamage => IsInvulnerable;
    public PlayerStamina Stamina => stamina;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        stamina = GetComponent<PlayerStamina>();
        if (stamina == null) stamina = gameObject.AddComponent<PlayerStamina>();
        // Fizik 50 Hz'de, ekran 60+ Hz'de calisir. Interpolation olmadan karakter kareler arasinda
        // ziplayarak ilerler ve kamera takip ederken titrer/bulanik gorunur.
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if (groundCheck == null)
            Debug.LogWarning($"{name}: PlayerMovement'ta Ground Check atanmamis, ziplama calismaz.", this);

        SetupDefaultBindings();

        // Baslangic yonu sprite'in cizildigi yon olsun
        FacingDirection = spriteFacesRight ? 1 : -1;
        ApplyFacing();
    }

    private void SetupDefaultBindings()
    {
        if (moveAction == null) moveAction = new InputAction("Move", InputActionType.Value);
        if (jumpAction == null) jumpAction = new InputAction("Jump", InputActionType.Button);
        if (dashAction == null) dashAction = new InputAction("Dash", InputActionType.Button);

        // Inspector'da girilmis tuslar korunur; eksik olanlar eklenir.
        if (!HasBinding(moveAction, "<Keyboard>/a"))
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        if (!HasBinding(moveAction, "<Keyboard>/leftArrow"))
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
        if (!HasBinding(moveAction, "<Gamepad>/leftStick"))
            moveAction.AddBinding("<Gamepad>/leftStick");

        if (!HasBinding(jumpAction, "<Keyboard>/space")) jumpAction.AddBinding("<Keyboard>/space");
        if (!HasBinding(jumpAction, "<Keyboard>/upArrow")) jumpAction.AddBinding("<Keyboard>/upArrow");
        if (!HasBinding(jumpAction, "<Gamepad>/buttonSouth")) jumpAction.AddBinding("<Gamepad>/buttonSouth");

        if (!HasBinding(dashAction, "<Keyboard>/leftShift")) dashAction.AddBinding("<Keyboard>/leftShift");
        if (!HasBinding(dashAction, "<Gamepad>/buttonEast")) dashAction.AddBinding("<Gamepad>/buttonEast");
    }

    private static bool HasBinding(InputAction action, string path)
    {
        foreach (var b in action.bindings)
            if (string.Equals(b.path, path, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void OnDamaged(DamageInfo info)
    {
        if (health.IsDead) return;
        isDashing = false;
        isHurt = true;
        hurtEndTime = Time.time + hurtStunDuration;

        if (info.knockback > 0f)
        {
            float dx = transform.position.x - info.sourcePosition.x;
            float dir = Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : -FacingDirection;
            rb.linearVelocity = new Vector2(dir * info.knockback, knockbackUpward);
        }
    }

    private void OnDied()
    {
        isDashing = false;
        isHurt = false;
        MovementLocked = false;
        horizontal = 0f;
        if (animator != null) animator.speed = 0f; // olum animasyonu gelene kadar son karede donsun
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Gecici olum efekti: karakter kararir ve solar
        float t = 0f;
        Color start = spriteRenderer != null ? spriteRenderer.color : Color.white;
        Color end = new Color(0.25f, 0.05f, 0.05f, 0f);
        float fade = Mathf.Max(0.01f, deathRestartDelay * 0.6f);
        while (t < fade)
        {
            t += Time.deltaTime;
            if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(start, end, t / fade);
            yield return null;
        }
        if (!restartSceneOnDeath) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, deathRestartDelay - fade));
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Update()
    {
        // Duraklatma menusu acikken (ve kapandigi karede) kontrolleri okuma
        if (PauseMenu.BlocksGameplayInput) return;
        if (IsDead) { UpdateAnimator(); return; }

        if (isHurt && Time.time >= hurtEndTime) isHurt = false;

        horizontal = moveAction.ReadValue<Vector2>().x;
        if (Mathf.Abs(horizontal) < 0.1f) horizontal = 0f;

        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded) lastGroundedTime = Time.time;

        if (isDashing && Time.time >= dashEndTime)
            isDashing = false;

        HandleDash();
        HandleJump();
        HandleFacing();
        UpdateAnimator();
    }

    private void HandleJump()
    {
        if (jumpAction.WasPressedThisFrame())
            lastJumpPressedTime = Time.time;

        bool canJump = !isDashing && !ControlsLocked;
        bool buffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool coyote = Time.time - lastGroundedTime <= coyoteTime;

        if (canJump && buffered && coyote)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            lastJumpPressedTime = -10f;
            lastGroundedTime = -10f; // ayni ziplamayi iki kez kullanmasin
        }

        // Tus erken birakilirsa daha alcak zipla
        if (jumpAction.WasReleasedThisFrame() && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
    }

    private void HandleDash()
    {
        if (!dashAction.WasPressedThisFrame()) return;
        if (isDashing || ControlsLocked || Time.time < nextDashTime) return;
        if (!isGrounded && !allowAirDash) return;

        bool forward = horizontal != 0f;
        if (!stamina.TrySpend(forward ? dashStaminaCost : backstepStaminaCost)) return;
        if (forward)
        {
            // Yon tusu basili: o yone yuvarlan
            FacingDirection = horizontal > 0f ? 1 : -1;
            ApplyFacing();
            dashDirection = FacingDirection;
            currentDashSpeed = dashSpeed;
            dashEndTime = Time.time + dashDuration;
            PlayState(DashStateHash);
        }
        else
        {
            // Bos durumda: baktigi yonun tersine geri adim (yuz yine dusmana donuk)
            dashDirection = -FacingDirection;
            currentDashSpeed = backstepSpeed;
            dashEndTime = Time.time + backstepDuration;
            PlayState(BackstepStateHash);
        }

        isDashing = true;
        nextDashTime = dashEndTime + dashCooldown;
    }

    private void HandleFacing()
    {
        if (isDashing || ControlsLocked || horizontal == 0f) return;
        int dir = horizontal > 0f ? 1 : -1;
        if (dir != FacingDirection)
        {
            FacingDirection = dir;
            ApplyFacing();
        }
    }

    private void ApplyFacing()
    {
        if (spriteRenderer == null) return;
        // Sprite sola cizildiyse saga bakarken cevir, saga cizildiyse sola bakarken cevir
        spriteRenderer.flipX = spriteFacesRight ? FacingDirection < 0 : FacingDirection > 0;
    }

    private void FixedUpdate()
    {
        if (IsDead)
            rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 30f * Time.fixedDeltaTime), rb.linearVelocity.y);
        else if (isHurt)
            return; // geri itilme hizi korunur
        else if (isDashing)
            rb.linearVelocity = new Vector2(dashDirection * currentDashSpeed, rb.linearVelocity.y);
        else if (MovementLocked)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(horizontal * moveSpeed, rb.linearVelocity.y);
    }

    private void UpdateAnimator()
    {
        if (!HasAnimator()) return;
        animator.SetFloat(SpeedHash, ControlsLocked || isDashing ? 0f : Mathf.Abs(horizontal));
        // Zemindeki kucuk tumsek/collider eklerinde karakter bir anligina havada sayilabilir.
        // Animator'a bunu yansitirsak yururken Walk ile JumpFall arasinda titrer (bulanik gorunur).
        // Bu yuzden cok kisa yer kesilmelerini animasyon icin yok sayiyoruz.
        bool animGrounded = isGrounded ||
                            (Time.time - lastGroundedTime < animGroundedGrace && rb.linearVelocity.y < 1f);
        animator.SetFloat(VelocityYHash, animGrounded ? 0f : rb.linearVelocity.y);
        animator.SetBool(IsGroundedHash, animGrounded);
    }

    private void PlayState(int stateHash)
    {
        if (HasAnimator() && animator.HasState(0, stateHash))
            animator.Play(stateHash, 0, 0f);
    }

    private bool HasAnimator() => animator != null && animator.runtimeAnimatorController != null;

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
