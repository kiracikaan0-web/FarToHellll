using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Bir vurusun bilgisi: ne kadar hasar, nereden geldi, ne kadar itecek.</summary>
public struct DamageInfo
{
    public int amount;
    public Vector2 sourcePosition;
    public float knockback;

    public DamageInfo(int amount, Vector2 sourcePosition, float knockback = 0f)
    {
        this.amount = amount;
        this.sourcePosition = sourcePosition;
        this.knockback = knockback;
    }
}

/// <summary>
/// Ayni objedeki bir script hasari engelleyebilir (ornegin dash sirasindaki i-frame).
/// </summary>
public interface IDamageBlocker
{
    bool BlocksDamage { get; }
}

/// <summary>
/// Ortak can sistemi: oyuncu da dusmanlar da kullanir.
/// - TakeDamage(int) ya da TakeDamage(DamageInfo) ile hasar alir
/// - Vurulunca kisa sure hasarsiz kalir (i-frame) ve sprite kirmizi parlar
/// - Olaylar: Damaged, Died, Changed (koddan) ve OnDamaged / OnDeath (Inspector'dan)
/// </summary>
[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("Can")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [Tooltip("Vurulduktan sonra hasar almama suresi (sn)")]
    [SerializeField, Min(0f)] private float invulnerableAfterHit = 0.5f;

    [Header("Vurulma parlamasi")]
    [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField, Min(0f)] private float flashDuration = 0.12f;

    [Header("Inspector olaylari (istege bagli)")]
    public UnityEvent OnDamaged;
    public UnityEvent OnDeath;

    /// <summary>Hasar alindiginda (olmeden once de cagrilir).</summary>
    public event Action<DamageInfo> Damaged;
    /// <summary>Can sifira indiginde bir kez.</summary>
    public event Action Died;
    /// <summary>(mevcut, maksimum) degisince.</summary>
    public event Action<int, int> Changed;

    /// <summary>Herhangi bir Health hasar alinca (GameFeel gibi genel sistemler icin).</summary>
    public static event Action<Health, DamageInfo> AnyDamaged;
    /// <summary>Herhangi bir Health olunce.</summary>
    public static event Action<Health> AnyDied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { AnyDamaged = null; AnyDied = null; }

    public int Current { get; private set; }
    public int Max => maxHealth;
    public float Normalized => maxHealth > 0 ? (float)Current / maxHealth : 0f;
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil || IsBlocked();

    private float invulnerableUntil;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = Color.white;
    private float flashUntil;
    private IDamageBlocker[] blockers;

    private void Awake()
    {
        Current = maxHealth;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(); // gorsel alt objede de olabilir (dusmanlar)
        if (spriteRenderer != null) baseColor = spriteRenderer.color;
        blockers = GetComponents<IDamageBlocker>();
    }

    private void Start() => Changed?.Invoke(Current, maxHealth);

    /// <summary>SendMessage("TakeDamage", int) ile de cagrilabilir.</summary>
    public void TakeDamage(int amount) => TakeDamage(new DamageInfo(amount, transform.position));

    public bool TakeDamage(DamageInfo info)
    {
        if (IsDead || info.amount <= 0 || IsInvulnerable) return false;

        Current = Mathf.Max(0, Current - info.amount);
        invulnerableUntil = Time.time + invulnerableAfterHit;
        Flash();

        Damaged?.Invoke(info);
        AnyDamaged?.Invoke(this, info);
        OnDamaged?.Invoke();
        Changed?.Invoke(Current, maxHealth);

        if (Current == 0)
        {
            IsDead = true;
            Died?.Invoke();
            AnyDied?.Invoke(this);
            OnDeath?.Invoke();
        }
        return true;
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>Tam can ile yeniden baslat (yeniden dogma, kontrol noktasi).</summary>
    public void ResetHealth()
    {
        IsDead = false;
        Current = maxHealth;
        invulnerableUntil = 0f;
        Changed?.Invoke(Current, maxHealth);
    }

    public void SetMaxHealth(int value, bool refill)
    {
        maxHealth = Mathf.Max(1, value);
        Current = refill ? maxHealth : Mathf.Min(Current, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

    private bool IsBlocked()
    {
        if (blockers == null) return false;
        foreach (var b in blockers)
            if (b != null && b.BlocksDamage) return true;
        return false;
    }

    private void Flash()
    {
        if (spriteRenderer == null || flashDuration <= 0f) return;
        spriteRenderer.color = flashColor;
        flashUntil = Time.time + flashDuration;
    }

    private void Update()
    {
        if (spriteRenderer != null && flashUntil > 0f && Time.time >= flashUntil)
        {
            flashUntil = 0f;
            spriteRenderer.color = baseColor;
        }
    }

#if UNITY_EDITOR
    // Inspector'da maxHealth degisince Play sirasinda da bara yansisin
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        Current = Mathf.Min(Current, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }
#endif
}
