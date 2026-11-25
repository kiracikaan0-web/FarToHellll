using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Ana menu sahnesini sifirdan kurar.
/// Unity menusu: Tools > FarToHell > Ana Menu Kur
///
///  - Assets/Scenes/MainMenu.unity sahnesini olusturur (varsa sorup yeniden kurar)
///  - Hareketli cehennem arka plani, baslik logosu, BASLA / CIKIS butonlari, karartma gecisi
///  - Gorseller, fontlar ve renkler UI Theme'den gelir: Assets/UI/Resources/UITheme.asset
///  - Build Settings'i ayarlar: 0 = MainMenu, 1 = oyun sahnesi (circle)
/// </summary>
public static class MainMenuSetup
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameScenePath = "Assets/circle.unity";

    [MenuItem("Tools/FarToHell/Ana Menu Kur")]
    public static void Setup()
    {
        var theme = UIBuildUtil.GetOrCreateTheme();
        if (theme.menuTitle == null || theme.menuVignette == null || theme.buttonFont == null)
        {
            EditorUtility.DisplayDialog("Ana Menu Kur",
                "UI Theme'de baslik, vignette veya font eksik:\n" + UIBuildUtil.ThemePath, "Tamam");
            Selection.activeObject = theme;
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (System.IO.File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Ana Menu Kur", ScenePath + " zaten var. Bastan kurulsun mu?", "Evet", "Vazgec"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Kamera ---
        var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 8f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(20, 4, 10, 255);
        camGO.AddComponent<AudioListener>();
        camGO.AddComponent<MenuCameraDrift>();

        // --- Arka plan (oyundakiyle ayni katmanlar) ---
        if (BackgroundSetup.BuildBackground(cam) == null) return;

        // --- EventSystem (yeni Input System ile) ---
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        // --- Canvas ---
        var canvas = UIBuildUtil.CreateCanvas("Canvas", 0);
        var canvasRT = (RectTransform)canvas.transform;
        var menu = canvas.gameObject.AddComponent<MainMenu>();

        // Kenar karartmasi (yazilar arka planda kaybolmasin)
        var vig = UIBuildUtil.CreateThemedImage("Vignette", canvasRT, theme, UITheme.Slot.MenuVignette, hideIfEmpty: true);
        UIBuildUtil.Stretch(vig.rectTransform);

        // Baslik
        var titleImg = UIBuildUtil.CreateThemedImage("Title", canvasRT, theme, UITheme.Slot.MenuTitle);
        titleImg.preserveAspect = true;
        var trt = titleImg.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 1f);
        float titleW = 1250f;
        trt.sizeDelta = new Vector2(titleW, titleW * theme.menuTitle.rect.height / theme.menuTitle.rect.width);
        trt.anchoredPosition = new Vector2(0f, -110f);
        titleImg.gameObject.AddComponent<UIPulse>();

        // Butonlar
        var list = UIBuildUtil.CreateVerticalList("Buttons", canvasRT, new Vector2(600f, 260f), new Vector2(0f, -150f), 18f);
        var startBtn = UIBuildUtil.CreateMenuButton(list, "BASLA_Button", "BAŞLA", theme);
        var quitBtn = UIBuildUtil.CreateMenuButton(list, "CIKIS_Button", "ÇIKIŞ", theme);
        UnityEventTools.AddPersistentListener(startBtn.onClick, menu.StartGame);
        UnityEventTools.AddPersistentListener(quitBtn.onClick, menu.QuitGame);
        UIBuildUtil.LinkVertical(startBtn, quitBtn);

        // Alt bilgi
        var hint = UIBuildUtil.CreateThemedText("Hint", canvasRT, theme, ThemedText.Role.Hint,
            "W / S  ·  ↑ / ↓  ile seç     ·     Enter  ile onayla", 26);
        var hrt = hint.rectTransform;
        hrt.anchorMin = hrt.anchorMax = hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(1400f, 50f);
        hrt.anchoredPosition = new Vector2(0f, 40f);

        var version = UIBuildUtil.CreateThemedText("Version", canvasRT, theme, ThemedText.Role.Hint, "v0.1", 24);
        version.alignment = TextAnchor.LowerRight;
        var vrt = version.rectTransform;
        vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(1f, 0f);
        vrt.sizeDelta = new Vector2(300f, 50f);
        vrt.anchoredPosition = new Vector2(-30f, 30f);

        // Karartma paneli (en ustte)
        var fadeImg = UIBuildUtil.CreateImage("Fader", canvasRT, null, Color.black);
        UIBuildUtil.Stretch(fadeImg.rectTransform);
        var fader = fadeImg.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 0f; // editorde menu gorunsun; oyunda MainMenu acilista karartip acar
        fader.blocksRaycasts = false;

        var so = new SerializedObject(menu);
        so.FindProperty("gameSceneName").stringValue = System.IO.Path.GetFileNameWithoutExtension(GameScenePath);
        so.FindProperty("firstSelected").objectReferenceValue = startBtn;
        so.FindProperty("fader").objectReferenceValue = fader;
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- Kaydet ve Build Settings ---
        UIBuildUtil.EnsureFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        UpdateBuildSettings();

        Selection.activeGameObject = canvas.gameObject;
        EditorUtility.DisplayDialog("Ana Menu Kur",
            "Ana menu hazir: " + ScenePath + "\n\n" +
            "Build Settings: 0 = MainMenu, 1 = " + System.IO.Path.GetFileNameWithoutExtension(GameScenePath) + "\n\n" +
            "Play'e basip dene. BASLA oyuna gecer, CIKIS Play modunu kapatir (derlenmis oyunda oyundan cikar).\n\n" +
            "Gorselleri degistirmek icin: " + UIBuildUtil.ThemePath,
            "Tamam");
    }

    private static void UpdateBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        if (System.IO.File.Exists(GameScenePath))
            scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));
        foreach (var s in EditorBuildSettings.scenes)
            if (scenes.All(x => x.path != s.path))
                scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
