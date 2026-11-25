using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sol ustteki can ve stamina barlari (soulslike tarzi).
/// - Hasar alinca can bari hemen duser, arkasindaki sari "kayip" bari biraz bekleyip yavasca erir
/// - Stamina yetmeyince stamina bari kirmizi yanip soner
/// - Bar uzunlugu istenirse maksimum degere gore uzar (can arttikca bar da uzar)
/// Renkler ve cerceve gorseli UI Theme'den gelir.
/// Prefab: Assets/UI/Resources/PlayerHUD.prefab — Player olan her sahneye otomatik eklenir.
/// Kurulum: Tools > FarToHell > Can ve Stamina Kur
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    public const string ResourcePath = "PlayerHUD";

    [Header("Baglantilar (kurulum menusu doldurur)")]
    [SerializeField] private RectTransform healthBar;
    [SerializeField] private RectTransform healthFill;
    [SerializeField] private RectTransform healthChip;
    [SerializeField] private RectTransform staminaBar;
    [SerializeField] private RectTransform staminaFill;
    [SerializeField] private Image[] barBacks;
    [Tooltip("Sise gostergesi (bos birakilirsa oyun basinda barlarin altina kendiliginden olusturulur)")]
    [SerializeField] private RectTransform flaskRoot;
    [SerializeField] private Image flaskIcon;
    [SerializeField] private Text flaskCount;

    [Header("Bar uzunlugu")]
    [Tooltip("Acik: bar uzunlugu maksimum cana/staminaya gore degisir")]
    [SerializeField] private bool widthFollowsMax = true;
    [SerializeField, Min(0f)] private float pixelsPerHealth = 4f;
    [SerializeField, Min(0f)] private float pixelsPerStamina = 3.2f;
    [SerializeField] private Vector2 widthLimits = new Vector2(160f, 1100f);

    [Header("Animasyon")]
    [Tooltip("Sari kayip bari erimeye baslamadan once bekleme (sn)")]
    [SerializeField, Min(0f)] private float chipDelay = 0.45f;
    [Tooltip("Sari barin erime hizi (saniyede barin yuzde kaci)")]
    [SerializeField, Min(0.01f)] private float chipSpeed = 0.6f;
    [SerializeField, Min(0.1f)] private float fillSpeed = 14f;
    [SerializeField, Min(0f)] private float staminaFailFlash = 0.35f;

    [Header("Tema")]
    [Tooltip("Bos birakilirsa varsayilan tema kullanilir")]
    [SerializeField] private UITheme theme;

    private Health health;
    private PlayerStamina stamina;
    private PlayerFlask flask;
    private Sprite flaskFull, flaskEmpty;
    private Image healthFillImg, chipImg, staminaFillImg;
    private Color staminaColor, staminaEmptyColor;
    private float shownHealth = 1f, shownChip = 1f, shownStamina = 1f;
    private float chipWaitUntil;
    private float staminaFlashUntil;

    // --- Otomatik ekleme (PauseMenu ile ayni mantik) ---
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitAutoSpawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SpawnIfNeeded();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnIfNeeded();

    private static void SpawnIfNeeded()
    {
        if (FindAnyObjectByType<PlayerHUD>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<PlayerMovement>() == null) return;
        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("PlayerHUD: Assets/UI/Resources/PlayerHUD.prefab bulunamadi. Tools > FarToHell > Can ve Stamina Kur ile olustur.");
            return;
        }
        var go = Instantiate(prefab);
        go.name = prefab.name;
    }

    // --- Yasam dongusu ---
    private void Awake()
    {
        if (healthFill != null) healthFillImg = healthFill.GetComponent<Image>();
        if (healthChip != null) chipImg = healthChip.GetComponent<Image>();
        if (staminaFill != null) staminaFillImg = staminaFill.GetComponent<Image>();
        ApplyTheme();
    }

    private void Start() => TryBind();

    private void OnDestroy() => Unbind();

    private void TryBind()
    {
        if (health != null) return;
        var player = FindAnyObjectByType<PlayerMovement>();
        if (player == null) return;
        health = player.GetComponent<Health>();
        stamina = player.GetComponent<PlayerStamina>();
        if (health != null)
        {
            health.Changed += OnHealthChanged;
            shownHealth = shownChip = health.Normalized;
            OnHealthChanged(health.Current, health.Max);
        }
        if (stamina != null)
        {
            stamina.Failed += OnStaminaFailed;
            shownStamina = stamina.Normalized;
        }
        flask = player.GetComponent<PlayerFlask>();
        if (flask != null)
        {
            if (flaskRoot == null) BuildFlaskWidget();
            flask.Changed += OnFlaskChanged;
            OnFlaskChanged(flask.Charges, flask.MaxCharges);
        }
    }

    private void OnFlaskChanged(int charges, int max)
    {
        if (flaskCount != null) flaskCount.text = "×" + charges;
        if (flaskIcon != null)
        {
            if (flaskFull == null) flaskFull = Resources.Load<Sprite>("FlaskIcon");
            if (flaskEmpty == null) flaskEmpty = Resources.Load<Sprite>("FlaskIconEmpty");
            var sprite = charges > 0 ? flaskFull : flaskEmpty;
            if (sprite != null) flaskIcon.sprite = sprite;
            flaskIcon.enabled = flaskIcon.sprite != null;
        }
        if (flaskRoot != null) flaskRoot.gameObject.SetActive(max > 0);
    }

    /// <summary>Sise gostergesini kodla olusturur (HUD prefabini yeniden kurmaya gerek kalmasin).</summary>
    private void BuildFlaskWidget()
    {
        var parent = healthBar != null ? healthBar.parent as RectTransform : transform as RectTransform;
        var t = theme != null ? theme : UITheme.Default;

        var root = new GameObject("Flask", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(-6f, -58f);
        root.sizeDelta = new Vector2(160f, 56f);

        var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
        icon.rectTransform.SetParent(root, false);
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(52f, 52f);
        icon.rectTransform.anchoredPosition = Vector2.zero;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var txt = new GameObject("Count", typeof(RectTransform)).AddComponent<Text>();
        txt.rectTransform.SetParent(root, false);
        txt.rectTransform.anchorMin = txt.rectTransform.anchorMax = txt.rectTransform.pivot = new Vector2(0f, 0.5f);
        txt.rectTransform.sizeDelta = new Vector2(100f, 50f);
        txt.rectTransform.anchoredPosition = new Vector2(54f, -2f);
        txt.font = t != null && t.buttonFont != null ? t.buttonFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 34;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.color = t != null ? t.buttonSelected : Color.white;
        txt.raycastTarget = false;
        var sh = txt.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sh.effectDistance = new Vector2(2f, -2f);

        flaskRoot = root;
        flaskIcon = icon;
        flaskCount = txt;
    }

    private void Unbind()
    {
        if (health != null) health.Changed -= OnHealthChanged;
        if (stamina != null) stamina.Failed -= OnStaminaFailed;
        if (flask != null) flask.Changed -= OnFlaskChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        if (widthFollowsMax) SetWidth(healthBar, max * pixelsPerHealth);
        chipWaitUntil = Time.time + chipDelay;
    }

    private void OnStaminaFailed() => staminaFlashUntil = Time.time + staminaFailFlash;

    private void Update()
    {
        if (health == null) { TryBind(); if (health == null) return; }

        float dt = Time.deltaTime;

        // Can: on bar hizla, sari bar gecikmeli
        float hpTarget = health.Normalized;
        shownHealth = Mathf.MoveTowards(shownHealth, hpTarget, fillSpeed * dt);
        if (hpTarget > shownChip) shownChip = hpTarget;                 // iyilesince sari bar hemen yetissin
        else if (Time.time >= chipWaitUntil) shownChip = Mathf.MoveTowards(shownChip, hpTarget, chipSpeed * dt);
        SetFill(healthFill, shownHealth);
        SetFill(healthChip, shownChip);

        // Stamina
        if (stamina != null)
        {
            if (widthFollowsMax) SetWidth(staminaBar, stamina.Max * pixelsPerStamina);
            shownStamina = Mathf.MoveTowards(shownStamina, stamina.Normalized, fillSpeed * dt);
            SetFill(staminaFill, shownStamina);
            if (staminaFillImg != null)
            {
                bool flashing = Time.time < staminaFlashUntil;
                bool pulse = flashing && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f;
                staminaFillImg.color = pulse ? staminaEmptyColor : staminaColor;
                // Bos bar da yanip sonsun ki "stamina yok" belli olsun
                if (barBacks != null && barBacks.Length > 1 && barBacks[1] != null)
                    barBacks[1].color = pulse ? Color.Lerp(BackColor(), staminaEmptyColor, 0.5f) : BackColor();
            }
        }
    }

    private Color backColor = new Color(0.05f, 0.01f, 0.02f, 0.75f);
    private Color BackColor() => backColor;

    /// <summary>Temadaki renkleri uygular (tema degisince de cagrilir).</summary>
    public void ApplyTheme()
    {
        var t = theme != null ? theme : UITheme.Default;
        if (t == null) return;
        if (healthFill != null && healthFillImg == null) healthFillImg = healthFill.GetComponent<Image>();
        if (healthChip != null && chipImg == null) chipImg = healthChip.GetComponent<Image>();
        if (staminaFill != null && staminaFillImg == null) staminaFillImg = staminaFill.GetComponent<Image>();

        if (healthFillImg != null) healthFillImg.color = t.hudHealth;
        if (chipImg != null) chipImg.color = t.hudHealthChip;
        staminaColor = t.hudStamina;
        staminaEmptyColor = t.hudStaminaEmpty;
        if (staminaFillImg != null) staminaFillImg.color = staminaColor;
        backColor = t.hudBarBack;
        if (barBacks != null)
        {
            foreach (var b in barBacks)
            {
                if (b == null) continue;
                b.color = t.hudBarBack;
                var outline = b.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = t.hudBarBorder;
                    outline.enabled = t.hudBarFrame == null; // ozel cerceve gorseli varsa ince cizgiye gerek yok
                }
            }
        }
    }

    private static void SetFill(RectTransform rt, float value)
    {
        if (rt == null) return;
        var max = rt.anchorMax;
        max.x = Mathf.Clamp01(value);
        rt.anchorMax = max;
    }

    private void SetWidth(RectTransform bar, float width)
    {
        if (bar == null) return;
        bar.sizeDelta = new Vector2(Mathf.Clamp(width, widthLimits.x, widthLimits.y), bar.sizeDelta.y);
    }
}
