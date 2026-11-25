using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ayarlar menusundeki bir satir:  "Muzik            ‹  %80  ›"
/// Klavye/gamepad: yukari-asagi ile satir sec, sol-sag ile degistir, Enter ile ileri.
/// Fare: degerin solundaki/sagindaki oka (ya da o tarafa) tikla.
/// Renkler ve font UI Theme'den gelir.
/// </summary>
public class SettingsRow : Selectable, ISubmitHandler, IPointerClickHandler
{
    public enum Kind { MasterVolume, MusicVolume, SfxVolume, Fullscreen, Resolution, VSync }

    [SerializeField] private Kind kind;
    [SerializeField] private Text label;
    [SerializeField] private Text valueText;
    [Tooltip("Ses satirlarinda doluluk bari (diger satirlarda gizlenir)")]
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private RectTransform barFill;
    [SerializeField, Range(0.01f, 0.5f)] private float volumeStep = 0.1f;

    [Header("Tema")]
    [SerializeField] private UITheme theme;

    private bool isSelected;
    private float blend;
    private Color normalColor = new Color(0.80f, 0.72f, 0.64f, 0.85f);
    private Color selectedColor = new Color(1f, 0.80f, 0.48f, 1f);
    private Image barFillImage;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;
        if (barFill != null) barFillImage = barFill.GetComponent<Image>();
        ApplyTheme();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        isSelected = false;
        blend = 0f;
        Refresh();
        ApplyVisual();
    }

    public void ApplyTheme()
    {
        var t = theme != null ? theme : UITheme.Default;
        if (t == null) return;
        normalColor = t.buttonNormal;
        selectedColor = t.buttonSelected;
        if (t.buttonFont != null)
        {
            if (label != null) label.font = t.buttonFont;
            if (valueText != null) valueText.font = t.buttonFont;
        }
        if (barFillImage != null) barFillImage.color = t.decoColor;
    }

    // ---------------- Girdi ----------------

    public override void OnSelect(BaseEventData eventData) { base.OnSelect(eventData); isSelected = true; }
    public override void OnDeselect(BaseEventData eventData) { base.OnDeselect(eventData); isSelected = false; }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (IsInteractable() && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left) { Change(-1); eventData.Use(); return; }
        if (eventData.moveDir == MoveDirection.Right) { Change(1); eventData.Use(); return; }
        base.OnMove(eventData);
    }

    public void OnSubmit(BaseEventData eventData) => Change(1);

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || valueText == null) return;
        // Degerin solu = azalt, sagi = arttir
        RectTransformUtility.ScreenPointToLocalPointInRectangle(valueText.rectTransform, eventData.position,
            eventData.pressEventCamera, out Vector2 local);
        Change(local.x < valueText.rectTransform.rect.center.x ? -1 : 1);
    }

    // ---------------- Deger ----------------

    private void Change(int dir)
    {
        switch (kind)
        {
            case Kind.MasterVolume: GameSettings.SetMasterVolume(Step(GameSettings.MasterVolume, dir)); break;
            case Kind.MusicVolume: GameSettings.SetMusicVolume(Step(GameSettings.MusicVolume, dir)); break;
            case Kind.SfxVolume: GameSettings.SetSfxVolume(Step(GameSettings.SfxVolume, dir)); break;
            case Kind.Fullscreen: GameSettings.SetFullscreen(!GameSettings.Fullscreen); break;
            case Kind.VSync: GameSettings.SetVSync(!GameSettings.VSync); break;
            case Kind.Resolution: GameSettings.CycleResolution(dir); break;
        }
        Refresh();
    }

    private float Step(float v, int dir) => Mathf.Clamp01(Mathf.Round((v + dir * volumeStep) / volumeStep) * volumeStep);

    public void Refresh()
    {
        float vol = -1f;
        string value;
        switch (kind)
        {
            case Kind.MasterVolume: vol = GameSettings.MasterVolume; value = Percent(vol); break;
            case Kind.MusicVolume: vol = GameSettings.MusicVolume; value = Percent(vol); break;
            case Kind.SfxVolume: vol = GameSettings.SfxVolume; value = Percent(vol); break;
            case Kind.Fullscreen: value = GameSettings.Fullscreen ? "AÇIK" : "KAPALI"; break;
            case Kind.VSync: value = GameSettings.VSync ? "AÇIK" : "KAPALI"; break;
            default:
                var r = GameSettings.Resolution;
                value = r.x > 0 ? $"{r.x} × {r.y}" : $"{Screen.width} × {Screen.height}";
                break;
        }
        if (valueText != null) valueText.text = "‹    " + value + "    ›";

        if (barRoot != null) barRoot.gameObject.SetActive(vol >= 0f);
        if (barFill != null && vol >= 0f)
        {
            var max = barFill.anchorMax;
            max.x = vol;
            barFill.anchorMax = max;
        }
    }

    private static string Percent(float v) => "%" + Mathf.RoundToInt(v * 100f);

    // ---------------- Gorunum ----------------

    private void Update()
    {
        float target = isSelected ? 1f : 0f;
        if (Mathf.Approximately(blend, target)) return;
        blend = Mathf.MoveTowards(blend, target, 8f * Time.unscaledDeltaTime);
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        float e = Mathf.SmoothStep(0f, 1f, blend);
        var c = Color.Lerp(normalColor, selectedColor, e);
        if (label != null) label.color = c;
        if (valueText != null) valueText.color = c;
        transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.04f, e);
    }
}
