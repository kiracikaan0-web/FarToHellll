using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Menu kurulum scriptlerinin (ana menu, duraklatma) ortak yardimcilari ve varsayilan UI Theme.
/// </summary>
public static class UIBuildUtil
{
    public const string UIFolder = "Assets/UI/";
    public const string ThemePath = "Assets/UI/Resources/UITheme.asset";
    public const string FontPath = "Assets/Fonts/Cardo-Bold.ttf";

    /// <summary>Varsayilan temayi bulur; yoksa mevcut gorsellerle olusturur. Var olan temaya dokunmaz.</summary>
    public static UITheme GetOrCreateTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        if (theme != null) return theme;

        if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder("Assets/UI/Resources")) AssetDatabase.CreateFolder("Assets/UI", "Resources");

        theme = ScriptableObject.CreateInstance<UITheme>();
        theme.buttonFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        theme.titleFont = theme.buttonFont;
        theme.menuTitle = ImportUISprite(UIFolder + "MenuTitle.png");
        theme.menuVignette = ImportUISprite(UIFolder + "MenuVignette.png");
        theme.pausePanel = ImportUISprite(UIFolder + "PausePanel.png");
        AssetDatabase.CreateAsset(theme, ThemePath);
        AssetDatabase.SaveAssets();
        return theme;
    }

    public static Sprite ImportUISprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        if (importer.textureType != TextureImporterType.Sprite ||
            importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform CreateRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    public static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var img = CreateRect(name, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return img;
    }

    /// <summary>Gorseli temadan gelen Image (tasarimci degistirince otomatik guncellenir).</summary>
    public static Image CreateThemedImage(string name, RectTransform parent, UITheme theme, UITheme.Slot slot,
        bool hideIfEmpty = false, GameObject showWhenEmpty = null)
    {
        var img = CreateImage(name, parent, theme.Get(slot), Color.white);
        img.raycastTarget = false;
        var tg = img.gameObject.AddComponent<ThemedGraphic>();
        var so = new SerializedObject(tg);
        so.FindProperty("slot").enumValueIndex = (int)slot;
        so.FindProperty("hideIfEmpty").boolValue = hideIfEmpty;
        so.FindProperty("showWhenEmpty").objectReferenceValue = showWhenEmpty;
        so.ApplyModifiedPropertiesWithoutUndo();
        tg.Apply();
        return img;
    }

    public static Text CreateText(string name, RectTransform parent, Font font, string text, int size, Color color)
    {
        var t = CreateRect(name, parent).gameObject.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Text CreateThemedText(string name, RectTransform parent, UITheme theme, ThemedText.Role role, string text, int size)
    {
        var font = role == ThemedText.Role.Title && theme.titleFont != null ? theme.titleFont : theme.buttonFont;
        var t = CreateText(name, parent, font, text, size, role == ThemedText.Role.Title ? theme.titleColor : theme.hintColor);
        var tt = t.gameObject.AddComponent<ThemedText>();
        var so = new SerializedObject(tt);
        so.FindProperty("role").enumValueIndex = (int)role;
        so.ApplyModifiedPropertiesWithoutUndo();
        return t;
    }

    public static Button CreateMenuButton(RectTransform parent, string name, string text, UITheme theme, int fontSize = 64)
    {
        var rt = CreateRect(name, parent);
        rt.sizeDelta = new Vector2(600f, fontSize * 1.55f);

        // Tiklama alani (temada buton arka plani varsa o gorunur)
        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        var btn = rt.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = hit;

        var label = CreateText("Label", rt, theme.buttonFont, text, fontSize, theme.buttonNormal);
        Stretch(label.rectTransform);
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(3f, -3f);
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.buttonOutline;
        outline.effectDistance = new Vector2(2f, -2f);

        var left = CreateDeco("DecoLeft", rt);
        var right = CreateDeco("DecoRight", rt);

        var fx = rt.gameObject.AddComponent<MenuButtonFX>();
        var so = new SerializedObject(fx);
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("leftDeco").objectReferenceValue = left;
        so.FindProperty("rightDeco").objectReferenceValue = right;
        so.ApplyModifiedPropertiesWithoutUndo();
        return btn;
    }

    private static RectTransform CreateDeco(string name, RectTransform parent)
    {
        var img = CreateImage(name, parent, null, new Color(1f, 0.55f, 0.22f, 0f));
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(14f, 14f);
        rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var outline = img.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.25f, 0.03f, 0.02f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);
        return rt;
    }

    public static RectTransform CreateVerticalList(string name, RectTransform parent, Vector2 size, Vector2 position, float spacing)
    {
        var list = CreateRect(name, parent);
        list.anchorMin = list.anchorMax = list.pivot = new Vector2(0.5f, 0.5f);
        list.sizeDelta = size;
        list.anchoredPosition = position;
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return list;
    }

    /// <summary>Butonlar arasinda dongusel yukari/asagi gezinme.</summary>
    public static void LinkVertical(params Button[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length],
                selectOnDown = buttons[(i + 1) % buttons.Length]
            };
        }
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
