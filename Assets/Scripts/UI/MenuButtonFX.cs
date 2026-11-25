using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Menu butonu efekti: secilince (fare uzerine gelince ya da klavye/gamepad ile) buyur, rengi
/// ates tonuna doner ve yanlarinda kucuk susler belirir.
/// Font, renkler, sus ve buton arka plani UI Theme'den gelir (Use Theme acikken).
/// Butonun kendi Transition ayari "None" olmali; gorseli bu script yonetir.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonFX : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler
{
    [SerializeField] private Text label;
    [SerializeField] private RectTransform leftDeco;
    [SerializeField] private RectTransform rightDeco;

    [Header("Tema")]
    [Tooltip("Acikken font/renk/susler UI Theme'den alinir; asagidaki renkler yok sayilir.")]
    [SerializeField] private bool useTheme = true;
    [Tooltip("Bos birakilirsa varsayilan tema kullanilir")]
    [SerializeField] private UITheme theme;

    [Header("Tema kapaliyken")]
    [SerializeField] private Color normalColor = new Color(0.80f, 0.72f, 0.64f, 0.85f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.80f, 0.48f, 1f);
    [SerializeField] private float selectedScale = 1.12f;

    [Header("Animasyon")]
    [SerializeField] private float decoGap = 34f;
    [Tooltip("Gecis hizi (1/sn). 8 = yaklasik 0.12 sn")]
    [SerializeField] private float speed = 8f;

    private Button button;
    private bool selected;
    private float blend; // 0 = normal, 1 = secili
    private Graphic leftGraphic, rightGraphic;
    private Color decoColor = new Color(1f, 0.55f, 0.22f, 1f);

    private void Awake()
    {
        button = GetComponent<Button>();
        CacheDecos();
        ApplyTheme();
    }

    private void CacheDecos()
    {
        if (leftDeco != null) leftGraphic = leftDeco.GetComponent<Graphic>();
        if (rightDeco != null) rightGraphic = rightDeco.GetComponent<Graphic>();
    }

    /// <summary>Temadaki font, renk ve gorselleri uygular (tema degisince de cagrilir).</summary>
    public void ApplyTheme()
    {
        if (!useTheme) return;
        var t = theme != null ? theme : UITheme.Default;
        if (t == null) return;

        normalColor = t.buttonNormal;
        selectedColor = t.buttonSelected;
        selectedScale = t.buttonSelectedScale;
        decoColor = t.decoColor;

        if (label != null)
        {
            if (t.buttonFont != null) label.font = t.buttonFont;
            var outline = label.GetComponent<Outline>();
            if (outline != null) outline.effectColor = t.buttonOutline;
        }

        if (leftGraphic == null || rightGraphic == null) CacheDecos();
        ApplyDecoSprite(leftGraphic as Image, t.buttonDeco);
        ApplyDecoSprite(rightGraphic as Image, t.buttonDeco);

        var bg = GetComponent<Image>();
        if (bg != null)
        {
            bg.sprite = t.buttonBackground;
            // Arka plan gorseli yoksa gorunmez tiklama alani olarak kalir
            bg.color = t.buttonBackground != null ? Color.white : new Color(0f, 0f, 0f, 0f);
        }
        Apply();
    }

    private static void ApplyDecoSprite(Image img, Sprite sprite)
    {
        if (img == null) return;
        img.sprite = sprite;
        // Ozel sus gorseli varsa dondurme, yoksa kare 45 derece donup elmas olur
        img.rectTransform.localRotation = sprite != null ? Quaternion.identity : Quaternion.Euler(0f, 0f, 45f);
        if (sprite != null)
        {
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(40f, 40f);
        }
    }

    private void Start() => Apply();

    public void OnSelect(BaseEventData eventData) => selected = true;
    public void OnDeselect(BaseEventData eventData) => selected = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    private void OnDisable()
    {
        // Menu kapanip acilinca eski secim efekti takili kalmasin
        selected = false;
        blend = 0f;
        Apply();
    }

    private void Update()
    {
        float target = selected ? 1f : 0f;
        if (Mathf.Approximately(blend, target)) return;
        blend = Mathf.MoveTowards(blend, target, speed * Time.unscaledDeltaTime);
        Apply();
    }

    private void Apply()
    {
        float e = Mathf.SmoothStep(0f, 1f, blend);
        transform.localScale = Vector3.one * Mathf.Lerp(1f, selectedScale, e);
        if (label != null) label.color = Color.Lerp(normalColor, selectedColor, e);

        if (leftGraphic != null) SetColor(leftGraphic, decoColor, e);
        if (rightGraphic != null) SetColor(rightGraphic, decoColor, e);

        if (label == null) return;
        float half = label.preferredWidth * 0.5f + decoGap + Mathf.Lerp(12f, 0f, e); // susler disaridan iceri kayarak gelsin
        if (leftDeco != null) leftDeco.anchoredPosition = new Vector2(-half, leftDeco.anchoredPosition.y);
        if (rightDeco != null) rightDeco.anchoredPosition = new Vector2(half, rightDeco.anchoredPosition.y);
    }

    private static void SetColor(Graphic g, Color c, float a)
    {
        c.a *= a;
        g.color = c;
    }
}
