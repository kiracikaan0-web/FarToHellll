using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Sol alttaki 4 slotluk esya bari. Inventory'deki esyalari ikon ve adetle gosterir.
/// Esya alininca slot zipla, ustunde "+1 Altın" yazisi belirir.
/// Prefab gerekmez: karakter olan her sahneye oyun basinda kendiliginden eklenir (kodla kurulur).
/// Renkler ve font UI Theme'den gelir. Tasarimci slot cercevesi gonderirse slotFrame'e verilebilir.
/// </summary>
public class InventoryBar : MonoBehaviour
{
    [Header("Yerlesim")]
    [SerializeField] private float slotSize = 84f;
    [SerializeField] private float spacing = 10f;
    [Tooltip("Ekranin sol alt kosesinden uzaklik")]
    [SerializeField] private Vector2 margin = new Vector2(40f, 36f);
    [Tooltip("Slot cercevesi (bos = duz kutu)")]
    [SerializeField] private Sprite slotFrame;

    private class SlotView
    {
        public RectTransform root;
        public Image back, icon;
        public Text count;
        public float punch;
    }

    private SlotView[] views;
    private Text popup;
    private float popupUntil;
    private UITheme theme;

    // ---------------- Otomatik ekleme ----------------
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
        if (FindAnyObjectByType<InventoryBar>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<PlayerMovement>() == null) return;
        new GameObject("InventoryBar", typeof(RectTransform)).AddComponent<InventoryBar>();
    }

    // ---------------- Kurulum ----------------
    private void Awake()
    {
        theme = UITheme.Default;
        Build();
    }

    private void OnEnable()
    {
        Inventory.Changed += OnSlotChanged;
        Inventory.Added += OnAdded;
        RefreshAll();
    }

    private void OnDisable()
    {
        Inventory.Changed -= OnSlotChanged;
        Inventory.Added -= OnAdded;
    }

    private Font UIFont => theme != null && theme.buttonFont != null ? theme.buttonFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;   // HUD seviyesinde; duraklatma ve olum ekranlari ustte kalir
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Color back = theme != null ? theme.hudBarBack : new Color(0.05f, 0.01f, 0.02f, 0.75f);
        Color border = theme != null ? theme.hudBarBorder : new Color(0.55f, 0.40f, 0.28f, 0.9f);
        Color text = theme != null ? theme.buttonSelected : Color.white;
        Color hint = theme != null ? theme.hintColor : new Color(1f, 1f, 1f, 0.5f);

        var bar = NewRect("Slots", transform as RectTransform);
        bar.anchorMin = bar.anchorMax = bar.pivot = Vector2.zero;
        bar.anchoredPosition = margin;
        bar.sizeDelta = new Vector2(Inventory.SlotCount * slotSize + (Inventory.SlotCount - 1) * spacing, slotSize);

        views = new SlotView[Inventory.SlotCount];
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            var v = new SlotView();
            v.root = NewRect("Slot" + (i + 1), bar);
            v.root.anchorMin = v.root.anchorMax = v.root.pivot = new Vector2(0f, 0.5f);
            v.root.sizeDelta = new Vector2(slotSize, slotSize);
            v.root.anchoredPosition = new Vector2(i * (slotSize + spacing) + slotSize * 0.5f, 0f);
            v.root.pivot = new Vector2(0.5f, 0.5f);

            v.back = v.root.gameObject.AddComponent<Image>();
            v.back.raycastTarget = false;
            v.back.color = back;
            if (slotFrame != null) { v.back.sprite = slotFrame; v.back.type = Image.Type.Sliced; v.back.color = Color.white; }
            else
            {
                var ol = v.root.gameObject.AddComponent<Outline>();
                ol.effectColor = border;
                ol.effectDistance = new Vector2(2f, -2f);
            }

            var iconRT = NewRect("Icon", v.root);
            Stretch(iconRT, 14f);
            v.icon = iconRT.gameObject.AddComponent<Image>();
            v.icon.preserveAspect = true;
            v.icon.raycastTarget = false;
            v.icon.enabled = false;

            v.count = NewText("Count", v.root, 28, TextAnchor.LowerRight, text);
            Stretch(v.count.rectTransform, 5f);

            var key = NewText("Key", v.root, 20, TextAnchor.UpperLeft, hint);
            Stretch(key.rectTransform, 5f);
            key.text = (i + 1).ToString();

            views[i] = v;
        }

        popup = NewText("Popup", bar, 30, TextAnchor.LowerLeft, text);
        popup.rectTransform.anchorMin = popup.rectTransform.anchorMax = popup.rectTransform.pivot = new Vector2(0f, 1f);
        popup.rectTransform.sizeDelta = new Vector2(400f, 44f);
        popup.rectTransform.anchoredPosition = new Vector2(0f, 48f);
        popup.text = "";
    }

    // ---------------- Guncelleme ----------------
    private void RefreshAll()
    {
        if (views == null) return;
        for (int i = 0; i < views.Length; i++) OnSlotChanged(i);
    }

    private void OnSlotChanged(int i)
    {
        if (views == null || i < 0 || i >= views.Length) return;
        var s = Inventory.Get(i);
        var v = views[i];
        v.icon.enabled = !s.IsEmpty && s.item.Icon != null;
        if (v.icon.enabled) v.icon.sprite = s.item.Icon;
        v.count.text = !s.IsEmpty && (s.count > 1 || s.item.maxStack > 1) ? s.count.ToString() : "";
    }

    private void OnAdded(ItemDefinition item, int amount, int slot)
    {
        if (views != null && slot >= 0 && slot < views.Length) views[slot].punch = 1f;
        if (popup != null)
        {
            popup.text = "+" + amount + " " + item.displayName;
            popupUntil = Time.unscaledTime + 1.4f;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (views != null)
            foreach (var v in views)
            {
                v.punch = Mathf.MoveTowards(v.punch, 0f, dt * 4f);
                float s = 1f + 0.18f * Mathf.Sin(v.punch * Mathf.PI);
                v.root.localScale = new Vector3(s, s, 1f);
            }
        if (popup != null)
        {
            float left = popupUntil - Time.unscaledTime;
            var c = popup.color;
            c.a = Mathf.Clamp01(left / 0.4f);
            popup.color = c;
            popup.rectTransform.anchoredPosition = new Vector2(0f, 48f + (1.4f - Mathf.Clamp(left, 0f, 1.4f)) * 14f);
        }
    }

    // ---------------- Yardimcilar ----------------
    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    private Text NewText(string name, RectTransform parent, int size, TextAnchor align, Color color)
    {
        var t = NewRect(name, parent).gameObject.AddComponent<Text>();
        t.font = UIFont;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sh = t.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(2f, -2f);
        return t;
    }
}
