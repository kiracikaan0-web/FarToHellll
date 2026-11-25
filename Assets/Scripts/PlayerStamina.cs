using System;
using UnityEngine;

/// <summary>
/// Oyuncu staminasi. Dash ve saldiri harcar, bir sure kullanilmayinca dolar.
/// Soulslike kurali: stamina 0'dan buyukse hareket yapilabilir (yetmese bile), sonra 0'a iner.
/// Harcama miktarlari PlayerMovement (dash) ve PlayerCombat (saldiri) icinde ayarlanir.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStamina : MonoBehaviour
{
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [Tooltip("Saniyede dolan miktar")]
    [SerializeField, Min(0f)] private float regenPerSecond = 40f;
    [Tooltip("Harcadiktan sonra dolmaya baslamadan once bekleme (sn)")]
    [SerializeField, Min(0f)] private float regenDelay = 0.7f;
    [Tooltip("Acik: stamina 0'dan buyukse yetmese de hareket yapilir (Dark Souls gibi). Kapali: tam miktar gerekir.")]
    [SerializeField] private bool allowOverspend = true;
    [Tooltip("Tamamen bitince dolmaya baslamadan once ekstra ceza beklemesi (sn)")]
    [SerializeField, Min(0f)] private float exhaustedExtraDelay = 0.5f;

    /// <summary>(mevcut, maksimum) degisince.</summary>
    public event Action<float, float> Changed;
    /// <summary>Yetersiz stamina yuzunden bir hareket yapilamayinca (HUD bari titretmek icin).</summary>
    public event Action Failed;

    public float Current { get; private set; }
    public float Max => maxStamina;
    public float Normalized => maxStamina > 0f ? Current / maxStamina : 0f;

    private float regenStartTime;

    private void Awake() => Current = maxStamina;
    private void Start() => Changed?.Invoke(Current, maxStamina);

    public bool CanSpend(float amount) => allowOverspend ? Current > 0f : Current >= amount;

    /// <summary>Yeterliyse harcar ve true doner; degilse false doner (Failed olayi tetiklenir).</summary>
    public bool TrySpend(float amount)
    {
        if (amount <= 0f) return true;
        if (!CanSpend(amount))
        {
            Failed?.Invoke();
            return false;
        }
        Current = Mathf.Max(0f, Current - amount);
        regenStartTime = Time.time + regenDelay + (Current <= 0f ? exhaustedExtraDelay : 0f);
        Changed?.Invoke(Current, maxStamina);
        return true;
    }

    public void Refill()
    {
        Current = maxStamina;
        Changed?.Invoke(Current, maxStamina);
    }

    private void Update()
    {
        if (Current >= maxStamina || Time.time < regenStartTime) return;
        Current = Mathf.Min(maxStamina, Current + regenPerSecond * Time.deltaTime);
        Changed?.Invoke(Current, maxStamina);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        Current = Mathf.Min(Current, maxStamina);
        Changed?.Invoke(Current, maxStamina);
    }
#endif
}
