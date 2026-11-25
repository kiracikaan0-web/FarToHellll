using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Iyilestirme sisesi (soulslike): sinirli sayida kullanim, icerken karakter yavaslar ve savunmasiz kalir.
///  - Tus: R, gamepad LB
///  - Icme baslayinca bir hak harcanir; iyilesme "Heal Time" aninda gelir.
///    O andan once vurulursan (Cancel On Hit aciksa) hak gider ama iyilesmezsin.
///  - Kontrol noktasinda ve oldukten sonra haklar dolar.
/// PlayerMovement yoksa calismaz; oyun basinda Player'a kendiliginden eklenir.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerFlask : MonoBehaviour
{
    [SerializeField, Min(0)] private int maxCharges = 3;
    [Tooltip("Bir icimde dolan can")]
    [SerializeField, Min(1)] private int healAmount = 40;
    [Tooltip("Icme animasyonunun toplam suresi (sn)")]
    [SerializeField, Min(0.1f)] private float drinkDuration = 1.1f;
    [Tooltip("Icmeye baslandiktan kac sn sonra can dolar")]
    [SerializeField, Min(0f)] private float healTime = 0.65f;
    [Tooltip("Icerken yurume hizi carpani")]
    [SerializeField, Range(0f, 1f)] private float drinkMoveSpeed = 0.35f;
    [Tooltip("Iyilesmeden once vurulursan icme bozulur (hak yine gider)")]
    [SerializeField] private bool cancelOnHit = true;
    [SerializeField] private InputAction useAction = new InputAction("Flask", InputActionType.Button);

    [Header("Gorsel (icme animasyonu gelene kadar)")]
    [SerializeField] private Color drinkTint = new Color(1f, 0.85f, 0.55f, 1f);

    public int Charges { get; private set; }
    public int MaxCharges => maxCharges;
    public bool IsDrinking { get; private set; }
    /// <summary>(kalan, maksimum) degisince.</summary>
    public event System.Action<int, int> Changed;

    private PlayerMovement movement;
    private PlayerCombat combat;
    private Health health;
    private SpriteRenderer sr;
    private float drinkStart;
    private bool healed;
    private Color baseColor = Color.white;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();
        sr = GetComponent<SpriteRenderer>();
        Charges = maxCharges;

        if (useAction == null) useAction = new InputAction("Flask", InputActionType.Button);
        if (useAction.bindings.Count == 0)
        {
            useAction.AddBinding("<Keyboard>/r");
            useAction.AddBinding("<Gamepad>/leftShoulder");
        }
    }

    private void OnEnable()
    {
        useAction.Enable();
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        useAction.Disable();
        if (health != null) health.Damaged -= OnDamaged;
        if (IsDrinking) EndDrink();
    }

    private void Start() => Changed?.Invoke(Charges, maxCharges);

    private void Update()
    {
        if (PauseMenu.BlocksGameplayInput) return;

        if (IsDrinking)
        {
            float t = Time.time - drinkStart;
            if (!healed && t >= healTime)
            {
                healed = true;
                if (health != null) health.Heal(healAmount);
                if (GameFeelSettings.Current.hitParticles)
                    GameFeel.Burst((Vector2)transform.position + Vector2.up * 2f, 12, new Color(1f, 0.7f, 0.3f, 1f), 0f);
            }
            // Gecici gorsel: icerken sicak bir parilti
            if (sr != null)
                sr.color = Color.Lerp(baseColor, drinkTint, Mathf.Sin(Mathf.Clamp01(t / drinkDuration) * Mathf.PI));
            if (t >= drinkDuration) EndDrink();
            return;
        }

        if (useAction.WasPressedThisFrame()) TryDrink();
    }

    private void TryDrink()
    {
        if (movement.IsDead || movement.IsHurt || movement.IsDashing || movement.ControlsLocked) return;
        if (combat != null && combat.IsAttacking) return;
        if (Charges <= 0)
        {
            GameFeel.PlaySound("FlaskEmpty", 0.8f);
            return;
        }

        Charges--;
        Changed?.Invoke(Charges, maxCharges);
        IsDrinking = true;
        healed = false;
        drinkStart = Time.time;
        if (sr != null) baseColor = sr.color;
        movement.SpeedMultiplier = drinkMoveSpeed;
        movement.ActionLock = true;
        GameFeel.PlaySound("FlaskDrink", 0.9f);
    }

    private void OnDamaged(DamageInfo info)
    {
        if (IsDrinking && cancelOnHit && !healed) EndDrink();
    }

    private void EndDrink()
    {
        IsDrinking = false;
        movement.SpeedMultiplier = 1f;
        movement.ActionLock = false;
        if (sr != null) sr.color = baseColor;
    }

    /// <summary>Tum haklari doldur (kontrol noktasi).</summary>
    public void Refill()
    {
        Charges = maxCharges;
        Changed?.Invoke(Charges, maxCharges);
    }
}
