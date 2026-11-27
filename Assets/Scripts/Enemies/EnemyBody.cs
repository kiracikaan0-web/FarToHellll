using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dusman govdesi:
///  - Oyuncu dusmanin kafasina inerse kayip yana duser (kafada durulamaz)
///  - Govde collider'i kaygandir (surtunmesiz), oyuncu kenarina tutunamaz
///  - Oyuncu dash/geri adim atarken bu dusmanin icinden gecebilir (PlayerMovement bu listeyi kullanir)
///  - Oyuncu dusmana degerse (govdeye ya da tum govdeyi saran hasar alanina) maksimum caninin %10'u kadar hasar alir
/// SkeletonEnemy bunu kendiliginden ekler; yeni dusman tiplerine de eklemen yeterli.
/// </summary>
[DisallowMultipleComponent]
public class EnemyBody : MonoBehaviour
{
    /// <summary>Sahnedeki tum dusman govdeleri.</summary>
    public static readonly List<EnemyBody> All = new List<EnemyBody>();

    /// <summary>Bu isimde bir alt obje (trigger collider) varsa temas hasari SADECE ona degince verilir
    /// (boss'ta kirbacli kol bu alanin disinda). Yoksa govde ve tum hasar alani temas sayilir.</summary>
    public const string ContactAreaName = "ContactArea";

    /// <summary>Bu isimdeki alt obje (trigger) kilictan hasar ALMAZ ama oyuncu degince temas hasari verir
    /// (Skeleton2'nin elinden dirsegine kadar olan kismi).</summary>
    public const string HandAreaName = "HandArea";
    [Tooltip("Kafaya basan oyuncunun yana kayma hizi")]
    [SerializeField, Min(0f)] private float headSlideSpeed = 7f;
    [Tooltip("Oyuncunun ayagi dusmanin tepesinden bu kadar asagidaysa hala 'kafada' sayilir")]
    [SerializeField, Min(0f)] private float headTolerance = 0.35f;

    [Header("Temas hasari")]
    [Tooltip("Oyuncu bu dusmana degince maksimum caninin bu orani kadar hasar alir (0.1 = %10). 0 = kapali.")]
    [SerializeField, Range(0f, 1f)] private float contactDamagePercent = 0.1f;
    [Tooltip("Degmeye devam ederse kac sn'de bir tekrar hasar alir")]
    [SerializeField, Min(0.1f)] private float contactInterval = 1f;
    [Tooltip("Degince oyuncuyu dusmandan uzaga itme gucu")]
    [SerializeField, Min(0f)] private float contactKnockback = 8f;

    private Collider2D[] colliders;
    private static PhysicsMaterial2D slippery;
    private Health myHealth;
    private Collider2D contactArea;
    private float nextContactTime;

    public Collider2D[] Colliders => colliders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    private void Awake()
    {
        var list = new List<Collider2D>();
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) list.Add(c);
        colliders = list.ToArray();

        myHealth = GetComponent<Health>();
        var ca = transform.Find(ContactAreaName);
        if (ca != null) contactArea = ca.GetComponent<Collider2D>();

        if (slippery == null) slippery = new PhysicsMaterial2D("EnemySlippery") { friction = 0f, bounciness = 0f };
        foreach (var c in colliders)
            if (c.sharedMaterial == null) c.sharedMaterial = slippery;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void OnCollisionEnter2D(Collision2D collision) { CheckHead(collision); TryContactDamage(collision.collider); }
    private void OnCollisionStay2D(Collision2D collision) { CheckHead(collision); TryContactDamage(collision.collider); }
    // tum govdeyi saran hasar alani (trigger) da temas sayilir: gorunen vucuda degmek yeter
    private void OnTriggerEnter2D(Collider2D other) => TryContactDamage(other);
    private void OnTriggerStay2D(Collider2D other) => TryContactDamage(other);

    private void TryContactDamage(Collider2D other)
    {
        if (contactDamagePercent <= 0f || Time.time < nextContactTime) return;
        if (myHealth != null && myHealth.IsDead) return;
        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        // temas alani tanimliysa (boss) oyuncu ona degmiyorsa sayma: elindeki kirbaca degmek hasar vermez
        if (contactArea != null && contactArea.enabled && !other.IsTouching(contactArea)) return;
        var h = player.GetComponent<Health>();
        if (h == null || h.IsDead) return;
        int dmg = Mathf.Max(1, Mathf.CeilToInt(h.Max * contactDamagePercent));
        // dash i-frame'i ya da vurulma sonrasi hasarsizlik varsa Health kendisi reddeder
        if (h.TakeDamage(new DamageInfo(dmg, transform.position, contactKnockback)))
            nextContactTime = Time.time + contactInterval;
    }

    private void CheckHead(Collision2D collision)
    {
        var player = collision.collider.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        // Oyuncunun ayagi bizim tepemizin hizasinda mi?
        float myTop = float.MinValue;
        foreach (var c in colliders) if (c.enabled) myTop = Mathf.Max(myTop, c.bounds.max.y);
        float playerBottom = collision.collider.bounds.min.y;
        if (playerBottom < myTop - headTolerance) return;

        float dir = player.transform.position.x - transform.position.x;
        player.PushOffEnemyHead(dir, headSlideSpeed);
    }
}
