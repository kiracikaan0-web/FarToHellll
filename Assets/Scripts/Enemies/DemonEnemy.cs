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
    private enum State { Patrol, Chase, Lunge, Whip, Slam, Grapple, Pull, Stagger, Dead }

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
    [SerializeField, Min(0f)] private float hopBackSpeed = 28f;
    [Tooltip("Yanan alan, ileri yonde (baslangic, bitis), olcek 1 iken")]
    [SerializeField] private Vector2 fireArea = new Vector2(0.6f, 7.4f);
    [SerializeField, Min(0f)] private float slamRecovery = 0.35f;

    [Header("Kirbacla sarip cekme (uzaktaki oyuncuya kirbaci atar, sarar ve kendine ceker)")]
    [SerializeField] private bool grappleEnabled = true;
    [Tooltip("Oyuncu bu mesafeler arasindaysa kullanir (min, max), dunya birimi. Min 30: oyuncu 30 birim ve daha uzaktaysa.")]
    [SerializeField] private Vector2 grappleDistance = new Vector2(30f, 50f);
    [Tooltip("Oyuncu bundan yuksekte/alcakta ise kullanmaz")]
    [SerializeField, Min(0f)] private float grappleMaxHeight = 8f;
    [Tooltip("Iki kullanim arasi bekleme (yakalasa da yakalamasa da): bu araliktan rastgele (min, max sn)")]
    [SerializeField] private Vector2 grappleCooldownRange = new Vector2(15f, 20f);
    [Tooltip("Iskalarsa bir sonraki denemeye kadar bekleme: bu araliktan rastgele (min, max sn). Iskalayinca tekrar tekrar atmasin diye uzun.")]
    [SerializeField] private Vector2 grappleMissCooldownRange = new Vector2(15f, 20f);
    [Tooltip("Hazir ve menzildeyken her denemede secme ihtimali (0-1)")]
    [SerializeField, Range(0f, 1f)] private float grappleChance = 0.6f;
    [SerializeField, Min(0.1f)] private float grappleRollInterval = 0.5f;
    [Tooltip("Oyuncuyu ilk gordukten kac sn sonra ilk kez kullanabilir")]
    [SerializeField, Min(0f)] private float grappleFirstDelay = 1f;
    [Tooltip("Kirbacin uzama hizi (birim/sn)")]
    [SerializeField, Min(1f)] private float grappleWhipSpeed = 70f;
    [Tooltip("Kirbac ucu oyuncuya bu kadar yaklasirsa degmis sayilir (olcek 1 iken)")]
    [SerializeField, Min(0.05f)] private float grappleHitRadius = 0.7f;
    [Tooltip("Sarilinca oyuncunun maksimum caninin bu orani kadar hasar (0 = hasar yok, sadece sarar ve ceker)")]
    [SerializeField, Range(0f, 1f)] private float grappleDamagePercent = 0.1f;
    [Tooltip("Kirbacin oyuncunun etrafina sarilma suresi (sn)")]
    [SerializeField, Min(0.05f)] private float wrapTime = 0.4f;
    [Tooltip("Oyuncuyu cekme hizi (birim/sn)")]
    [SerializeField, Min(1f)] private float pullSpeed = 32f;
    [Tooltip("Oyuncu boss'un bu kadar onune birakilir (olcek 1 iken)")]
    [SerializeField, Min(0.2f)] private float pullStopDistance = 2.6f;
    [Tooltip("Cekme en fazla bu kadar surer (sn); oyuncu bir yere takilirsa erken biter")]
    [SerializeField, Min(0.1f)] private float pullMaxTime = 1.6f;
    [Tooltip("Halkalarin cozulme suresi (sn)")]
    [SerializeField, Min(0.05f)] private float unwrapTime = 0.25f;
    [Tooltip("Iskaladiysa kirbaci geri toplama ve toparlanma suresi (sn)")]
    [SerializeField, Min(0f)] private float grappleMissRecovery = 0.5f;
    [Tooltip("Oyundaki kirbacin kalinligi (olcek 1 iken, kok / uc)")]
    [SerializeField] private Vector2 grappleWhipWidth = new Vector2(0.075f, 0.02f);
    [SerializeField] private Color grappleWhipColor = new Color(0.055f, 0.04f, 0.047f, 1f);

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

    // Kirbacla cekilme klibi: 20 kare, 20 fps. Kirbac 9. karede (0.45 sn) elden firlar; sonrasi kol ileride, one egik bekler.
    private static readonly int GrappleHash = Animator.StringToHash("Grapple");
    private const float GrappleRelease = 9f / 20f;
    // Elin (kirbac sapinin) yeri, sprite sola bakarken, ayak noktasina gore (birim, olcek 1): firlatma karesi ve bekleme pozu
    private static readonly Vector2 GrappleHandRelease = new Vector2(-0.593f, 1.072f);
    private static readonly Vector2 GrappleHandHold = new Vector2(-0.668f, 0.916f);
    private enum WhipPhase { None, Out, Wrap, Reel, Unwrap, Retract }
    private WhipPhase whipPhase;
    private float nextGrappleTime, nextGrappleRoll;
    private Vector2 grappleTarget;        // kirbacin gittigi nokta (firlatildigi anda oyuncunun oldugu yer)
    private float whipLen;                // kirbacin su anki uzunlugu
    private float whipPhaseStart;
    private float retractFrom;
    private float retractDuration = 0.3f;
    private Vector2 lastWhipDir = Vector2.left;
    private float pullStart;
    private int wrapSide = -1;
    private PlayerMovement grabbedPlayer, playerMove;
    private Collider2D playerCol;
    private SpriteRenderer playerSprite;
    private LineRenderer whipLine;
    private LineRenderer[] coilLines;
    private Material whipMat;
    private const int WhipPoints = 32;
    private const int CoilLoops = 3, CoilPointsPerHalf = 14;
    // Geri cekme (yank) klibi: 16 kare, 20 fps, dongusuz. Ilk 0.12 sn kirbac gerilir, sonra oyuncu ucmaya baslar.
    private static readonly int YankHash = Animator.StringToHash("Yank");
    private const float YankJerk = 0.12f;
    private bool yanking;
    private bool grappleThrown;   // bu saldirida kirbac elden cikti mi (tek atis)
    private float yankStart;
    // Yank karelerinde elin yeri (sprite sola bakarken, ayak noktasina gore, birim)
    private static readonly Vector2[] YankHand = { new Vector2(-0.658f, 0.916f), new Vector2(-0.675f, 0.961f), new Vector2(-0.479f, 1.221f), new Vector2(-0.356f, 1.293f), new Vector2(-0.337f, 1.299f), new Vector2(-0.351f, 1.297f), new Vector2(-0.376f, 1.286f), new Vector2(-0.399f, 1.274f), new Vector2(-0.409f, 1.266f), new Vector2(-0.333f, 1.313f), new Vector2(-0.316f, 1.316f), new Vector2(-0.319f, 1.314f), new Vector2(-0.327f, 1.309f), new Vector2(-0.339f, 1.303f), new Vector2(-0.351f, 1.296f), new Vector2(-0.360f, 1.291f) };

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
    private void OnDisable()
    {
        health.Damaged -= OnDamaged; health.Died -= OnDied;
        if (state == State.Pull || state == State.Grapple) { ReleasePlayer(Vector2.zero); yanking = false; state = State.Chase; }
        HideCoil();
        whipPhase = WhipPhase.None;
        if (whipLine != null) whipLine.enabled = false;
    }
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
            case State.Grapple: UpdateGrapple(); break;
            case State.Pull: UpdatePull(); break;
            case State.Stagger:
                if (Time.time >= staggerEnd) state = playerAlive ? State.Chase : State.Patrol;
                break;
        }
        UpdateIdleMotion();
    }

    private void LateUpdate() => DrawGrappleWhip();

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
        // kirbacla cekme 30+ birimde kullanildigi icin o mesafede de takibi birakmaz
        float lose = grappleEnabled ? Mathf.Max(loseRange, grappleDistance.y + 2f) : loseRange;
        if (dist > lose || Time.time - lastSeenTime > loseTime)
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
        // Kirbacla cekilme: uzaktaki oyuncuya kirbaci atar, degerse kendini ceker (rastgele, atilmadan once denenir)
        if (grappleEnabled && Time.time >= nextGrappleTime && Time.time >= nextGrappleRoll)
        {
            float full = to.magnitude;
            if (full >= grappleDistance.x && full <= grappleDistance.y && Mathf.Abs(to.y) <= grappleMaxHeight && GrappleClear())
            {
                nextGrappleRoll = Time.time + grappleRollInterval;
                if (Random.value < grappleChance) { StartGrapple(); return; }
            }
        }
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

    // ---------------- Kirbacla sarip cekme ----------------
    // Akis: Grapple (kirbaci basinin ustunden savurur, oyuncunun oldugu yere firlatir)
    //   -> degerse: Wrap (kirbacin ucu oyuncunun etrafina sarilir) -> Reel (boss geri yaslanip ceker, oyuncu ona ucar)
    //   -> Unwrap (halkalar cozulur, oyuncu serbest) ; degmezse: Retract (kirbac geri toplanir)

    private void StartGrapple()
    {
        state = State.Grapple;
        actionStart = Time.time;
        whipPhase = WhipPhase.None;
        grappleThrown = false;
        nextGrappleTime = Time.time + Random.Range(Mathf.Min(grappleCooldownRange.x, grappleCooldownRange.y), Mathf.Max(grappleCooldownRange.x, grappleCooldownRange.y));
        nextLungeTime = Mathf.Max(nextLungeTime, Time.time + 1.5f);
        if (animator != null) { animator.speed = 1f; PlayState(GrappleHash); }
    }

    private void UpdateGrapple()
    {
        float t = Time.time - actionStart;
        // kirbac bir kez atildiysa (iskalayip geri toplandiysa) ayni saldiride tekrar ATILMAZ: saldiri biter
        if (whipPhase == WhipPhase.None && grappleThrown) { EndGrapple(); return; }
        if (whipPhase == WhipPhase.None)
        {
            if (player != null) { float px = player.position.x - transform.position.x; if (Mathf.Abs(px) > 0.1f) Face(px > 0f ? 1 : -1); }
            if (t >= GrappleRelease)
            {
                // kirbac elden cikar: oyuncunun SU ANKI yerine gider (kacarsa iskalar)
                grappleTarget = player != null ? PlayerAimPoint() : (Vector2)transform.position + Vector2.right * facing * 8f;
                whipPhase = WhipPhase.Out;
                whipLen = 0f;
                grappleThrown = true;
                whipPhaseStart = Time.time;
            }
            else if (t > 3f) EndGrapple();
            return;
        }
        if (whipPhase == WhipPhase.Out)
        {
            Vector2 hand = GrappleHand();
            Vector2 to = grappleTarget - hand;
            float maxLen = to.magnitude;
            Vector2 dir = maxLen > 0.001f ? to / maxLen : Vector2.right * facing;
            // hedefin biraz otesine kadar uzanir (oyuncu tam yerinden kipirdadiysa da yakalayabilsin); duvara carparsa orada durur
            maxLen += 1.2f * ScaleK;
            var wall = Physics2D.Raycast(hand, dir, maxLen, groundLayer);
            if (wall.collider != null) maxLen = wall.distance;
            float prev = whipLen;
            whipLen = Mathf.Min(maxLen, whipLen + grappleWhipSpeed * Time.deltaTime);
            lastWhipDir = dir;
            if (WhipTouchesPlayer(hand + dir * prev, hand + dir * whipLen)) { BeginWrap(); return; }
            if (whipLen >= maxLen - 0.001f) {
                BeginRetract(grappleMissRecovery * 0.7f, grappleMissRecovery);
                // iskaladi: hemen tekrar denemesin, uzun sure bekler
                nextGrappleTime = Mathf.Max(nextGrappleTime, Time.time + Random.Range(Mathf.Min(grappleMissCooldownRange.x, grappleMissCooldownRange.y), Mathf.Max(grappleMissCooldownRange.x, grappleMissCooldownRange.y)));
            }
            return;
        }
        // iskaladi: kirbac geri toplanir, sonra toparlanir
        if (whipPhase == WhipPhase.None || Time.time >= actionEnd) EndGrapple();
    }

    private void BeginRetract(float duration, float recover)
    {
        whipPhase = WhipPhase.Retract;
        retractFrom = whipLen;
        retractDuration = Mathf.Max(0.01f, duration);
        whipPhaseStart = Time.time;
        actionEnd = Time.time + recover;
    }

    private void EndGrapple()
    {
        if (whipPhase != WhipPhase.Retract) whipPhase = WhipPhase.None;
        ReleasePlayer(Vector2.zero);
        yanking = false;
        state = State.Chase;
        if (animator != null) { animator.speed = 1f; PlayState(IdleHash); }
    }

    private bool WhipTouchesPlayer(Vector2 a, Vector2 b)
    {
        if (player == null || (playerHealth != null && playerHealth.IsDead)) return false;
        var pm = PlayerMove();
        if (pm != null && pm.BlocksDamage) return false;   // dash ile kacti: kirbac icinden gecer
        float r = grappleHitRadius * ScaleK;
        for (int i = 0; i <= 4; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, i / 4f);
            Vector2 cp = PlayerCol() != null ? PlayerCol().ClosestPoint(p) : PlayerAimPoint();
            if ((cp - p).sqrMagnitude <= r * r) return true;
        }
        return false;
    }

    private void BeginWrap()
    {
        whipPhase = WhipPhase.Wrap;
        whipPhaseStart = Time.time;
        state = State.Pull;                 // boss zirhli, yerinde durur
        pullStart = Time.time;
        wrapSide = player.position.x >= transform.position.x ? -1 : 1;   // halkalar boss'a bakan taraftan baslar
        var pm = PlayerMove();
        if (pm != null) { pm.BeginGrab(); grabbedPlayer = pm; }
        if (grappleDamagePercent > 0f && playerHealth != null)
        {
            int dmg = Mathf.Max(1, Mathf.CeilToInt(playerHealth.Max * grappleDamagePercent));
            playerHealth.TakeDamage(new DamageInfo(dmg, transform.position, 0f));
        }
    }

    private void UpdatePull()
    {
        if (player == null || (playerHealth != null && playerHealth.IsDead)) { BeginUnwrap(); return; }
        float px = player.position.x - transform.position.x;
        if (Mathf.Abs(px) > 0.1f && whipPhase != WhipPhase.Unwrap) Face(px > 0f ? 1 : -1);

        if (whipPhase == WhipPhase.Wrap)
        {
            if (Time.time - whipPhaseStart >= wrapTime)
            {
                // sarma bitti: boss geri yaslanip ceker
                whipPhase = WhipPhase.Reel;
                whipPhaseStart = Time.time;
                yanking = true; yankStart = Time.time;
                if (animator != null) { animator.speed = 1f; PlayState(YankHash); }
            }
            return;
        }
        if (whipPhase == WhipPhase.Reel)
        {
            float tr = Time.time - whipPhaseStart;
            if (tr < YankJerk) { if (grabbedPlayer != null) grabbedPlayer.SetGrabVelocity(Vector2.zero); return; }   // kirbac gerilir
            float k = ScaleK;
            Vector2 center = PlayerAimPoint();
            float halfH = PlayerCol() != null ? PlayerCol().bounds.extents.y : 1f;
            // oyuncu boss'un onunde, ayaklari boss'un ayak hizasinda olacak sekilde birakilir
            Vector2 dest = new Vector2(transform.position.x + facing * pullStopDistance * k, transform.position.y + halfH + 0.05f);
            Vector2 to = dest - center;
            float dist = to.magnitude;
            bool passed = Mathf.Abs(player.position.x - transform.position.x) <= pullStopDistance * k;
            float pulling = tr - YankJerk;
            bool stuck = pulling > 0.3f && grabbedPlayer != null && grabbedPlayer.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude < 2f;
            if (dist < 0.5f || passed || stuck || pulling > pullMaxTime) { BeginUnwrap(); return; }
            // hizlanarak, hedefe yaklasinca hafif yavaslayarak ceker; yolda hafif bir kavis (yukari) cizer
            float spd = pullSpeed * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(pulling / 0.14f)) * Mathf.Clamp(dist / 2.5f, 0.45f, 1f);
            Vector2 v = to / Mathf.Max(0.001f, dist) * spd;
            v.y += Mathf.Clamp(dist, 0f, 10f) * 0.35f * Mathf.Clamp01(1f - pulling / 0.5f);
            if (grabbedPlayer != null) grabbedPlayer.SetGrabVelocity(v);
            return;
        }
        if (whipPhase == WhipPhase.Unwrap)
        {
            if (Time.time - whipPhaseStart >= unwrapTime + 0.2f)
            {
                whipPhase = WhipPhase.None;
                EndGrapple();
            }
            return;
        }
        BeginUnwrap();   // beklenmedik durum: birak
    }

    private void BeginUnwrap()
    {
        if (whipPhase == WhipPhase.Unwrap) return;
        // oyuncu serbest: hafifce yukari ve boss'a dogru savrulur
        ReleasePlayer(new Vector2(facing * 1.5f, 3f));
        whipPhase = WhipPhase.Unwrap;
        whipPhaseStart = Time.time;
        state = State.Pull;
    }

    private void ReleasePlayer(Vector2 v)
    {
        if (grabbedPlayer == null) return;
        grabbedPlayer.EndGrab(v);
        grabbedPlayer = null;
    }

    private PlayerMovement PlayerMove()
    {
        if (playerMove == null && player != null) playerMove = player.GetComponent<PlayerMovement>();
        return playerMove;
    }

    private Collider2D PlayerCol()
    {
        if (playerCol == null && player != null) playerCol = player.GetComponent<Collider2D>();
        return playerCol;
    }

    private Vector2 PlayerAimPoint()
    {
        var c = PlayerCol();
        if (c != null) return c.bounds.center;
        return (Vector2)player.position + Vector2.up * 1.5f * Mathf.Abs(player.lossyScale.y);
    }

    /// <summary>Kirbac sapinin (elin) dunya konumu: o an oynayan karedeki el noktasi.</summary>
    private Vector2 GrappleHand()
    {
        Vector2 local;
        if (yanking)
        {
            float f = Mathf.Clamp((Time.time - yankStart) * 20f, 0f, YankHand.Length - 1);
            int i = Mathf.Min((int)f, YankHand.Length - 2);
            local = Vector2.Lerp(YankHand[i], YankHand[i + 1], f - i);
        }
        else
        {
            float t = Time.time - actionStart;
            local = state == State.Grapple && t < GrappleRelease + 0.05f
                ? Vector2.Lerp(GrappleHandRelease, GrappleHandHold, Mathf.Clamp01((t - GrappleRelease) / 0.05f))
                : GrappleHandHold;
        }
        float lean = visual != null && visual != transform ? visual.localEulerAngles.z : 0f;
        Vector3 v = Quaternion.Euler(0f, 0f, lean) * (Vector3)local;
        return transform.TransformPoint(v);
    }

    private bool GrappleClear()
    {
        if (player == null) return false;
        Vector2 from = (Vector2)transform.position + Vector2.up * 1.0f * ScaleK;
        return !Physics2D.Linecast(from, PlayerAimPoint(), groundLayer);
    }

    // ---- cizim ----

    // Oyundaki kirbac: elden uca. Uzarken dalgalanir; sarinca ucu oyuncunun etrafinda halkalar yapar
    // (halkalarin arka yarisi oyuncunun arkasinda, on yarisi onunde cizilir); cekerken gerilip titrer; cozulurken geri toplanir.
    private void DrawGrappleWhip()
    {
        bool show = whipPhase != WhipPhase.None && state != State.Dead;
        if (whipPhase == WhipPhase.Retract)
        {
            float k = Mathf.Clamp01((Time.time - whipPhaseStart) / retractDuration);
            whipLen = retractFrom * (1f - k * k * (3f - 2f * k));
            if (k >= 1f) { whipPhase = WhipPhase.None; show = false; }
        }
        if (!show)
        {
            if (whipLine != null) whipLine.enabled = false;
            HideCoil();
            return;
        }
        EnsureWhipLine();
        float k0 = ScaleK;
        float age = Time.time - whipPhaseStart;
        Vector2 hand = GrappleHand();

        // sarma ilerlemesi (0 = hic, 1 = tum halkalar)
        float coil = 0f;
        if (whipPhase == WhipPhase.Wrap) coil = Smooth(age / wrapTime);
        else if (whipPhase == WhipPhase.Reel) coil = 1f;
        else if (whipPhase == WhipPhase.Unwrap) coil = 1f - Smooth(age / unwrapTime);

        Vector2 tip;
        bool onPlayer = (whipPhase == WhipPhase.Wrap || whipPhase == WhipPhase.Reel || whipPhase == WhipPhase.Unwrap) && player != null;
        if (onPlayer)
        {
            tip = CoilPoint(0f);
            if (whipPhase == WhipPhase.Unwrap)
            {
                // halkalar cozulunce kirbac ele geri toplanir
                float back = Smooth((age - unwrapTime * 0.6f) / 0.25f);
                tip = Vector2.Lerp(tip, hand, back);
            }
            DrawCoil(coil);
        }
        else
        {
            tip = hand + lastWhipDir * whipLen;
            HideCoil();
        }

        Vector2 d = tip - hand;
        float len = d.magnitude;
        Vector2 dir = len > 0.001f ? d / len : lastWhipDir;
        Vector2 nrm = new Vector2(-dir.y, dir.x);
        whipLine.enabled = len > 0.05f;
        whipLine.widthMultiplier = k0;
        for (int i = 0; i < WhipPoints; i++)
        {
            float s = i / (float)(WhipPoints - 1);
            Vector2 p = hand + d * s;
            float off;
            if (whipPhase == WhipPhase.Wrap)
                off = -0.18f * k0 * Mathf.Sin(Mathf.PI * s) * Mathf.Min(1f, len / 6f) * (1f - coil * 0.7f);   // sarilirken hafif sarkik
            else if (whipPhase == WhipPhase.Reel)
            {
                float tr = age;
                // gerilme ani: tok bir titresim, sonra gergin ve hafifce titreyen kirbac
                float snap = tr < YankJerk + 0.15f ? Mathf.Exp(-(tr) * 9f) : 0f;
                off = (0.22f * snap * Mathf.Sin(tr * 70f) + 0.025f * Mathf.Sin(tr * 55f + s * 12f)) * k0 * Mathf.Sin(Mathf.PI * s);
            }
            else if (whipPhase == WhipPhase.Unwrap)
                off = 0.3f * k0 * Mathf.Sin(s * 8f - age * 30f) * s * Mathf.Min(1f, len / 3f);
            else
            {
                float amp = (whipPhase == WhipPhase.Out ? 0.22f : 0.35f) * k0 * Mathf.Min(1f, len / 3f);
                off = amp * Mathf.Sin(s * 9f - age * 32f) * s * (1f - 0.5f * s);
            }
            whipLine.SetPosition(i, new Vector3(p.x + nrm.x * off, p.y + nrm.y * off, transform.position.z));
        }
    }

    private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // Halkanin a (0..1) noktasi: oyuncunun govdesi etrafinda, yukaridan asagi inen sarmal. Boss'a bakan taraftan baslar.
    private Vector2 CoilPoint(float a)
    {
        Bounds b = PlayerCol() != null ? PlayerCol().bounds : new Bounds(PlayerAimPoint(), new Vector3(0.8f, 2f, 0f));
        float rx = b.extents.x * 1.15f + 0.08f;
        float ry = rx * 0.32f;
        float yTop = b.center.y - b.extents.y * 0.08f, yBot = b.center.y - b.extents.y * 0.55f;   // gogus kafesi ve kollar
        float th = a * Mathf.PI * 2f * CoilLoops;
        float y = Mathf.Lerp(yTop, yBot, a) - ry * Mathf.Sin(th);
        // wrapSide: -1 = oyuncu boss.un saginda (halka sol taraftan, boss tarafindan baslar)
        float x = b.center.x + (wrapSide < 0 ? -1f : 1f) * rx * Mathf.Cos(th);
        return new Vector2(x, y);
    }

    private void DrawCoil(float progress)
    {
        EnsureCoil();
        int halves = CoilLoops * 2;
        float total = progress * halves;   // kac yarim tur gorunur
        int pOrder = 0, pLayer = 0;
        var psr = PlayerSprite();
        if (psr != null) { pOrder = psr.sortingOrder; pLayer = psr.sortingLayerID; }
        for (int h = 0; h < halves; h++)
        {
            var lr = coilLines[h];
            float vis = Mathf.Clamp01(total - h);
            if (vis <= 0.01f) { lr.enabled = false; continue; }
            lr.enabled = true;
            // sin > 0 (alt yari) onde, ust yari oyuncunun arkasinda
            bool front = h % 2 == 0;
            lr.sortingLayerID = pLayer;
            lr.sortingOrder = front ? pOrder + 2 : pOrder - 1;
            Color c = front ? grappleWhipColor : Color.Lerp(grappleWhipColor, Color.black, 0.35f);
            lr.startColor = c; lr.endColor = c;
            lr.widthMultiplier = ScaleK;
            for (int i = 0; i < CoilPointsPerHalf; i++)
            {
                float a = (h + vis * i / (CoilPointsPerHalf - 1f)) / halves;
                Vector2 p = CoilPoint(a);
                lr.SetPosition(i, new Vector3(p.x, p.y, transform.position.z));
            }
        }
    }

    private void HideCoil()
    {
        if (coilLines == null) return;
        foreach (var lr in coilLines) if (lr != null) lr.enabled = false;
    }

    private SpriteRenderer PlayerSprite()
    {
        if (playerSprite == null && player != null) playerSprite = player.GetComponentInChildren<SpriteRenderer>();
        return playerSprite;
    }

    private LineRenderer MakeLine(string name, int points, float w0, float w1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = points;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        if (whipMat == null) { var shader = Shader.Find("Sprites/Default"); if (shader != null) whipMat = new Material(shader); }
        if (whipMat != null) lr.sharedMaterial = whipMat;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, w0), new Keyframe(1f, w1));
        lr.startColor = grappleWhipColor;
        lr.endColor = grappleWhipColor;
        lr.enabled = false;
        return lr;
    }

    private void EnsureWhipLine()
    {
        if (whipLine != null) return;
        whipLine = MakeLine("GrappleWhip", WhipPoints, grappleWhipWidth.x, grappleWhipWidth.y);
        whipLine.widthCurve = new AnimationCurve(new Keyframe(0f, grappleWhipWidth.x), new Keyframe(0.7f, Mathf.Lerp(grappleWhipWidth.x, grappleWhipWidth.y, 0.6f)), new Keyframe(1f, grappleWhipWidth.y));
        if (sr != null) { whipLine.sortingLayerID = sr.sortingLayerID; whipLine.sortingOrder = sr.sortingOrder + 1; }
    }

    private void EnsureCoil()
    {
        if (coilLines != null) return;
        coilLines = new LineRenderer[CoilLoops * 2];
        float w = Mathf.Lerp(grappleWhipWidth.y, grappleWhipWidth.x, 0.55f);
        for (int i = 0; i < coilLines.Length; i++) { coilLines[i] = MakeLine("GrappleCoil" + i, CoilPointsPerHalf, w, w); coilLines[i].numCapVertices = 2; }
    }

    private void FixedUpdate()
    {
        if (state == State.Dead || state == State.Stagger) return;
        if (state == State.Pull) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }   // cekerken yerinde durur
        rb.linearVelocity = new Vector2(moveX, rb.linearVelocity.y);
    }

    private void OnDamaged(DamageInfo info)
    {
        if (state == State.Dead) return;
        if (state == State.Patrol) BeginAggro();
        lastSeenTime = Time.time;
        // Yer yakma ve kirbac firladiktan / cekilirken sersemlemez (boss zirhi)
        if (state == State.Slam || state == State.Pull || (state == State.Grapple && whipPhase != WhipPhase.None)) return;
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
        ReleasePlayer(Vector2.zero);
        HideCoil();
        whipPhase = WhipPhase.None;
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
        nextGrappleTime = Mathf.Max(nextGrappleTime, Time.time + grappleFirstDelay);
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
        // kirbacla kendini cekerken one egik ucar
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
