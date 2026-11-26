using UnityEngine;

/// <summary>
/// Seytan kizin kirbaci yere vurunca biraktigi yanan alan.
/// Belirli sure (varsayilan 5 sn) yanar; icinde duran oyuncuya araliklarla hasar verir, sonra soner.
/// Alev animasyonunu kendisi oynatir (Animator gerekmez). DemonEnemy.SpawnFire ile olusturulur.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public class DemonFireZone : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float fps = 14f;
    [Tooltip("Ates kac saniye yanar")]
    [SerializeField, Min(0.1f)] private float lifetime = 5f;
    [SerializeField, Min(0f)] private float fadeIn = 0.25f;
    [SerializeField, Min(0f)] private float fadeOut = 0.5f;
    [Tooltip("Icinde durana her vurusta hasar")]
    [SerializeField, Min(1)] private int damage = 8;
    [Tooltip("Icinde durulursa kac sn'de bir yakar")]
    [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;
    [SerializeField, Min(0f)] private float knockback = 3f;

    private SpriteRenderer sr;
    private BoxCollider2D box;
    private float born;
    private float nextTick;
    private float frameOffset;

    /// <summary>Spritelarin dunya genisligi (olcek 1 iken).</summary>
    public float NativeWidth => frames != null && frames.Length > 0 && frames[0] != null ? frames[0].bounds.size.x : 4.57f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
        born = Time.time;
        frameOffset = Random.value * 10f;
    }

    /// <summary>Atesi istenen genislige getirir (yukseklik orantili).</summary>
    public void Init(float width)
    {
        baseScale = Mathf.Max(0.1f, width / NativeWidth);
        transform.localScale = new Vector3(baseScale, baseScale * 0.15f, 1f);
    }

    private float Age => Time.time - born;
    private bool Burning => Age >= fadeIn * 0.5f && Age < lifetime;

    private void Update()
    {
        if (frames != null && frames.Length > 0)
            sr.sprite = frames[(int)((Time.time + frameOffset) * fps) % frames.Length];

        float a = 1f;
        if (Age < fadeIn) a = Age / fadeIn;
        else if (Age > lifetime) a = 1f - (Age - lifetime) / Mathf.Max(0.01f, fadeOut);
        sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        // alevler cikarken yerden boy atsin, sonerken yere insin (pivot altta)
        float grow = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(a));
        transform.localScale = new Vector3(baseScale, baseScale * grow, 1f);

        if (Age > lifetime + fadeOut) Destroy(gameObject);
    }

    private float baseScale = 1f;

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!Burning || Time.time < nextTick) return;
        var pm = other.GetComponentInParent<PlayerMovement>();
        if (pm == null) return;
        var h = pm.GetComponent<Health>();
        if (h == null) return;
        if (h.TakeDamage(new DamageInfo(damage, new Vector2(transform.position.x, transform.position.y - 1f), knockback)))
            nextTick = Time.time + tickInterval;
    }
}
