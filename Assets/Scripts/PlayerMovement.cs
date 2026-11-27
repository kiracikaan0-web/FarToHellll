using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

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
    [Tooltip("Kosarken yurume hizinin kac kati (ileri dash'ten sonra Shift basili tutulunca kosar)")]
    [SerializeField, Min(1f)] private float runSpeedMultiplier = 1.8f;
    [SerializeField] private float jumpForce = 10f;
    [Tooltip("Ziplama tusu erken birakilirsa yukselis hizi bu oranla kesilir (kisa ziplama).")]
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;
    [Tooltip("Platformdan dustukten sonra hala ziplanabilen sure (sn).")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Yere degmeden hemen once basilan ziplamanin hatirlanma suresi (sn).")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Tooltip("Karakterin collider'i surtunmesiz olsun: duvara/tilemap'e carpinca yapisip kalmaz, kayarak devam eder.")]
    [SerializeField] private bool noFriction = true;

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

    [Tooltip("Dash ve geri adim sirasinda dusmanlarin icinden gecilsin mi?")]
    [SerializeField] private bool dashThroughEnemies = true;

    [Header("Kenara Tutunma / Tirmanma")]
    [Tooltip("Havadayken bir duvarin ust kosesine yetisince otomatik tutunup tirmansin mi?")]
    [SerializeField] private bool ledgeClimb = true;
    [Tooltip("Tutunmak icin duvara dogru yon tusuna basili tutmak gereksin mi?")]
    [SerializeField] private bool ledgeNeedsInput = true;
    [Tooltip("Sadece tilemap'teki kare duvar/zemin bloklarina tutun. Platformlar, merdivenler ve sekilli parcalarda tutunma olmaz.")]
    [SerializeField] private bool ledgeOnlyBlockTiles = true;
    [Tooltip("Kare blok olmasa da tutunulabilecek tile'larin adlari (Project'teki tile asset adi).")]
    [SerializeField] private string[] ledgeGrabTileNames = { "platforms_2", "platforms_3", "Platform_Red" };
    [Tooltip("Kenar, ayaktan bu araliktaysa tutunulur (karakter boyunun orani).")]
    [SerializeField, Range(0f, 1.5f)] private float ledgeMinHeight = 0.45f;
    [SerializeField, Range(0f, 1.5f)] private float ledgeMaxHeight = 1.0f;
    [Tooltip("Duvari aramak icin collider'in onune bakilan mesafe")]
    [SerializeField, Min(0f)] private float ledgeReach = 0.35f;
    [Tooltip("Kenarin hemen onunde en az bu kadar (birim) bosluk olmali. Merdiven ve rampalarda tutunmayi engeller.")]
    [SerializeField, Min(0f)] private float ledgeMinDrop = 1.5f;
    [Tooltip("Tirmanma bitince ayaklarin koseden iceri girdigi mesafe (olcek 1 icin, birim). Tirmanma animasyonunun son karesine gore.")]
    [SerializeField] private float climbEndInset = 0.846f;
    [Tooltip("Animator'daki tirmanma state'inin adi")]
    [SerializeField] private string climbStateName = "Climb";
    [Tooltip("Klip bulunamazsa kullanilan tirmanma suresi (sn)")]
    [SerializeField, Min(0.05f)] private float climbFallbackDuration = 0.6f;
    [Tooltip("Tirmanma kareleri (SkeletonClimb_0..6). Doluysa Animator yerine dogrudan bu kareler oynatilir.")]
    [SerializeField] private Sprite[] climbFrames;
    [SerializeField, Min(1f)] private float climbFps = 12f;
    [Tooltip("Tutunma basladiginda Console'a bilgi yazsin (sorun ararken)")]
    [SerializeField] private bool ledgeDebugLog = true;

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
    private bool lastDashForward;
    private bool airDashUsed;      // bu ziplamada havada dash atildi mi
    private bool dashInAir;        // suanki dash havada mi basladi
    private bool isRunning;
    public bool IsRunning => isRunning;

    /// <summary>1 = saga, -1 = sola bakiyor.</summary>
    public int FacingDirection { get; private set; } = 1;
    public bool IsGrounded => isGrounded;
    public bool IsDashing => isDashing;
    public bool IsInvulnerable => isDashing && dashInvulnerable;
    /// <summary>Saldiri gibi durumlarda yurumeyi/ziplamayi/dash'i kilitler (PlayerCombat kullanir).</summary>
    public bool MovementLocked { get; set; }
    /// <summary>Yurume hizi carpani (ornegin sise icerken yavaslama).</summary>
    public float SpeedMultiplier { get; set; } = 1f;
    /// <summary>Yurunebilir ama ziplama/dash/saldiri yapilamaz (ornegin sise icerken).</summary>
    public bool ActionLock { get; set; }

    private Health health;
    private PlayerStamina stamina;
    private bool isHurt;
    private float hurtEndTime;

    // Dash ile dusmandan gecis: dash boyunca carpismasi kapatilan (oyuncu, dusman) collider ciftleri
    private Collider2D[] bodyColliders;
    private readonly List<Collider2D> ignoredEnemyColliders = new List<Collider2D>();

    // Dusmanin kafasindan kaydirma: kisa sure yatay hizi disaridan belirler
    private float pushUntil;
    private float pushVelocityX;

    // Kenara tutunma
    private bool isClimbing;
    private float nextLedgeTime;
    private Coroutine climbRoutine;
    private RigidbodyType2D bodyTypeBeforeClimb;
    public bool IsClimbing => isClimbing;

    public bool IsDead => health != null && health.IsDead;
    public bool IsHurt => isHurt;
    /// <summary>Saldiri, sersemleme, tirmanma ya da olum yuzunden kontrol kilitli mi?</summary>
    public bool ControlsLocked => MovementLocked || isHurt || IsDead || isClimbing || isGrabbed;

    // ---- Dusman tarafindan yakalanma (boss'un kirbaci sarip ceker) ----
    private bool isGrabbed;
    private Vector2 grabVelocity;
    private float grabGravity = 1f;
    /// <summary>Kirbacla sarilmis: kontrol yok, hizi yakalayan verir, yercekimi kapali.</summary>
    public bool IsGrabbed => isGrabbed;

    public void BeginGrab()
    {
        if (isGrabbed || IsDead) return;
        StopClimb(false);
        isDashing = false;
        isRunning = false;
        grabGravity = holdingSlope ? gravityBeforeSlope : rb.gravityScale;
        holdingSlope = false;
        isGrabbed = true;
        rb.gravityScale = 0f;
        grabVelocity = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }

    public void SetGrabVelocity(Vector2 v) { if (isGrabbed) grabVelocity = v; }

    public void EndGrab(Vector2 releaseVelocity)
    {
        if (!isGrabbed) return;
        isGrabbed = false;
        rb.gravityScale = grabGravity;
        rb.linearVelocity = releaseVelocity;
    }
    /// <summary>Health bunu sorar: dash sirasinda hasar alinmaz.</summary>
    public bool BlocksDamage => IsInvulnerable;
    public PlayerStamina Stamina => stamina;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        var cols = new List<Collider2D>();
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) cols.Add(c);
        bodyColliders = cols.ToArray();
        // Surtunme olunca havadayken duvara/tilemap'e dogru basili tutan karakter oraya yapisip kalir.
        // Yatay hiz zaten her karede koddan verildigi icin surtunmesiz malzeme yerde kaymaya yol acmaz.
        if (noFriction)
        {
            var slippery = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
            rb.sharedMaterial = slippery;
            foreach (var c in bodyColliders) c.sharedMaterial = slippery;
        }
        health = GetComponent<Health>();
        if (health == null) health = gameObject.AddComponent<Health>();
        stamina = GetComponent<PlayerStamina>();
        if (stamina == null) stamina = gameObject.AddComponent<PlayerStamina>();
        if (GetComponent<PlayerFlask>() == null) gameObject.AddComponent<PlayerFlask>();
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
        StopClimb(false);
        isRunning = false;
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
        EndGrab(Vector2.zero);
        StopClimb(false);
        isRunning = false;
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
        // Olum ekrani varsa yeniden dogmayi o yonetir (OLDUN yazisi, kontrol noktasi)
        if (!restartSceneOnDeath || DeathScreen.Instance != null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, deathRestartDelay - fade));
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void Update()
    {
        // Duraklatma menusu acikken (ve kapandigi karede) kontrolleri okuma
        if (PauseMenu.BlocksGameplayInput) return;
        if (IsDead) { UpdateAnimator(); return; }

        if (isHurt && Time.time >= hurtEndTime) isHurt = false;
        if (isClimbing) return; // tirmanma bitene kadar kontrol yok, animator'u Climb state'i yonetir

        horizontal = moveAction.ReadValue<Vector2>().x;
        if (Mathf.Abs(horizontal) < 0.1f) horizontal = 0f;

        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (isGrounded) { lastGroundedTime = Time.time; airDashUsed = false; }

        if (isDashing && Time.time >= dashEndTime)
        {
            isDashing = false;
            // ileri dash bittiginde Shift hala basili ve yon tusu basiliysa kosmaya gec
            if (lastDashForward && dashAction.IsPressed() && horizontal != 0f && !ControlsLocked && !ActionLock)
                isRunning = true;
        }
        // Shift ya da yon tusu birakilinca (veya kontrol kilitlenince) kosu biter
        if (isRunning && (!dashAction.IsPressed() || horizontal == 0f || ControlsLocked || ActionLock))
            isRunning = false;
        if (!isDashing && ignoredEnemyColliders.Count > 0)
            RestoreEnemyCollisions();

        HandleDash();
        HandleJump();
        HandleFacing();
        if (TryGrabLedge()) return;
        UpdateAnimator();
    }

    // ---------------- Kenara tutunma ----------------

    /// <summary>Havadayken onumuzdeki duvarin ust kosesi uygun yukseklikteyse tutunup tirmanmayi baslatir.</summary>
    private bool TryGrabLedge()
    {
        if (!ledgeClimb || isGrounded || isDashing || ControlsLocked || ActionLock) return false;
        // yukselirken de tutunur: ziplarken kenar tutunma araligina girdiginde karakter hala yukseliyor olur,
        // beklersek kenarin ustune cikip normal sekilde konar (tirmanma hic oynamaz)
        if (Time.time < nextLedgeTime) return false;
        if (ledgeNeedsInput && (horizontal == 0f || (horizontal > 0f ? 1 : -1) != FacingDirection)) return false;
        if (!TryGetBodyBounds(out Bounds body)) return false;

        int dir = FacingDirection;
        float feet = body.min.y, h = body.size.y, halfW = body.extents.x;
        float lowY = feet + ledgeMinHeight * h, highY = feet + ledgeMaxHeight * h;
        Vector2 front = new Vector2(dir, 0f);
        float reach = halfW + ledgeReach;

        // 1) bas hizasinda onumuz bos olmali (kenarin ustu acik)
        if (Physics2D.Raycast(new Vector2(body.center.x, highY), front, reach, groundLayer).collider != null)
            return LedgeReject("bas hizasinda onu kapali (kenar henuz cok yuksekte)");
        // 2) govdenin hemen onunde, yukaridan asagi bakip tutunma araliginda bir ust yuzey ara.
        //    Boylece altinda duvar olmayan ince, havada duran platformlarin kenari da bulunur.
        float probeX = body.center.x + dir * reach;
        Debug.DrawLine(new Vector3(probeX, highY), new Vector3(probeX, lowY), Color.yellow, 0.1f);
        RaycastHit2D top = Physics2D.Raycast(new Vector2(probeX, highY), Vector2.down, highY - lowY, groundLayer);
        if (top.collider == null || top.collider.isTrigger || top.distance < 0.01f)
            return LedgeReject("onunde tutunma araliginda ust yuzey yok");
        // 3) yuzeyin hemen altindan yana bakip kenarin (kosenin) x'ini bul
        RaycastHit2D wall = Physics2D.Raycast(new Vector2(body.center.x, top.point.y - 0.3f), front, reach + 0.1f, groundLayer);
        if (wall.collider == null || wall.collider.isTrigger)
            return LedgeReject("yuzeyin kenari bulunamadi");
        // rampa / merdiven: kenarin yani dik, ustu duz olmali; egimli yuzeylerde tutunma, karakter ustunde yurusun
        if (top.normal.y < 0.9f || Mathf.Abs(wall.normal.x) < 0.8f)
            return LedgeReject("egimli yuzey (rampa/merdiven)");
        probeX = wall.point.x + dir * 0.08f;
        // sadece tilemap'teki kare bloklarin (Grid collider) ve listedeki tile'larin koselerine tutun
        if (ledgeOnlyBlockTiles && !IsBlockTile(top.collider, new Vector2(probeX, top.point.y - 0.05f)))
            return LedgeReject("tile tutunulabilir degil (" + DescribeTileAt(top.collider, new Vector2(probeX, top.point.y - 0.05f)) + ")");
        // 4) yukarida ayakta duracak kadar bos yer olmali
        float scale = Mathf.Abs(transform.lossyScale.x);
        Vector2 corner = new Vector2(wall.point.x, top.point.y);
        // gercek kenarin hemen onunde bosluk vardir; merdiven/rampada az asagida bir sonraki basamak olur
        Vector2 outside = new Vector2(corner.x - dir * 0.35f, corner.y + 0.05f);
        Debug.DrawLine(outside, outside + Vector2.down * ledgeMinDrop, Color.cyan, 0.5f);
        if (Physics2D.Raycast(outside, Vector2.down, ledgeMinDrop, groundLayer).collider != null)
            return LedgeReject("kenarin onunde bosluk yok (merdiven/rampa)");
        Vector2 standFeet = corner + new Vector2(dir * climbEndInset * scale, 0f);
        Vector2 boxSize = new Vector2(body.size.x * 0.9f, h * 0.95f);
        if (Physics2D.OverlapBox(standFeet + new Vector2(0f, h * 0.5f + 0.05f), boxSize, 0f, groundLayer) != null)
            return LedgeReject("kenarin ustunde ayakta duracak yer yok");

        climbRoutine = StartCoroutine(ClimbRoutine(corner, standFeet));
        return true;
    }

    private IEnumerator ClimbRoutine(Vector2 corner, Vector2 standFeet)
    {
        isClimbing = true;
        isDashing = false;
        bodyTypeBeforeClimb = rb.bodyType;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        // tirmanma sprite'inin pivotu kenarin kosesinde: karakteri koseye koy
        rb.position = corner;
        transform.position = new Vector3(corner.x, corner.y, transform.position.z);

        bool framesOk = climbFrames != null && climbFrames.Length > 0 && spriteRenderer != null;
        if (framesOk)
        {
            // Kareleri dogrudan oynat: Animator bu sure boyunca durdurulur, boylece hicbir gecis araya giremez
            if (ledgeDebugLog)
            {
                int missing = 0; foreach (var f in climbFrames) if (f == null) missing++;
                Debug.Log($"[Tirmanma] basladi (kareler). Kose {corner}, {climbFrames.Length} kare, eksik {missing}, ilk kare '{(climbFrames[0] != null ? climbFrames[0].name : "YOK")}', " +
                          $"renderer '{spriteRenderer.name}' acik {spriteRenderer.enabled}, olcek {transform.lossyScale}", this);
            }
            if (animator != null) animator.enabled = false;
            float frameTime = 1f / climbFps;
            for (int i = 0; i < climbFrames.Length; i++)
            {
                if (climbFrames[i] != null) spriteRenderer.sprite = climbFrames[i];
                yield return new WaitForSeconds(frameTime);
            }
            if (animator != null) animator.enabled = true;
            rb.position = standFeet;
            transform.position = new Vector3(standFeet.x, standFeet.y, transform.position.z);
            StopClimb(true);
            yield break;
        }

        float duration = climbFallbackDuration;
        int stateHash = Animator.StringToHash(climbStateName);
        if (HasAnimator() && animator.HasState(0, stateHash))
        {
            animator.SetFloat(SpeedHash, 0f);
            animator.SetFloat(VelocityYHash, 0f);
            animator.Play(stateHash, 0, 0f);
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip != null && clip.name == "SkeletonClimb") { duration = clip.length; break; }
        }
        else
            Debug.LogWarning($"{name}: Animator'da '{climbStateName}' state'i yok, tirmanma animasyonu oynamaz. Tools > FarToHell > Tirmanma Animasyonunu Ekle.", this);
        if (ledgeDebugLog)
            Debug.Log($"[Tirmanma] basladi. Kose {corner}, controller '{(animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "YOK")}', " +
                      $"Climb state {(HasAnimator() && animator.HasState(0, stateHash) ? "VAR" : "YOK")}, sure {duration:0.00} sn", this);
        yield return new WaitForSeconds(duration);

        // son kare: karakter kenarin ustunde ayakta
        rb.position = standFeet;
        transform.position = new Vector3(standFeet.x, standFeet.y, transform.position.z);
        StopClimb(true);
    }

    private void StopClimb(bool finished)
    {
        if (!isClimbing) return;
        if (climbRoutine != null) { StopCoroutine(climbRoutine); climbRoutine = null; }
        isClimbing = false;
        if (animator != null && !animator.enabled && !IsDead) animator.enabled = true;
        rb.bodyType = bodyTypeBeforeClimb;
        rb.linearVelocity = Vector2.zero;
        nextLedgeTime = Time.time + 0.25f;
        if (finished)
        {
            isGrounded = true;
            lastGroundedTime = Time.time;
            PlayState(Animator.StringToHash("Idle"));
        }
    }

    /// <summary>
    /// Noktadaki tile tutunulabilir mi: tilemap'te kare (Grid) collider'li bir blok ya da
    /// adi ledgeGrabTileNames listesinde olan bir tile (ornegin platforms_2, platforms_3).
    /// </summary>
    private bool IsBlockTile(Collider2D col, Vector2 point)
    {
        var tilemap = col.GetComponentInParent<Tilemap>();
        if (tilemap == null) return false;                  // tilemap disindaki nesneler (platform prefablari vb.)
        Vector3Int cell = tilemap.WorldToCell(point);
        var tile = tilemap.GetTile(cell);
        if (tile != null)
        {
            if (IsExtraGrabTile(tile)) return true;
            var t = tile as Tile;
            if (t == null || t.colliderType == Tile.ColliderType.Grid) return true;
        }
        // platform sprite'lari hucreden buyuk: nokta komsu hucreye tasmis olabilir, cevredeki listedeki tile'lara bak
        if (ledgeGrabTileNames == null || ledgeGrabTileNames.Length == 0) return false;
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -3; dy <= 3; dy++)
            {
                var c = cell + new Vector3Int(dx, dy, 0);
                var other = tilemap.GetTile(c);
                // kenarin cevresinde (birkac hucre icinde) listedeki bir platform varsa bu kenar ona ait sayilir
                if (other != null && IsExtraGrabTile(other)) return true;
            }
        return false;
    }

    private float nextLedgeLogTime;
    /// <summary>Tutunma neden olmadi: ledgeDebugLog aciksa saniyede birkac kez Console'a yazar.</summary>
    private bool LedgeReject(string why)
    {
        if (ledgeDebugLog && Time.time >= nextLedgeLogTime)
        {
            nextLedgeLogTime = Time.time + 0.3f;
            Debug.Log("[Tirmanma] olmadi: " + why, this);
        }
        return false;
    }

    private string DescribeTileAt(Collider2D col, Vector2 point)
    {
        var tilemap = col.GetComponentInParent<Tilemap>();
        if (tilemap == null) return "tilemap degil: " + col.name;
        Vector3Int cell = tilemap.WorldToCell(point);
        var tile = tilemap.GetTile(cell);
        var near = new System.Text.StringBuilder();
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -3; dy <= 3; dy++)
            {
                var t = tilemap.GetTile(cell + new Vector3Int(dx, dy, 0));
                if (t != null && near.ToString().IndexOf(t.name) < 0) near.Append(t.name).Append(' ');
            }
        return (tile == null ? "hucre bos" : "tile '" + tile.name + "'") + ", cevrede: " + near;
    }

    private bool IsExtraGrabTile(TileBase tile)
    {
        if (ledgeGrabTileNames == null) return false;
        foreach (var n in ledgeGrabTileNames)
            if (!string.IsNullOrEmpty(n) && tile.name == n) return true;
        return false;
    }

    private bool TryGetBodyBounds(out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var c in bodyColliders)
        {
            if (c == null || !c.enabled) continue;
            if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
        }
        return any;
    }

    private void HandleJump()
    {
        if (jumpAction.WasPressedThisFrame())
            lastJumpPressedTime = Time.time;

        bool canJump = !isDashing && !ControlsLocked && !ActionLock;
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
        if (isDashing || ControlsLocked || ActionLock || Time.time < nextDashTime) return;
        if (!isGrounded && !allowAirDash) return;
        if (!isGrounded && airDashUsed) return;   // havada tek dash: yere degince yenilenir
        dashInAir = !isGrounded;
        if (dashInAir) airDashUsed = true;

        bool forward = horizontal != 0f;
        if (!stamina.TrySpend(forward ? dashStaminaCost : backstepStaminaCost)) return;
        lastDashForward = forward;
        isRunning = false;
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
        if (dashThroughEnemies) IgnoreEnemyCollisions();
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
        if (isClimbing)
            return; // tirmanirken fizik durdurulur
        if (isGrabbed) { rb.linearVelocity = grabVelocity; return; }   // kirbacla cekiliyor
        HoldOnSlope();
        if (IsDead)
            rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 30f * Time.fixedDeltaTime), rb.linearVelocity.y);
        else if (isHurt)
            return; // geri itilme hizi korunur
        else if (Time.time < pushUntil)
            rb.linearVelocity = new Vector2(pushVelocityX, Mathf.Min(rb.linearVelocity.y, 0f)); // dusmanin kafasindan kay
        else if (isDashing)
            rb.linearVelocity = new Vector2(dashDirection * currentDashSpeed, dashInAir ? 0f : rb.linearVelocity.y);   // havada dash duz gider, dusmez
        else if (MovementLocked)
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        else
            rb.linearVelocity = new Vector2(horizontal * moveSpeed * SpeedMultiplier * (isRunning ? runSpeedMultiplier : 1f), rb.linearVelocity.y);
    }

    /// <summary>
    /// Surtunmesiz karakter rampada dururken asagi kayar. Yerde, yon tusu basili degilken ve egimli bir
    /// yuzeyin ustundeyken yercekimini gecici kapatip karakteri yerinde tutar.
    /// </summary>
    private void HoldOnSlope()
    {
        bool hold = false;
        if (isGrounded && horizontal == 0f && !isDashing && !isHurt && !IsDead && rb.linearVelocity.y <= 0.5f
            && TryGetBodyBounds(out Bounds body))
        {
            RaycastHit2D g = Physics2D.Raycast(new Vector2(body.center.x, body.min.y + 0.1f), Vector2.down, 0.6f, groundLayer);
            hold = g.collider != null && g.normal.y < 0.99f && g.normal.y > 0.4f;
        }
        if (hold)
        {
            if (!holdingSlope) { gravityBeforeSlope = rb.gravityScale; holdingSlope = true; }
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
        }
        else if (holdingSlope)
        {
            rb.gravityScale = gravityBeforeSlope;
            holdingSlope = false;
        }
    }
    private bool holdingSlope;
    private float gravityBeforeSlope = 1f;

    /// <summary>Dusmanin kafasina basinca EnemyBody cagirir: oyuncu yana dogru kayip duser.</summary>
    public void PushOffEnemyHead(float direction, float speed)
    {
        if (IsDead || isDashing) return;
        pushVelocityX = Mathf.Sign(direction == 0f ? -FacingDirection : direction) * speed;
        pushUntil = Time.time + 0.12f;
    }

    private void IgnoreEnemyCollisions()
    {
        foreach (var enemy in EnemyBody.All)
        {
            if (enemy == null) continue;
            foreach (var ec in enemy.Colliders)
            {
                if (ec == null || ignoredEnemyColliders.Contains(ec)) continue;
                foreach (var mine in bodyColliders) Physics2D.IgnoreCollision(mine, ec, true);
                ignoredEnemyColliders.Add(ec);
            }
        }
    }

    /// <summary>Dash bitince: icinden cikilan dusmanlarla carpismayi geri ac (hala icindeyse cikana kadar bekle).</summary>
    private void RestoreEnemyCollisions()
    {
        for (int i = ignoredEnemyColliders.Count - 1; i >= 0; i--)
        {
            var ec = ignoredEnemyColliders[i];
            if (ec == null) { ignoredEnemyColliders.RemoveAt(i); continue; }
            bool overlapping = false;
            if (ec.enabled && ec.gameObject.activeInHierarchy)
                foreach (var mine in bodyColliders)
                    if (mine.enabled && mine.Distance(ec).isOverlapped) { overlapping = true; break; }
            if (overlapping) continue;
            foreach (var mine in bodyColliders) Physics2D.IgnoreCollision(mine, ec, false);
            ignoredEnemyColliders.RemoveAt(i);
        }
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

        // Kosu: Run state'i kodla acilip kapatilir (Walk/Idle uzerinden)
        if (animator.HasState(0, RunStateHash))
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            bool wantRun = isRunning && animGrounded && horizontal != 0f && !isDashing && !ControlsLocked;
            bool inRun = st.shortNameHash == RunStateHash;
            if (wantRun && !inRun && (st.shortNameHash == WalkStateHash || st.shortNameHash == IdleStateHash))
                animator.Play(RunStateHash, 0, 0f);
            else if (!wantRun && inRun)
            {
                int next = !animGrounded ? (rb.linearVelocity.y > 0.1f ? JumpRiseStateHash : JumpFallStateHash)
                         : (horizontal != 0f ? WalkStateHash : IdleStateHash);
                animator.Play(next, 0, 0f);
            }
        }
    }
    private static readonly int RunStateHash = Animator.StringToHash("Run");
    private static readonly int WalkStateHash = Animator.StringToHash("Walk");
    private static readonly int IdleStateHash = Animator.StringToHash("Idle");
    private static readonly int JumpRiseStateHash = Animator.StringToHash("JumpRise");
    private static readonly int JumpFallStateHash = Animator.StringToHash("JumpFall");

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
