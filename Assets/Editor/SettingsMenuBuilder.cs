using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ayarlar panelini olusturur (Ana Menu Kur kullanir; ileride duraklatma menusune de eklenebilir).
/// </summary>
public static class SettingsMenuBuilder
{
    private static readonly (string label, SettingsRow.Kind kind)[] AudioRows =
    {
        ("Genel Ses", SettingsRow.Kind.MasterVolume),
        ("Müzik", SettingsRow.Kind.MusicVolume),
        ("Efektler", SettingsRow.Kind.SfxVolume),
    };

    private static readonly (string label, SettingsRow.Kind kind)[] DisplayRows =
    {
        ("Tam Ekran", SettingsRow.Kind.Fullscreen),
        ("Çözünürlük", SettingsRow.Kind.Resolution),
        ("Dikey Senkron (VSync)", SettingsRow.Kind.VSync),
    };

    public static SettingsMenu Build(RectTransform parent, UITheme theme, CanvasGroup hideWhenOpen, Selectable returnSelection)
    {
        var root = UIBuildUtil.CreateRect("Settings", parent);
        UIBuildUtil.Stretch(root);
        var menu = root.gameObject.AddComponent<SettingsMenu>();

        var panelRT = UIBuildUtil.CreateRect("Panel", root);
        UIBuildUtil.Stretch(panelRT);
        var panelGroup = panelRT.gameObject.AddComponent<CanvasGroup>();

        var bg = UIBuildUtil.CreateThemedImage("Background", panelRT, theme, UITheme.Slot.PausePanel, hideIfEmpty: true);
        var brt = bg.rectTransform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(1040f, 900f);

        var title = UIBuildUtil.CreateThemedText("Title", panelRT, theme, ThemedText.Role.Title, "AYARLAR", 72);
        var trt = title.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(900f, 100f);
        trt.anchoredPosition = new Vector2(0f, 330f);
        var tOutline = title.gameObject.AddComponent<Outline>();
        tOutline.effectColor = new Color(0.25f, 0.03f, 0.02f, 0.95f);
        tOutline.effectDistance = new Vector2(3f, -3f);

        var rows = UIBuildUtil.CreateVerticalList("Rows", panelRT, new Vector2(880f, 500f), new Vector2(0f, 15f), 4f);
        var selectables = new System.Collections.Generic.List<Selectable>();

        AddHeader(rows, theme, "SES");
        foreach (var (label, kind) in AudioRows) selectables.Add(CreateRow(rows, theme, label, kind));
        AddHeader(rows, theme, "GÖRÜNTÜ");
        foreach (var (label, kind) in DisplayRows) selectables.Add(CreateRow(rows, theme, label, kind));

        var backList = UIBuildUtil.CreateVerticalList("Back", panelRT, new Vector2(600f, 90f), new Vector2(0f, -300f), 0f);
        var back = UIBuildUtil.CreateMenuButton(backList, "GERI_Button", "GERİ", theme, 50);
        UnityEventTools.AddPersistentListener(back.onClick, menu.Close);
        selectables.Add(back);

        // Yukari/asagi dongusel gezinme (sol/sag satirlarin degerini degistirir)
        for (int i = 0; i < selectables.Count; i++)
        {
            selectables[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = selectables[(i - 1 + selectables.Count) % selectables.Count],
                selectOnDown = selectables[(i + 1) % selectables.Count]
            };
        }

        var hint = UIBuildUtil.CreateThemedText("Hint", panelRT, theme, ThemedText.Role.Hint,
            "‹ / ›  ya da  A / D  ile değiştir     ·     ESC  ile geri", 24);
        var hrt = hint.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = new Vector2(1000f, 40f);
        hrt.anchoredPosition = new Vector2(0f, -368f);

        var so = new SerializedObject(menu);
        so.FindProperty("panel").objectReferenceValue = panelGroup;
        so.FindProperty("firstSelected").objectReferenceValue = selectables[0];
        so.FindProperty("hideWhenOpen").objectReferenceValue = hideWhenOpen;
        so.FindProperty("returnSelection").objectReferenceValue = returnSelection;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Editorde kapali dursun
        panelGroup.alpha = 0f;
        panelRT.gameObject.SetActive(false);
        return menu;
    }

    private static void AddHeader(RectTransform parent, UITheme theme, string text)
    {
        var t = UIBuildUtil.CreateThemedText("Header_" + text, parent, theme, ThemedText.Role.Hint, text, 26);
        t.alignment = TextAnchor.LowerLeft;
        var rt = t.rectTransform;
        rt.sizeDelta = new Vector2(880f, 46f);
        // Basligin altinda ince cizgi
        var line = UIBuildUtil.CreateImage("Line", rt, null, new Color(theme.decoColor.r, theme.decoColor.g, theme.decoColor.b, 0.35f));
        line.raycastTarget = false;
        var lrt = line.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 0f);
        lrt.sizeDelta = new Vector2(0f, 2f);
        lrt.anchoredPosition = new Vector2(0f, -2f);
        // metni biraz iceri al
        t.text = "   " + text;
    }

    private static SettingsRow CreateRow(RectTransform parent, UITheme theme, string labelText, SettingsRow.Kind kind)
    {
        var rt = UIBuildUtil.CreateRect("Row_" + kind, parent);
        rt.sizeDelta = new Vector2(880f, 62f);
        var hit = rt.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        var row = rt.gameObject.AddComponent<SettingsRow>();
        row.targetGraphic = hit;

        var label = UIBuildUtil.CreateText("Label", rt, theme.buttonFont, labelText, 40, theme.buttonNormal);
        label.alignment = TextAnchor.MiddleLeft;
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0.55f, 1f);
        lrt.offsetMin = new Vector2(30f, 0f);
        lrt.offsetMax = Vector2.zero;
        AddShadow(label);

        var value = UIBuildUtil.CreateText("Value", rt, theme.buttonFont, "‹    %100    ›", 38, theme.buttonNormal);
        var vrt = value.rectTransform;
        vrt.anchorMin = new Vector2(0.55f, 0f);
        vrt.anchorMax = new Vector2(1f, 1f);
        vrt.offsetMin = new Vector2(0f, 8f);
        vrt.offsetMax = new Vector2(-20f, 0f);
        value.raycastTarget = true; // tiklama tarafini anlamak icin
        AddShadow(value);

        // Ses barlari (diger satirlarda kod gizler)
        var barRoot = UIBuildUtil.CreateRect("Bar", rt);
        barRoot.anchorMin = new Vector2(0.64f, 0f);
        barRoot.anchorMax = new Vector2(0.91f, 0f);
        barRoot.pivot = new Vector2(0.5f, 0f);
        barRoot.sizeDelta = new Vector2(0f, 6f);
        barRoot.anchoredPosition = new Vector2(0f, 6f);
        var barBack = UIBuildUtil.CreateImage("Back", barRoot, null, theme.hudBarBack);
        barBack.raycastTarget = false;
        UIBuildUtil.Stretch(barBack.rectTransform);
        var fill = UIBuildUtil.CreateImage("Fill", barRoot, null, theme.decoColor);
        fill.raycastTarget = false;
        var frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0.8f, 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;

        var so = new SerializedObject(row);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("valueText").objectReferenceValue = value;
        so.FindProperty("barRoot").objectReferenceValue = barRoot;
        so.FindProperty("barFill").objectReferenceValue = frt;
        so.ApplyModifiedPropertiesWithoutUndo();
        return row;
    }

    private static void AddShadow(Text t)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.8f);
        s.effectDistance = new Vector2(2f, -2f);
    }
}
