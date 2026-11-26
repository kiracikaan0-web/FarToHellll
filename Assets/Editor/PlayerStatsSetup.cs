using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Can ve stamina sistemini kurar.
/// Unity menusu:
///   Tools > FarToHell > Can ve Stamina Kur
///     - Assets/UI/Resources/PlayerHUD.prefab (can/stamina barlari; Player olan her sahneye otomatik gelir)
///     - Player'a Health ve PlayerStamina ekler (Inspector'dan ayarlayabilmen icin) ve sahneyi kaydeder
///   Tools > FarToHell > Test Hasar Bolgesi Ekle
///     - Oyuncunun yanina kirmizi bir hasar kutusu koyar (hasar almayi denemek icin)
/// Player'in oldugu sahne acik degilse Assets/Scenes/Map1.unity'yi kendisi acar.
/// </summary>
public static class PlayerStatsSetup
{
    private const string HudPrefabPath = "Assets/UI/Resources/PlayerHUD.prefab";
    private const string GameScenePath = "Assets/Scenes/Map1.unity";

    [MenuItem("Tools/FarToHell/Can ve Stamina Kur")]
    public static void Setup()
    {
        var theme = UIBuildUtil.GetOrCreateTheme();

        // --- HUD prefab ---
        bool buildHud = true;
        if (System.IO.File.Exists(HudPrefabPath))
            buildHud = EditorUtility.DisplayDialog("Can ve Stamina", HudPrefabPath + " zaten var. HUD bastan kurulsun mu?",
                "Evet", "Hayir, sadece oyuncuyu ayarla");
        if (buildHud)
        {
            var hud = BuildHud(theme);
            UIBuildUtil.EnsureFolder("Assets", "UI");
            UIBuildUtil.EnsureFolder("Assets/UI", "Resources");
            PrefabUtility.SaveAsPrefabAsset(hud, HudPrefabPath);
            Object.DestroyImmediate(hud);
        }

        // --- Oyuncu ---
        var player = FindPlayerOpeningSceneIfNeeded();
        if (player == null) return;

        var go = player.gameObject;
        if (go.GetComponent<Health>() == null) Undo.AddComponent<Health>(go);
        if (go.GetComponent<PlayerStamina>() == null) Undo.AddComponent<PlayerStamina>(go);
        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveScene(go.scene);
        Selection.activeGameObject = go;

        EditorUtility.DisplayDialog("Can ve Stamina",
            "Hazir.\n\n" +
            "'" + go.name + "' objesine Health ve PlayerStamina eklendi, sahne (" + go.scene.name + ") kaydedildi.\n" +
            "Degerleri Player'in Inspector'unda ayarlayabilirsin:\n" +
            "  - Health: maksimum can, vurulduktan sonraki hasarsizlik suresi\n" +
            "  - PlayerStamina: maksimum, dolma hizi, bekleme\n" +
            "  - PlayerMovement: dash maliyeti, geri itilme, olum\n" +
            "  - PlayerCombat: saldiri maliyeti ve hasari\n\n" +
            "Hasar almayi denemek icin: Tools > FarToHell > Test Hasar Bolgesi Ekle", "Tamam");
    }

    [MenuItem("Tools/FarToHell/Test Hasar Bolgesi Ekle")]
    public static void AddTestZone()
    {
        var player = FindPlayerOpeningSceneIfNeeded();
        if (player == null) return;

        var zone = new GameObject("TestDamageZone");
        Undo.RegisterCreatedObjectUndo(zone, "Test Hasar Bolgesi");
        SceneManager_MoveToScene(zone, player.gameObject.scene);
        zone.transform.position = player.transform.position + new Vector3(5f, 0.75f, 0f);
        zone.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

        var sr = zone.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        sr.color = new Color(1f, 0.2f, 0.1f, 0.55f);
        sr.sortingOrder = 5;

        var box = zone.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
        zone.AddComponent<DamageZone>();

        EditorSceneManager.MarkSceneDirty(zone.scene);
        EditorSceneManager.SaveScene(zone.scene);
        Selection.activeGameObject = zone;
        EditorUtility.DisplayDialog("Test Hasar Bolgesi",
            "Oyuncunun 5 birim sagina kirmizi bir kutu eklendi ve sahne kaydedildi.\n" +
            "Play'e basip icine yuru. Isin bitince objeyi silebilirsin.\n\n" +
            "Hasar ve itme gucu DamageZone bileseninden ayarlanir.", "Tamam");
    }

    private static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    /// <summary>Acik sahnede Player yoksa oyun sahnesini acar.</summary>
    internal static PlayerMovement FindPlayerOpeningSceneIfNeeded()
    {
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player != null) return player;

        if (!System.IO.File.Exists(GameScenePath))
        {
            EditorUtility.DisplayDialog("FarToHell", "Acik sahnede Player yok ve " + GameScenePath + " bulunamadi.", "Tamam");
            return null;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player == null)
            EditorUtility.DisplayDialog("FarToHell", GameScenePath + " icinde PlayerMovement olan obje yok.", "Tamam");
        return player;
    }

    // ---------------- HUD ----------------

    private static GameObject BuildHud(UITheme theme)
    {
        var canvas = UIBuildUtil.CreateCanvas("PlayerHUD", 50);
        var canvasRT = (RectTransform)canvas.transform;
        var hud = canvas.gameObject.AddComponent<PlayerHUD>();
        // HUD tiklamalari engellemesin
        Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());

        var bars = UIBuildUtil.CreateRect("Bars", canvasRT);
        bars.anchorMin = bars.anchorMax = bars.pivot = new Vector2(0f, 1f);
        bars.anchoredPosition = new Vector2(48f, -44f);
        bars.sizeDelta = new Vector2(1200f, 80f);

        var hp = CreateBar("HealthBar", bars, theme, 400f, 22f, 0f, true,
            out var hpBack, out var hpFill, out var hpChip);
        var st = CreateBar("StaminaBar", bars, theme, 320f, 13f, -34f, false,
            out var stBack, out var stFill, out _);

        var so = new SerializedObject(hud);
        so.FindProperty("healthBar").objectReferenceValue = hp;
        so.FindProperty("healthFill").objectReferenceValue = hpFill;
        so.FindProperty("healthChip").objectReferenceValue = hpChip;
        so.FindProperty("staminaBar").objectReferenceValue = st;
        so.FindProperty("staminaFill").objectReferenceValue = stFill;
        var backs = so.FindProperty("barBacks");
        backs.arraySize = 2;
        backs.GetArrayElementAtIndex(0).objectReferenceValue = hpBack;
        backs.GetArrayElementAtIndex(1).objectReferenceValue = stBack;
        so.ApplyModifiedPropertiesWithoutUndo();
        hud.ApplyTheme();
        return canvas.gameObject;
    }

    private static RectTransform CreateBar(string name, RectTransform parent, UITheme theme, float width, float height,
        float y, bool withChip, out Image back, out RectTransform fill, out RectTransform chip)
    {
        var bar = UIBuildUtil.CreateRect(name, parent);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 1f);
        bar.anchoredPosition = new Vector2(0f, y);
        bar.sizeDelta = new Vector2(width, height);

        back = UIBuildUtil.CreateImage("Back", bar, null, theme.hudBarBack);
        UIBuildUtil.Stretch(back.rectTransform);
        back.raycastTarget = false;
        var outline = back.gameObject.AddComponent<Outline>();
        outline.effectColor = theme.hudBarBorder;
        outline.effectDistance = new Vector2(2f, 2f);

        chip = null;
        if (withChip)
        {
            var chipImg = UIBuildUtil.CreateImage("Chip", bar, null, theme.hudHealthChip);
            chipImg.raycastTarget = false;
            chip = chipImg.rectTransform;
            FillRect(chip);
        }

        var fillImg = UIBuildUtil.CreateImage("Fill", bar, null, withChip ? theme.hudHealth : theme.hudStamina);
        fillImg.raycastTarget = false;
        fill = fillImg.rectTransform;
        FillRect(fill);

        // Ustte hafif parlaklik (duz renk sikici durmasin)
        var shine = UIBuildUtil.CreateImage("Shine", fill, null, new Color(1f, 1f, 1f, 0.12f));
        shine.raycastTarget = false;
        var srt = shine.rectTransform;
        srt.anchorMin = new Vector2(0f, 0.55f);
        srt.anchorMax = Vector2.one;
        srt.offsetMin = srt.offsetMax = Vector2.zero;

        // Tasarimci cercevesi (temada yoksa gizli)
        var frame = UIBuildUtil.CreateThemedImage("Frame", bar, theme, UITheme.Slot.HudBarFrame, hideIfEmpty: true);
        var frt = frame.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(-6f, -6f);
        frt.offsetMax = new Vector2(6f, 6f);
        return bar;
    }

    private static void FillRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
