using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ESC ile acilan duraklatma menusunu (Devam Et, Ayarlar, Ana Menu, Cikis) prefab olarak olusturur.
/// Unity menusu: Tools > FarToHell > Duraklatma Menusu Kur
///
///  - Assets/UI/Resources/PauseMenu.prefab (varsa sorup yeniden kurar)
///  - Sahnelere eklemeye gerek yok: oyun basladiginda Player olan her sahneye otomatik gelir.
///    (Istersen prefabi bir sahneye elle de koyabilirsin; o zaman ikinci bir tane eklenmez.)
/// </summary>
public static class PauseMenuSetup
{
    private const string PrefabPath = "Assets/UI/Resources/PauseMenu.prefab";
    private const string OldPrefabPath = "Assets/Prefabs/UI/PauseMenu.prefab";

    [MenuItem("Tools/FarToHell/Duraklatma Menusu Kur")]
    public static void Setup()
    {
        var theme = UIBuildUtil.GetOrCreateTheme();
        if (theme.buttonFont == null)
        {
            EditorUtility.DisplayDialog("Duraklatma Menusu", "UI Theme'de buton fontu yok: " + UIBuildUtil.ThemePath, "Tamam");
            return;
        }
        if (theme.pausePanel == null)
        {
            // Tema eski surumden kaldiysa paneli ekle
            theme.pausePanel = UIBuildUtil.ImportUISprite(UIBuildUtil.UIFolder + "PausePanel.png");
            EditorUtility.SetDirty(theme);
        }

        // Eski surumdeki yerinden Resources'a tasi (GUID korunur)
        if (System.IO.File.Exists(OldPrefabPath) && !System.IO.File.Exists(PrefabPath))
        {
            UIBuildUtil.EnsureFolder("Assets", "UI");
            UIBuildUtil.EnsureFolder("Assets/UI", "Resources");
            AssetDatabase.MoveAsset(OldPrefabPath, PrefabPath);
        }

        if (System.IO.File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("Duraklatma Menusu", PrefabPath + " zaten var. Bastan kurulsun mu?\n(Prefabda elle yaptigin degisiklikler silinir.)", "Evet", "Vazgec"))
            return;

        var go = BuildPauseMenu(theme);

        UIBuildUtil.EnsureFolder("Assets", "UI");
        UIBuildUtil.EnsureFolder("Assets/UI", "Resources");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        Selection.activeObject = prefab;

        EditorUtility.DisplayDialog("Duraklatma Menusu",
            "Hazir: " + PrefabPath + "\n\n" +
            "Sahnelere eklemene gerek yok: Player olan her sahnede oyun baslayinca otomatik gelir.\n" +
            "Play'e basip ESC ile dene.\n\n" +
            "Gorselleri degistirmek icin: " + UIBuildUtil.ThemePath, "Tamam");
    }

    private static GameObject BuildPauseMenu(UITheme theme)
    {
        var canvas = UIBuildUtil.CreateCanvas("PauseMenu", 100);
        var canvasRT = (RectTransform)canvas.transform;
        var menu = canvas.gameObject.AddComponent<PauseMenu>();

        // Acilip kapanan kisim (PauseMenu objesi hep aktif kalir ki ESC dinlensin)
        var root = UIBuildUtil.CreateRect("Root", canvasRT);
        UIBuildUtil.Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();

        // Ekrani karartan perde (tiklamalari da oyuna gecirmez)
        var overlay = UIBuildUtil.CreateImage("Overlay", root, null, theme.pauseOverlay);
        UIBuildUtil.Stretch(overlay.rectTransform);
        overlay.raycastTarget = true;

        // Panel
        var panel = UIBuildUtil.CreateThemedImage("Panel", root, theme, UITheme.Slot.PausePanel, hideIfEmpty: true);
        var prt = panel.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(820f, 760f);
        var panelGroup = panel.gameObject.AddComponent<CanvasGroup>(); // ayarlar acilinca gizlenir

        // Baslik: temada gorsel varsa o, yoksa yazi
        var titleText = UIBuildUtil.CreateThemedText("TitleText", prt, theme, ThemedText.Role.Title, "DURAKLATILDI", 76);
        var ttr = titleText.rectTransform;
        ttr.anchorMin = ttr.anchorMax = new Vector2(0.5f, 1f);
        ttr.sizeDelta = new Vector2(760f, 110f);
        ttr.anchoredPosition = new Vector2(0f, -150f);
        var tOutline = titleText.gameObject.AddComponent<Outline>();
        tOutline.effectColor = new Color(0.25f, 0.03f, 0.02f, 0.95f);
        tOutline.effectDistance = new Vector2(3f, -3f);
        var tShadow = titleText.gameObject.AddComponent<Shadow>();
        tShadow.effectColor = new Color(1f, 0.35f, 0.1f, 0.35f);
        tShadow.effectDistance = new Vector2(0f, -6f);

        var titleImg = UIBuildUtil.CreateThemedImage("TitleImage", prt, theme, UITheme.Slot.PauseTitle,
            hideIfEmpty: true, showWhenEmpty: titleText.gameObject);
        titleImg.preserveAspect = true;
        var tir = titleImg.rectTransform;
        tir.anchorMin = tir.anchorMax = new Vector2(0.5f, 1f);
        tir.sizeDelta = new Vector2(700f, 140f);
        tir.anchoredPosition = new Vector2(0f, -150f);

        // Butonlar
        var list = UIBuildUtil.CreateVerticalList("Buttons", prt, new Vector2(600f, 370f), new Vector2(0f, -45f), 6f);
        var resume = UIBuildUtil.CreateMenuButton(list, "DevamEt_Button", "DEVAM ET", theme, 54);
        var settingsBtn = UIBuildUtil.CreateMenuButton(list, "Ayarlar_Button", "AYARLAR", theme, 54);
        var toMenu = UIBuildUtil.CreateMenuButton(list, "AnaMenu_Button", "ANA MENÜ", theme, 54);
        var quit = UIBuildUtil.CreateMenuButton(list, "Cikis_Button", "ÇIKIŞ", theme, 54);
        UnityEventTools.AddPersistentListener(resume.onClick, menu.Resume);
        UnityEventTools.AddPersistentListener(toMenu.onClick, menu.GoToMainMenu);
        UnityEventTools.AddPersistentListener(quit.onClick, menu.QuitGame);
        UIBuildUtil.LinkVertical(resume, settingsBtn, toMenu, quit);

        // Alt bilgi
        var hint = UIBuildUtil.CreateThemedText("Hint", prt, theme, ThemedText.Role.Hint, "ESC  ile devam et", 26);
        var hrt = hint.rectTransform;
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(700f, 40f);
        hrt.anchoredPosition = new Vector2(0f, 95f);

        // Ayarlar paneli (ana menudekiyle ayni)
        var settings = SettingsMenuBuilder.Build(root, theme, panelGroup, settingsBtn);
        UnityEventTools.AddPersistentListener(settingsBtn.onClick, settings.Open);

        var so = new SerializedObject(menu);
        so.FindProperty("root").objectReferenceValue = group;
        so.FindProperty("firstSelected").objectReferenceValue = resume;
        so.FindProperty("settings").objectReferenceValue = settings;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Editorde kapali dursun ki sahneyi kapatmasin
        root.gameObject.SetActive(false);
        return canvas.gameObject;
    }
}
