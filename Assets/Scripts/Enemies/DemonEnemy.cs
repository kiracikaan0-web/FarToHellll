using System.Collections;
using UnityEngine;

/// <summary>
/// Seytan kiz dusmani: devriye gezer, oyuncuyu fark edince kovalar, kirbac menziline girince
/// kirbaci kafasinin ustunden savurup onune sasklatir. Kirbac ucu uzun menzillidir ama vurus ani gec gelir:
/// oyuncu kirbac kalkarken geri cekilerek ya da dash atarak kacabilir.
///
/// Sprite'lar sola bakacak sekilde cizildi; donerken tum obje (localScale.x) cevrilir.
/// Yapi: kok obje (Rigidbody2D, Collider, Health, bu script) + "Visual" alt objesi (SpriteRenderer, Animator).
/// Sahnede secince: sari = gorme mesafesi, kirmizi kutu = kirbac vurus alani, yesil = devriye alani.
/// Kurulum: Tools > FarToHell > Dusman Ekle: Seytan Kiz
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Health))]
public class DemonEnemy : MonoBehaviour
{
    private enum State { Patrol, Chase, Lunge, Whip, Slam, Stagger, Dead }

    [Header("Hareket")]
    [SerializeField, Min(0f)] private float patrolSpeed = 1.6f;
    [SerializeField, Min(0f)] private float chaseSpeed = 3.2f;
    [SerializeField, Min(0f)] private float patrolDistance = 3f;
    [SerializeField, Min(0f)] private float patrolWait = 1.4f;
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [SerializeField, Min(0f)] private float ledgeCheckForward = 1.0f;

    [Header("Algilama")]
    [SerializeField, Min(0f)] private float sightRange = 10f;
    [SerializeField, Min(0f)] private float backSightRange = 2.5f;
    [SerializeField, Min(0f)] private float verticalSight = 3.5f;
    [SerializeField, Min(0f)] private float loseRange = 15f;
    [SerializeField, Min(0f)] private float loseTime = 2.5f;

    [Header("Atilma (uzaktaki oyuncunun ustune hizla atilir)")]
    [SerializeField] private bool lungeEnabled = true;
    [SerializeField, Min(0f)] private float lungeSpeed = 10f;
    [SerializeField, Min(0f)] private float lungeDuration = 0.32f;
    [SerializeField, Min(0f)] private float lungeCooldown = 2.2f;
    [Tooltip("Oyuncu bu mesafeler arasindaysa atilir (min, max)")]
    [SerializeField] private Vector2 lungeDistance = new Vector2(5.5f, 11f);
    [Tooltip("Atilirken one egilme acisi")]
    [SerializeField, Range(0f, 25f)] private float lungeLean = 9f;

    [Header("Yer yakma (geri ziplar, kirbaci yere vurur, alan 5 sn yanar)")]
    [SerializeField] private bool slamEnabled = true;
    [SerializeField] private DemonFireZone firePrefab;
    [Tooltip("Oyuncu bu mesafedeyse (dunya birimi) bu saldiriyi kullanir")]
    [SerializeField, Min(0f)] private float slamRange = 6.5f;
    [Tooltip("Iki yer yakma arasi bekleme: her seferinde bu araliktan rastgele secilir (min, max sn)")]
    [SerializeField] private Vector2 slamCooldownRange = new Vector2(4f, 10f);
    [Tooltip("Hazir ve menzildeyken her denemede yer yakmayi secme ihtimali (0-1). Secmezse kirbac/atilma ile devam eder.")]
    [SerializeField, Range(0f, 1f)] private float slamChance = 0.35f;
    [Tooltip("Kac sn de bir zar atsin")]
    [SerializeField, Min(0.1f)] private float slamRollInterval = 0.6f;
    [Tooltip("Oyuncuyu ilk gordukten kac sn sonra ilk kez kullanabilir")]
    [SerializeField, Min(0f)] private float slamFirstDelay = 3f;
    [Tooltip("Geri zipliyorken yatay hiz")]
    [SerializeField, Min(0f)] private float hopBackSpeed = 7f;
    [Tooltip("Yanan alan, ileri yonde (baslangic, bitis), olcek 1 iken")]
    [SerializeField] private Vector2 fireArea = new Vector2(0.8f, 4.4f);
    [SerializeField, Min(0f)] private float slamRecovery = 0.35f;

    [Header("Kirbac")]
    [Tooltip("Kirbac oyuncunun MAKSIMUM caninin bu oranini goturur (0.25 = %25, 4 vurusta oldurur). 0 yaparsan asagidaki sabit hasar kullanilir.")]
    [SerializeField, Range(0f, 1f)] private float whipDamagePercent = 0.25f;
    [Tooltip("Sabit hasar (yalnizca yukaridaki oran 0 ise)")]
    [SerializeField, Min(0)] private int whipDamage = 18;
    [SerializeField, Min(0f)] private float whipKnockback = 10f;
    [Tooltip("Oyuncu bu mesafeye (olcekten bagimsiz, dunya birimi) girince kirbac savurur")]
    [SerializeField, Min(0f)] private float whipRange = 4.2f;
    [Tooltip("Vurus alani ileri yonde (baslangic, bitis), karakter olcegi 1 iken. Olcekle birlikte buyur.")]
    [SerializeField] private Vector2 whipReach = new Vector2(0.3f, 3.4f);
    [Tooltip("Vurus alani yukari yonde (alt, ust), karakter olcegi 1 iken")]
    [SerializeField] private Vector2 whipHeight = new Vector2(0.3f, 1.8f);
    [SerializeField, Min(0f)] private float whipCooldown = 1.3f;
    [Tooltip("Animasyon hizi. Dusuk = kirbac daha yavas kalkar, oyuncu kacmak icin daha cok zaman bulur.")]
    [SerializeField, Range(0.3f, 2f)] private float whipAnimSpeed = 1f;
    [Tooltip("Saldiridan sonra savunmasiz kalma (sn)")]
    [SerializeField, Min(0f)] private float whipRecovery = 0.25f;

    [Header("Vurulunca")]
    [SerializeField, Min(0f)] private float staggerDuration = 0.35f;
    [SerializeField, Min(0f)] private float staggerImmunity = 1.0f;
    [SerializeField, Min(0f)] private float knockbackMultiplier = 1f;

    [Header("Olum")]
    [SerializeField, Min(0f)] private float deathFadeTime = 1.2f;

    [Header("Bekleme / yurume hissi")]
    [SerializeField, Min(0f)] private float breatheAmount = 0.015f;
    [SerializeField, Min(0f)] private float breatheSpeed = 2.4f;
    [SerializeField, Min(0f)] private float walkBobAmount = 0.07f;
    [SerializeField, Min(0f)] private float walkBobSpeed = 8f;
    [Tooltip("Yurume animasyonunun bir turunda (2 adim) alinan yol, olcek 1 iken. Ayak kaymasi olursa bununla oyna.")]
    [SerializeField, Min(0.1f)] private float walkStrideLength = 1.07f;

    // Klip: 18 kare, 20 fps. Kirbacin saklayip ileri uzandigi kareler 11-13.
    private const float WhipHitStart = 11f / 20f;
    private const float WhipHitEnd = 14f / 20f;
    private const float ClipLength = 18f / 20f;

    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int WhipHash = Animator.StringToHash("Whip");
    private static readonly int WalkHash = Animator.StringToHash("Walk");
    private static readonly int SlamHash = Animator.StringToHash("Slam");
    // Yer yakma klibi: 30 kare, 24 fps. Geri zipla 0.09-0.41 sn, kirbac yere 21. karede (0.875 sn).
    private const float SlamLength = 30f / 24f, SlamHopStart = 0.094f, SlamHopEnd = 0.41f, SlamImpact = 21f / 24f;
    private bool fireDone;
    private float nextSlamTime, nextSlamRoll;

    private Rigidbody2D rb;
    private Health health;
    private Animator animator;
    private SpriteRenderer sr;
    private Transform visual;
    private Transform player;
    private Health playerHealth;

    private State state = State.Patrol;
    private int facing = -1;
    private float startX;
    private int patrolDir = -1;
    private float patrolWaitUntil;
    private float lastSeenTime = -100f;
    private float nextWhipTime;
    private float nextLungeTime, lungeEnd;
    private int lungeDir;
    private float actionStart, actionEnd;
    private bool hitDone;
    private float staggerEnd, staggerImmuneUntil;
    private float moveX;

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
        if (GetComponent<EnemyBody>() == null) gameObject.AddComponent<EnemyBody>();
    }

    private void OnEnable() { health.Damaged += OnDamaged; health.Died += OnDied; }
    private void OnDisable() { health.Damaged -= OnDamaged; health.Died -= OnDied; }
    private void Start() => FindPlayer();

    private void FindPlayer()
    {
        var p = FindAnyObjectByType<PlayerMovement>();
        if (p == null) return;
        player = p.transform;
        playerHealth = p.GetComponent<Health>();
    }

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
            case State.Lunge: UpdateLunge(); break;
            case State.Slam: UpdateSlam(); break;
            case State.Whip: UpdateWhip(); break;
            case State.Stagger:
                if (Time.time >= staggerEnd) state = playerAlive ? State.Chase : State.Patrol;
                break;
        }
        UpdateIdleMotion();
    }

    private void UpdatePatrol(bool playerAlive)
    {
        if (playerAlive && CanSeePlayer()) { BeginAggro(); return; }
        if (patrolDistance <= 0f || Time.time < patrolWaitUntil) return;

        float target = startX + patrolDir * patrolDistance;
        if ((patrolDir < 0 && transform.position.x <= target) || (patrolDir > 0 && transform.position.x >= target) || !GroundAhead(patrolDir))
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
        Vector2 to = player.position - transform.position;
        float dist = Mathf.Abs(to.x);
        if (CanSeePlayer()) lastSeenTime = Time.time;
        if (dist > loseRange || Time.time - lastSeenTime > loseTime)
        {
            state = State.Patrol;
            startX = transform.position.x;
            return;
        }

        int dir = to.x >= 0f ? 1 : -1;
        Face(dir);
        bool inRange = Mathf.Abs(to.y) < verticalSight && dist <= whipRange;
        if (slamEnabled && firePrefab != null && Time.time >= nextSlamTime && Time.time >= nextSlamRoll &&
            Mathf.Abs(to.y) < verticalSight && dist <= slamRange && CanSeePlayer())
        {
            nextSlamRoll = Time.time + slamRollInterval;
            if (Random.value < slamChance) { StartSlam(); return; }   // rastgele: bazen yakar, bazen kirbaclar
        }
        if (inRange && Time.time >= nextWhipTime) { StartWhip(); return; }
        if (lungeEnabled && Time.time >= nextLungeTime && Mathf.Abs(to.y) < verticalSight &&
            dist >= lungeDistance.x && dist <= lungeDistance.y && GroundAhead(dir) && CanSeePlayer())
        { StartLunge(dir); return; }

        // Kirbac mesafesinde durur; cok yakinsa kovalamaz (kirbacci mesafeli dovuser)
        if (dist > whipRange * 0.75f && GroundAhead(dir))
            moveX = dir * chaseSpeed;
    }

    private void StartLunge(int dir)
    {
        state = State.Lunge;
        lungeDir = dir;
        lungeEnd = Time.time + lungeDuration;
        nextLungeTime = Time.time + lungeDuration + lungeCooldown;
    }

    private void UpdateLunge()
    {
        float dist = player != null ? Mathf.Abs(player.position.x - transform.position.x) : 99f;
        bool arrived = dist <= whipRange * 0.7f;
        if (Time.time >= lungeEnd || arrived || !GroundAhead(lungeDir))
        {
            // Atilmanin sonunda menzildeyse hemen kirbac
            if (player != null && dist <= whipRange && Mathf.Abs(player.position.y - transform.position.y) < verticalSight) StartWhip();
            else state = State.Chase;
            return;
        }
        moveX = lungeDir * lungeSpeed;
    }

    private void StartSlam()
    {
        state = State.Slam;
        fireDone = false;
        actionStart = Time.time;
        actionEnd = actionStart + SlamLength + slamRecovery;
        nextSlamTime = actionEnd + Random.Range(Mathf.Min(slamCooldownRange.x, slamCooldownRange.y), Mathf.Max(slamCooldownRange.x, slamCooldownRange.y));
        nextWhipTime = Mathf.Max(nextWhipTime, actionEnd + 0.4f);
        if (animator != null) { animator.speed = 1f; PlayState(SlamHash); }
    }

    private void UpdateSlam()
    {
        float t = Time.time - actionStart;
        // geriye ziplarken arkaya dogru kay (arkada zemin varsa)
        if (t >= SlamHopStart && t <= SlamHopEnd && GroundAhead(-facing))
            moveX = -facing * hopBackSpeed * Mathf.Sin(Mathf.PI * (t - SlamHopStart) / (SlamHopEnd - SlamHopStart)) * 1.4f;
        if (!fireDone && t >= SlamImpact) { fireDone = true; SpawnFire(); }
        if (Time.time >= actionEnd) state = State.Chase;
    }

    private void SpawnFire()
    {
        if (firePrefab == null) return;
        float k = ScaleK;
        float near = Mathf.Min(fireArea.x, fireArea.y) * k, far = Mathf.Max(fireArea.x, fireArea.y) * k;
        float x = transform.position.x + facing * (near + far) * 0.5f;
        float y = transform.position.y;
        var hit = Physics2D.Raycast(new Vector2(x, y + 1.5f), Vector2.down, 6f, groundLayer);
        if (hit.collider != null) y = hit.point.y;
        var fire = Instantiate(firePrefab, new Vector3(x, y, transform.position.z), Quaternion.identity);
        fire.Init(far - near);
    }

    private void StartWhip()
    {
        state = State.Whip;
        hitDone = false;
        actionStart = Time.time;
        actionEnd = actionStart + ClipLength / whipAnimSpeed + whipRecovery;
        nextWhipTime = actionEnd + whipCooldown;
        if (animator != null) { animator.speed = whipAnimSpeed; PlayState(WhipHash); }
    }

    private void UpdateWhip()
    {
        float t = (Time.time - actionStart) * whipAnimSpeed;
        // Kirbac ucu ileri uzanirken (birkac kare boyunca) oyuncuya bir kez vurabilir
        if (!hitDone && t >= WhipHitStart && t <= WhipHitEnd) hitDone = TryHit();
        if (Time.time >= actionEnd)
        {
            if (animator != null) animator.speed = 1f;
            state = State.Chase;
        }
    }

    private bool TryHit()
    {
        GetWhipBox(out Vector2 c, out Vector2 size);
        foreach (var col in Physics2D.OverlapBoxAll(c, size, 0f))
        {
            var target = col.GetComponentInParent<PlayerMovement>();
            if (target == null) continue;
            var h = target.GetComponent<Health>();
            if (h != null)
            {
                int dmg = whipDamagePercent > 0f ? Mathf.Max(1, Mathf.CeilToInt(h.Max * whipDamagePercent)) : whipDamage;
                h.TakeDamage(new DamageInfo(dmg, transform.position, whipKnockback));
            }
            return true;
        }
        return false;
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || state == State.Stagger) return;
        rb.linearVelocity = new Vector2(moveX, rb.linearVelocity.y);
    }

    private void OnDamaged(DamageInfo info)
    {
        if (state == State.Dead) return;
        if (state == State.Patrol) BeginAggro();
        lastSeenTime = Time.time;
        // Yer yakma sirasinda sersemlemez (boss zirhi)
        if (state == State.Slam) return;
        // Kirbac sasklamaya basladiysa vurus kesilmez
        bool committed = state == State.Whip && (Time.time - actionStart) * whipAnimSpeed >= WhipHitStart;
        if (committed || Time.time < staggerImmuneUntil) return;

        state = State.Stagger;
        staggerEnd = Time.time + staggerDuration;
        staggerImmuneUntil = staggerEnd + staggerImmunity;
        if (animator != null) { animator.speed = 1f; PlayState(IdleHash); }

        float dx = transform.position.x - info.sourcePosition.x;
        float dir = Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : -facing;
        rb.linearVelocity = new Vector2(dir * info.knockback * knockbackMultiplier, rb.linearVelocity.y + 1.5f);
        Face(-(int)dir);
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
            transform.position += Vector3.down * (0.4f * Time.deltaTime);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void BeginAggro()
    {
        state = State.Chase;
        lastSeenTime = Time.time;
        nextSlamTime = Mathf.Max(nextSlamTime, Time.time + slamFirstDelay * Random.Range(0.5f, 1.5f));
        nextWhipTime = Mathf.Max(nextWhipTime, Time.time + 0.4f);
    }

    private float ScaleK => Mathf.Abs(transform.lossyScale.y);

    private void GetWhipBox(out Vector2 center, out Vector2 size)
    {
        float k = ScaleK;
        float near = Mathf.Min(whipReach.x, whipReach.y) * k, far = Mathf.Max(whipReach.x, whipReach.y) * k;
        float bottom = Mathf.Min(whipHeight.x, whipHeight.y) * k, top = Mathf.Max(whipHeight.x, whipHeight.y) * k;
        int f = Application.isPlaying ? facing : (transform.localScale.x >= 0f ? -1 : 1);
        center = (Vector2)transform.position + new Vector2((near + far) * 0.5f * f, (bottom + top) * 0.5f);
        size = new Vector2(far - near, top - bottom);
    }

    private bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 to = player.position - transform.position;
        if (Mathf.Abs(to.y) > verticalSight) return false;
        float range = Mathf.Sign(to.x) == facing ? sightRange : backSightRange;
        if (Mathf.Abs(to.x) > range) return false;
        Vector2 eye = (Vector2)transform.position + Vector2.up * 2f * ScaleK;
        return !Physics2D.Linecast(eye, (Vector2)player.position + Vector2.up * 1.5f, groundLayer);
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
        s.x = Mathf.Abs(s.x) * (facing < 0 ? 1f : -1f);
        transform.localScale = s;
    }

    private void PlayState(int hash)
    {
        if (animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, hash))
            animator.Play(hash, 0, 0f);
    }

    // Yururken Walk animasyonu (adim hizi gercek hiza gore), beklerken hafif nefes alma.
    // Walk state'i olmayan eski controller'larda yururken sekme kullanilir.
    private void UpdateIdleMotion()
    {
        if (visual == null || visual == transform) return;
        bool walking = Mathf.Abs(moveX) > 0.01f;
        bool idle = state == State.Patrol || state == State.Chase || state == State.Lunge;

        bool hasWalk = animator != null && animator.runtimeAnimatorController != null && animator.HasState(0, WalkHash);
        if (hasWalk && idle)
        {
            int cur = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
            if (walking)
            {
                if (cur != WalkHash) animator.Play(WalkHash, 0, 0f);
                animator.speed = Mathf.Clamp(Mathf.Abs(moveX) / (walkStrideLength * ScaleK), 0.6f, 2.6f);
            }
            else if (cur == WalkHash)
            {
                animator.speed = 1f;
                animator.Play(IdleHash, 0, 0f);
            }
            float br = walking ? 0f : Mathf.Sin(Time.time * breatheSpeed) * breatheAmount;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.Euler(0f, 0f, state == State.Lunge ? lungeLean : 0f);
            visual.localScale = new Vector3(1f - br * 0.5f, 1f + br, 1f);
            return;
        }
        float s = Mathf.Sin(Time.time * walkBobSpeed);
        float b = idle && !walking ? Mathf.Sin(Time.time * breatheSpeed) * breatheAmount : 0f;
        visual.localPosition = new Vector3(0f, walking ? Mathf.Abs(s) * walkBobAmount : 0f, 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, walking ? s * 2.5f : 0f);
        visual.localScale = new Vector3(1f - b * 0.5f, 1f + b, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        float sx = Application.isPlaying ? startX : transform.position.x;
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, sightRange);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        GetWhipBox(out Vector2 c, out Vector2 size);
        Gizmos.DrawWireCube(c, size);
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.8f);
        Gizmos.DrawLine(new Vector3(sx - patrolDistance, transform.position.y + 0.1f), new Vector3(sx + patrolDistance, transform.position.y + 0.1f));
    }
}
