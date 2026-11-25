using System.Collections;
using UnityEngine;

/// <summary>
/// Skeleton2 dusmani: devriye gezer, oyuncuyu fark edince kovalar, yakindaysa pencesiyle vurur,
/// fark ettigi surece belli araliklarla (varsayilan 4 sn) duz giden kemik firlatir. Vurulunca sersemler, olunce solup kaybolur.
///
/// Yon: sprite'lar sola bakacak sekilde cizildi. Dusman donerken tum obje (localScale.x) cevrilir;
/// boylece collider ve saldiri noktalari da kendiliginden doner. Asagidaki "ileri" mesafeleri
/// dusmanin baktigi yone gore (pozitif = ileri) yazilir.
///
/// Yapi: kok obje (Rigidbody2D, Collider, Health, bu script) + "Visual" alt objesi (SpriteRenderer, Animator).
///
/// Sahnede secince: sari = gorme mesafesi, kirmizi kutu = pence alani, mavi = kemigin ciktigi nokta,
/// yesil cizgi = devriye alani.
/// Kurulum: Tools > FarToHell > Dusman Ekle: Skeleton2
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class SkeletonEnemy : MonoBehaviour
{
    private enum State { Patrol, Chase, Attack, Throw, Stagger, Dead }

    [Header("Hareket")]
    [SerializeField, Min(0f)] private float patrolSpeed = 1.2f;
    [SerializeField, Min(0f)] private float chaseSpeed = 2.4f;
    [Tooltip("Baslangic noktasinin iki yanina kac birim devriye gezsin (0 = yerinde beklesin)")]
    [SerializeField, Min(0f)] private float patrolDistance = 3f;
    [Tooltip("Devriye ucunda bekleme (sn)")]
    [SerializeField, Min(0f)] private float patrolWait = 1.2f;
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [Tooltip("Ilerideki zemin kontrolu: ondeki bosluga/ucuruma yurumesin")]
    [SerializeField, Min(0f)] private float ledgeCheckForward = 1.2f;

    [Header("Algilama")]
    [Tooltip("Onundeki oyuncuyu bu mesafeden fark eder")]
    [SerializeField, Min(0f)] private float sightRange = 9f;
    [Tooltip("Arkasindaki oyuncuyu ancak bu kadar yakinsa fark eder")]
    [SerializeField, Min(0f)] private float backSightRange = 2.5f;
    [Tooltip("Oyuncu bundan yuksekte/alcakta ise gormez")]
    [SerializeField, Min(0f)] private float verticalSight = 3.5f;
    [Tooltip("Oyuncu bu mesafeden uzaklasirsa...")]
    [SerializeField, Min(0f)] private float loseRange = 14f;
    [Tooltip("...bu kadar sn sonra vazgecip devriyeye doner")]
    [SerializeField, Min(0f)] private float loseTime = 2.5f;

    [Header("Pence saldirisi")]
    [SerializeField, Min(0)] private int clawDamage = 20;
    [SerializeField, Min(0f)] private float clawKnockback = 9f;
    [Tooltip("Oyuncu bu mesafeye girince pence atar")]
    [SerializeField, Min(0f)] private float clawRange = 3.4f;
    [Tooltip("Pence vurus alani, ileri yonde (baslangic, bitis). Govdenin dibinden elin ulastigi yere kadar.")]
    [SerializeField] private Vector2 clawReach = new Vector2(0f, 4.1f);
    [Tooltip("Pence vurus alani, yukari yonde (alt, ust)")]
    [SerializeField] private Vector2 clawHeight = new Vector2(0.1f, 3.4f);
    [SerializeField, Min(0f)] private float clawCooldown = 1.4f;
    [Tooltip("Animasyon hizi. Dusuk = kol daha yavas kalkar, oyuncu tepki vermek icin daha cok zaman bulur.")]
    [SerializeField, Range(0.3f, 2f)] private float clawAnimSpeed = 0.75f;
    [Tooltip("Saldiridan sonra savunmasiz kalma (sn) - oyuncunun karsilik verme firsati")]
    [SerializeField, Min(0f)] private float clawRecovery = 0.45f;

    [Header("Kemik firlatma")]
    [SerializeField] private EnemyProjectile bonePrefab;
    [SerializeField, Min(0)] private int boneDamage = 12;
    [SerializeField, Min(0f)] private float boneSpeed = 9f;
    [Tooltip("Oyuncuyu fark ettigi surece kac sn'de bir kemik firlatsin")]
    [SerializeField, Min(0.5f)] private float throwInterval = 4f;
    [Tooltip("Oyuncuyu ilk fark ettikten kac sn sonra ilk kemigi atsin")]
    [SerializeField, Min(0f)] private float throwFirstDelay = 1.5f;
    [Tooltip("Oyuncu bundan uzaktaysa atmaz (gormedigi yere atmasin)")]
    [SerializeField, Min(0f)] private float throwMaxRange = 14f;
    [Tooltip("Kemigin ciktigi nokta (x: ileri, y: yukari). Animasyonda kemigin elden ciktigi yer. Oyuncu daha yakindaysa kemik onun hemen onunden cikar.")]
    [SerializeField] private Vector2 throwOffset = new Vector2(5.8f, 1.75f);
    [SerializeField, Range(0.3f, 2f)] private float throwAnimSpeed = 0.85f;
    [SerializeField, Min(0f)] private float throwRecovery = 0.4f;

    [Header("Vurulunca")]
    [Tooltip("Vurulunca sersemleme suresi (sn); saldirisi yarida kesilir")]
    [SerializeField, Min(0f)] private float staggerDuration = 0.35f;
    [Tooltip("Sersemledikten sonra bu kadar sn tekrar sersemlemez (sonsuz kilitlemeyi onler)")]
    [SerializeField, Min(0f)] private float staggerImmunity = 1.0f;
    [Tooltip("Geri itilme carpani (oyuncunun saldiri itmesiyle carpilir)")]
    [SerializeField, Min(0f)] private float knockbackMultiplier = 1f;

    [Header("Olum")]
    [SerializeField, Min(0f)] private float deathFadeTime = 1.2f;

    [Header("Yurume (yuruyus animasyonu gelene kadar)")]
    [Tooltip("Yurume animasyonu yokken hafif yukari asagi sallanma")]
    [SerializeField, Min(0f)] private float walkBobAmount = 0.08f;
    [SerializeField, Min(0f)] private float walkBobSpeed = 7f;

    // Animasyon kare zamanlari (16 fps, 8 kare). Animator hizi ile bolunur.
    private const float ClawHitTime = 5f / 16f;
    private const float ThrowReleaseTime = 7f / 16f;
    private const float ClipLength = 8f / 16f;

    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int ThrowHash = Animator.StringToHash("Throw");

    private Rigidbody2D rb;
    private Health health;
    private Animator animator;
    private SpriteRenderer sr;
    private Transform player;
    private Health playerHealth;

    private State state = State.Patrol;
    private int facing = -1; // -1 sol (sprite'in cizildigi yon), 1 sag
    private float startX;
    private int patrolDir = -1;
    private float patrolWaitUntil;
    private float lastSeenTime = -100f;
    private float nextClawTime, nextThrowTime;
    private float actionStart, actionHitTime, actionEnd;
    private bool actionDone;
    private float staggerEnd, staggerImmuneUntil;
    private float moveX;
    private Transform visual; // SpriteRenderer + Animator olan alt obje

    public bool IsAggro => state == State.Chase || state == State.Attack || state == State.Throw;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        health = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        visual = sr != null ? sr.transform : null;
        startX = transform.position.x;
        facing = transform.localScale.x >= 0f ? -1 : 1;
        // Kafada durulmasin + dash ile icinden gecilebilsin (eski prefablarda yoksa ekle)
        if (GetComponent<EnemyBody>() == null) gameObject.AddComponent<EnemyBody>();
    }

    private void OnEnable()
    {
        health.Damaged += OnDamaged;
        health.Died += OnDied;
    }

    private void OnDisable()
    {
        health.Damaged -= OnDamaged;
        health.Died -= OnDied;
    }

    private void Start() => FindPlayer();

    private void FindPlayer()
    {
        var p = FindAnyObjectByType<PlayerMovement>();
        if (p == null) return;
        player = p.transform;
        playerHealth = p.GetComponent<Health>();
    }

    // ---------------- Karar ----------------

    private void Update()
    {
        if (state == State.Dead) return;
        if (player == null) FindPlayer();

        bool playerAlive = player != null && (playerHealth == null || !playerHealth.IsDead);
        moveX = 0f;

        switch (state)
        {
            case State.Patrol: UpdatePatrol(playerAlive); break;
            case State.Chase: UpdateChase(playerAlive); break;
            case State.Attack: UpdateAction(true); break;
            case State.Throw: UpdateAction(false); break;
            case State.Stagger:
                if (Time.time >= staggerEnd) state = playerAlive ? State.Chase : State.Patrol;
                break;
        }

        UpdateWalkBob();
    }

    private void UpdatePatrol(bool playerAlive)
    {
        if (playerAlive && CanSeePlayer())
        {
            BeginAggro();
            return;
        }

        if (patrolDistance <= 0f || Time.time < patrolWaitUntil) return;

        float target = startX + patrolDir * patrolDistance;
        if ((patrolDir < 0 && transform.position.x <= target) || (patrolDir > 0 && transform.position.x >= target) ||
            !GroundAhead(patrolDir))
        {
            patrolDir = -patrolDir;
            patrolWaitUntil = Time.time + patrolWait;
            return;
        }
        Face(patrolDir);
        moveX = patrolDir * patrolSpeed;
    }

    private void UpdateChase(bool playerAlive)
    {
        if (!playerAlive) { state = State.Patrol; return; }

        Vector2 toPlayer = player.position - transform.position;
        float dist = Mathf.Abs(toPlayer.x);
        if (CanSeePlayer()) lastSeenTime = Time.time;
        if (dist > loseRange || Time.time - lastSeenTime > loseTime)
        {
            state = State.Patrol;
            startX = transform.position.x; // kaldigi yerde devriyeye baslasin
            return;
        }

        int dir = toPlayer.x >= 0f ? 1 : -1;
        Face(dir);

        bool sameHeight = Mathf.Abs(toPlayer.y) < verticalSight;
        bool inClawRange = sameHeight && dist <= clawRange;

        // Yakindaysa pence (pence hazirsa)
        if (inClawRange && Time.time >= nextClawTime)
        {
            StartAction(State.Attack);
            return;
        }
        // Kemik: menzilden bagimsiz, sureyle. Oyuncu pence menzilindeyse bekler (pence oncelikli).
        if (!inClawRange && sameHeight && Time.time >= nextThrowTime && bonePrefab != null &&
            dist <= throwMaxRange && HasLineOfSight())
        {
            StartAction(State.Throw);
            return;
        }

        // Pence menzilinin biraz icine kadar yaklas
        if (dist > clawRange * 0.8f && GroundAhead(dir))
            moveX = dir * chaseSpeed;
    }

    private void StartAction(State action)
    {
        bool claw = action == State.Attack;
        float speed = claw ? clawAnimSpeed : throwAnimSpeed;
        state = action;
        actionDone = false;
        actionStart = Time.time;
        actionHitTime = actionStart + (claw ? ClawHitTime : ThrowReleaseTime) / speed;
        actionEnd = actionStart + ClipLength / speed + (claw ? clawRecovery : throwRecovery);
        if (claw) nextClawTime = actionEnd + clawCooldown;
        else nextThrowTime = actionStart + throwInterval; // atis baslangicindan itibaren say

        if (animator != null)
        {
            animator.speed = speed;
            PlayState(claw ? AttackHash : ThrowHash);
        }
    }

    private void UpdateAction(bool claw)
    {
        if (!actionDone && Time.time >= actionHitTime)
        {
            actionDone = true;
            if (claw) DoClaw();
            else DoThrow();
        }
        if (Time.time >= actionEnd)
        {
            if (animator != null) animator.speed = 1f;
            state = State.Chase;
        }
    }

    private void DoClaw()
    {
        GetClawBox(out Vector2 center, out Vector2 size);
        var hits = Physics2D.OverlapBoxAll(center, size, 0f);
        foreach (var col in hits)
        {
            var target = col.GetComponentInParent<PlayerMovement>();
            if (target == null) continue;
            var h = target.GetComponent<Health>();
            if (h != null) h.TakeDamage(new DamageInfo(clawDamage, transform.position, clawKnockback));
            break; // oyuncuya bir kez vur
        }
    }

    private void DoThrow()
    {
        if (bonePrefab == null) return;
        // Oyuncu kemigin normal cikis noktasindan yakindaysa kemik onun hemen onunden ciksin (arkasindan degil)
        Vector2 offset = throwOffset;
        if (player != null)
        {
            float dist = Mathf.Abs(player.position.x - transform.position.x);
            offset.x = Mathf.Clamp(dist - 1.2f, 1f, throwOffset.x);
        }
        var bone = Instantiate(bonePrefab, WorldPoint(offset), Quaternion.identity);
        bone.Launch(new Vector2(facing, 0f), boneSpeed, boneDamage, gameObject);
    }

    // ---------------- Fizik ----------------

    private void FixedUpdate()
    {
        if (state == State.Dead) return;
        if (state == State.Stagger) return; // geri itilme hizi korunur
        rb.linearVelocity = new Vector2(moveX, rb.linearVelocity.y);
    }

    // ---------------- Hasar ----------------

    private void OnDamaged(DamageInfo info)
    {
        if (state == State.Dead) return;

        // Arkadan vurulduysa da oyuncuyu fark etsin
        if (state == State.Patrol) BeginAggro();
        lastSeenTime = Time.time;

        // Vurus ani gectiyse saldiri kesilmez (dusman hamlesine baglanmis durumda)
        bool committed = (state == State.Attack || state == State.Throw) && actionDone;
        if (committed || Time.time < staggerImmuneUntil) return;

        state = State.Stagger;
        staggerEnd = Time.time + staggerDuration;
        staggerImmuneUntil = staggerEnd + staggerImmunity;
        if (animator != null)
        {
            animator.speed = 1f;
            PlayState(IdleHash);
        }

        float dx = transform.position.x - info.sourcePosition.x;
        float dir = Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : -facing;
        rb.linearVelocity = new Vector2(dir * info.knockback * knockbackMultiplier, rb.linearVelocity.y + 1.5f);
        Face(-(int)dir); // vurana don
    }

    private void OnDied()
    {
        state = State.Dead;
        if (animator != null) animator.speed = 0f;
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        float t = 0f;
        Color start = sr != null ? sr.color : Color.white;
        Color end = new Color(0.3f, 0.05f, 0.05f, 0f);
        while (t < deathFadeTime)
        {
            t += Time.deltaTime;
            if (sr != null) sr.color = Color.Lerp(start, end, t / deathFadeTime);
            transform.position += Vector3.down * (0.4f * Time.deltaTime); // hafifce yere cokme
            yield return null;
        }
        Destroy(gameObject);
    }

    // ---------------- Yardimcilar ----------------

    private void BeginAggro()
    {
        state = State.Chase;
        lastSeenTime = Time.time;
        nextThrowTime = Mathf.Max(nextThrowTime, Time.time + throwFirstDelay);
    }

    private void GetClawBox(out Vector2 center, out Vector2 size)
    {
        float near = Mathf.Min(clawReach.x, clawReach.y), far = Mathf.Max(clawReach.x, clawReach.y);
        float bottom = Mathf.Min(clawHeight.x, clawHeight.y), top = Mathf.Max(clawHeight.x, clawHeight.y);
        center = WorldPoint(new Vector2((near + far) * 0.5f, (bottom + top) * 0.5f));
        size = new Vector2(far - near, top - bottom);
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 to = player.position - transform.position;
        if (Mathf.Abs(to.y) > verticalSight) return false;
        bool inFront = Mathf.Sign(to.x) == facing;
        float range = inFront ? sightRange : backSightRange;
        if (Mathf.Abs(to.x) > range) return false;
        return HasLineOfSight();
    }

    private bool HasLineOfSight()
    {
        if (player == null) return false;
        Vector2 eye = (Vector2)transform.position + Vector2.up * 2.2f;
        Vector2 target = (Vector2)player.position + Vector2.up * 1.5f;
        return !Physics2D.Linecast(eye, target, groundLayer);
    }

    private bool GroundAhead(int dir)
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(dir * ledgeCheckForward, 0.5f);
        return Physics2D.Raycast(origin, Vector2.down, 1.5f, groundLayer);
    }

    private void Face(int dir)
    {
        if (dir == 0 || dir == facing) return;
        facing = dir;
        var s = transform.localScale;
        // Sprite sola cizildi: sola bakarken +, saga bakarken - olcek
        s.x = Mathf.Abs(s.x) * (facing < 0 ? 1f : -1f);
        transform.localScale = s;
    }

    /// <summary>"ileri/yukari" ofsetini dunya konumuna cevirir.</summary>
    private Vector2 WorldPoint(Vector2 forwardUp)
    {
        int f = Application.isPlaying ? facing : (transform.localScale.x >= 0f ? -1 : 1);
        return (Vector2)transform.position + new Vector2(forwardUp.x * f, forwardUp.y);
    }

    private void PlayState(int hash)
    {
        if (animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, hash))
            animator.Play(hash, 0, 0f);
    }

    private void UpdateWalkBob()
    {
        // Yurume animasyonu yok: yururken gorseli hafifce zipla/sallanarak hareket hissi ver
        if (visual == null || visual == transform) return;
        bool walking = Mathf.Abs(moveX) > 0.01f;
        float s = Mathf.Sin(Time.time * walkBobSpeed);
        visual.localPosition = new Vector3(0f, walking ? Mathf.Abs(s) * walkBobAmount : 0f, 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, walking ? s * 2.5f : 0f);
    }

    private void OnDrawGizmosSelected()
    {
        float sx = Application.isPlaying ? startX : transform.position.x;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, sightRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        GetClawBox(out Vector2 cc, out Vector2 cs);
        Gizmos.DrawWireCube(cc, cs);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.9f);
        Gizmos.DrawWireSphere(WorldPoint(throwOffset), 0.3f);
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.8f);
        Vector3 a = new Vector3(sx - patrolDistance, transform.position.y + 0.1f);
        Vector3 b = new Vector3(sx + patrolDistance, transform.position.y + 0.1f);
        Gizmos.DrawLine(a, b);
    }
}
