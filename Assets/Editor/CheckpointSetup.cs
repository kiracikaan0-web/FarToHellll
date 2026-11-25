using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Olum ekrani ve kontrol noktasi kurulumu.
/// Unity menusu:
///   Tools > FarToHell > Olum Ekrani Kur
///     - Assets/UI/Resources/DeathScreen.prefab (Player olan her sahneye otomatik gelir)
///   Tools > FarToHell > Kontrol Noktasi Ekle
///     - Assets/Prefabs/Checkpoint.prefab (yoksa olusturur; olum ekrani yoksa onu da kurar)
///     - Oyuncunun 3 birim soluna bir kontrol noktasi koyar ve sahneyi kaydeder
/// Baska yerlere koymak icin prefabi sahneye surukle.
/// </summary>
public static class CheckpointSetup
{
    private const string DeathPrefabPath = "Assets/UI/Resources/DeathScreen.prefab";
    private const string CheckpointPrefabPath = "Assets/Prefabs/Checkpoint.prefab";
    private const string ArtFolder = "Assets/Sprites/Checkpoint/";

    // ---------------- Olum ekrani ----------------

    [MenuItem("Tools/FarToHell/Olum Ekrani Kur")]
    public static void SetupDeathScreen()
    {
        if (System.IO.File.Exists(DeathPrefabPath) &&
            !EditorUtility.DisplayDialog("Olum Ekrani", DeathPrefabPath + " zaten var. Bastan kurulsun mu?", "Evet", "Vazgec"))
            return;
        BuildDeathScreenPrefab();
        EditorUtility.DisplayDialog("Olum Ekrani",
            "Hazir: " + DeathPrefabPath + "\n\nPlayer olan her sahneye oyun basinda otomatik eklenir.\n" +
            "Denemek icin oyunda olmen yeterli (Test Hasar Bolgesi ya da Skeleton2).\n\n" +
            "Renkler ve istege bagli 'OLDUN' gorseli: " + UIBuildUtil.ThemePath, "Tamam");
    }

    private static void BuildDeathScreenPrefab()
    {
        var theme = UIBuildUtil.GetOrCreateTheme();
        var font = theme.titleFont != null ? theme.titleFont : theme.buttonFont;

        var canvas = UIBuildUtil.CreateCanvas("DeathScreen", 200); // HUD ve duraklatmanin ustunde
        Object.DestroyImmediate(canvas.GetComponent<GraphicRaycaster>());
        var crt = (RectTransform)canvas.transform;
        var ds = canvas.gameObject.AddComponent<DeathScreen>();

        // --- OLDUN ---
        var deathRoot = UIBuildUtil.CreateRect("Death", crt);
        UIBuildUtil.Stretch(deathRoot);
        var deathGroup = deathRoot.gameObject.AddComponent<CanvasGroup>();

        var dim = UIBuildUtil.CreateImage("Dim", deathRoot, null, new Color(0f, 0f, 0f, 0.35f));
        UIBuildUtil.Stretch(dim.rectTransform);
        dim.raycastTarget = false;

        var band = UIBuildUtil.CreateImage("Band", deathRoot, null, theme.deathBandColor);
        band.raycastTarget = false;
        var brt = band.rectTransform;
        brt.anchorMin = new Vector2(0f, 0.5f);
        brt.anchorMax = new Vector2(1f, 0.5f);
        brt.sizeDelta = new Vector2(0f, 220f);
        brt.anchoredPosition = Vector2.zero;

        var text = UIBuildUtil.CreateText("DeathText", deathRoot, font, "ÖLDÜN", 150, theme.deathTextColor);
        var trt = text.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(1600f, 220f);
        var glow = text.gameObject.AddComponent<Shadow>();
        glow.effectColor = new Color(0.4f, 0f, 0f, 0.6f);
        glow.effectDistance = new Vector2(0f, -6f);

        var img = UIBuildUtil.CreateImage("DeathImage", deathRoot, theme.deathTitle, Color.white);
        img.raycastTarget = false;
        img.preserveAspect = true;
        var irt = img.rectTransform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(1100f, 260f);
        img.enabled = theme.deathTitle != null;

        // --- ATES YAKILDI bandi ---
        var bannerRoot = UIBuildUtil.CreateRect("Banner", crt);
        UIBuildUtil.Stretch(bannerRoot);
        var bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        var bannerBand = UIBuildUtil.CreateImage("Band", bannerRoot, null, new Color(0f, 0f, 0f, 0.55f));
        bannerBand.raycastTarget = false;
        var bbrt = bannerBand.rectTransform;
        bbrt.anchorMin = new Vector2(0f, 0.5f);
        bbrt.anchorMax = new Vector2(1f, 0.5f);
        bbrt.sizeDelta = new Vector2(0f, 150f);
        bbrt.anchoredPosition = new Vector2(0f, 120f);
        var bannerText = UIBuildUtil.CreateText("BannerText", bannerRoot, font, "ATEŞ YAKILDI", 84, theme.bannerColor);
        var btr = bannerText.rectTransform;
        btr.anchorMin = btr.anchorMax = new Vector2(0.5f, 0.5f);
        btr.sizeDelta = new Vector2(1600f, 150f);
        btr.anchoredPosition = new Vector2(0f, 120f);
        var bOutline = bannerText.gameObject.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.25f, 0.06f, 0.02f, 0.9f);
        bOutline.effectDistance = new Vector2(2f, -2f);

        // --- Siyah gecis perdesi (en ustte) ---
        var fadeImg = UIBuildUtil.CreateImage("Fader", crt, null, Color.black);
        UIBuildUtil.Stretch(fadeImg.rectTransform);
        fadeImg.raycastTarget = false;
        var fader = fadeImg.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 0f;

        deathGroup.alpha = 0f;
        bannerGroup.alpha = 0f;

        var so = new SerializedObject(ds);
        so.FindProperty("deathGroup").objectReferenceValue = deathGroup;
        so.FindProperty("band").objectReferenceValue = band;
        so.FindProperty("deathText").objectReferenceValue = text;
        so.FindProperty("deathImage").objectReferenceValue = img;
        so.FindProperty("bannerGroup").objectReferenceValue = bannerGroup;
        so.FindProperty("bannerText").objectReferenceValue = bannerText;
        so.FindProperty("fader").objectReferenceValue = fader;
        so.ApplyModifiedPropertiesWithoutUndo();

        UIBuildUtil.EnsureFolder("Assets", "UI");
        UIBuildUtil.EnsureFolder("Assets/UI", "Resources");
        PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, DeathPrefabPath);
        Object.DestroyImmediate(canvas.gameObject);
    }

    // ---------------- Kontrol noktasi ----------------

    [MenuItem("Tools/FarToHell/Kontrol Noktasi Ekle")]
    public static void AddCheckpoint()
    {
        if (!System.IO.File.Exists(DeathPrefabPath)) BuildDeathScreenPrefab();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
        if (prefab == null)
        {
            prefab = BuildCheckpointPrefab();
            if (prefab == null) return;
        }

        var player = PlayerStatsSetup.FindPlayerOpeningSceneIfNeeded();
        if (player == null) return;

        var ground = ReadGroundLayer(player);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(instance, "Kontrol Noktasi");
        instance.transform.position = FindGroundPoint(player.transform.position + new Vector3(-3f, 0f, 0f), ground);
        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        Selection.activeGameObject = instance;

        EditorUtility.DisplayDialog("Kontrol Noktasi",
            "Oyuncunun 3 birim soluna bir kontrol noktasi eklendi ve sahne kaydedildi.\n\n" +
            "Oyunda yanina gelip E'ye bas:\n" +
            "  - ilk sefer: ates yanar, can dolar, dogma noktasi olur\n" +
            "  - sonra: dinlenirsin, dusmanlar yeniden canlanir\n" +
            "Olunce son yaktigin kontrol noktasinda dogarsin.\n\n" +
            "Daha fazlasi icin " + CheckpointPrefabPath + " dosyasini sahneye surukle.", "Tamam");
    }

    private static GameObject BuildCheckpointPrefab()
    {
        var altar = ImportSprite(ArtFolder + "CheckpointAltar.png", 140f, new Vector2(0.5f, 0f));
        var flameSprite = ImportSprite(ArtFolder + "CheckpointFlame.png", 140f, new Vector2(0.5f, 0.05f));
        var embersSprite = ImportSprite(ArtFolder + "CheckpointEmbers.png", 140f, new Vector2(0.5f, 0.1f));
        var glowSprite = ImportSprite(ArtFolder + "CheckpointGlow.png", 128f, new Vector2(0.5f, 0.5f));
        if (altar == null || flameSprite == null || embersSprite == null || glowSprite == null)
        {
            EditorUtility.DisplayDialog("Kontrol Noktasi", "Gorseller bulunamadi: " + ArtFolder, "Tamam");
            return null;
        }
        var theme = UIBuildUtil.GetOrCreateTheme();

        var root = new GameObject("Checkpoint");
        var cp = root.AddComponent<Checkpoint>();

        var glow = CreateSprite("Glow", root.transform, glowSprite, new Vector3(0f, 2.6f, 0f), -3);
        glow.color = new Color(1f, 1f, 1f, 0.8f);
        glow.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
        CreateSprite("Altar", root.transform, altar, Vector3.zero, -2);
        var flame = CreateSprite("Flame", root.transform, flameSprite, new Vector3(0f, 2.05f, 0f), -1);
        var embers = CreateSprite("Embers", root.transform, embersSprite, new Vector3(0f, 1.95f, 0f), -1);

        var spawn = new GameObject("SpawnPoint").transform;
        spawn.SetParent(root.transform, false);
        spawn.localPosition = new Vector3(1.8f, 0.05f, 0f);

        // Etkilesim yazisi (dunya uzayinda kucuk bir canvas)
        var promptGO = new GameObject("Prompt", typeof(RectTransform));
        promptGO.transform.SetParent(root.transform, false);
        var pc = promptGO.AddComponent<Canvas>();
        pc.renderMode = RenderMode.WorldSpace;
        pc.sortingOrder = 30;
        var prt = (RectTransform)promptGO.transform;
        prt.sizeDelta = new Vector2(600f, 100f);
        prt.localScale = Vector3.one * 0.01f; // 100 piksel = 1 birim
        prt.localPosition = new Vector3(0f, 4.6f, 0f);
        var bg = UIBuildUtil.CreateImage("Back", prt, null, new Color(0f, 0f, 0f, 0.6f));
        bg.raycastTarget = false;
        var bgr = bg.rectTransform;
        bgr.anchorMin = bgr.anchorMax = new Vector2(0.5f, 0.5f);
        bgr.sizeDelta = new Vector2(360f, 70f);
        var txt = UIBuildUtil.CreateText("Text", prt, theme.buttonFont, "E  ·  Ateşi yak", 40, theme.bannerColor);
        UIBuildUtil.Stretch(txt.rectTransform);

        var so = new SerializedObject(cp);
        so.FindProperty("spawnPoint").objectReferenceValue = spawn;
        so.FindProperty("flame").objectReferenceValue = flame.gameObject;
        so.FindProperty("embers").objectReferenceValue = embers.gameObject;
        so.FindProperty("glow").objectReferenceValue = glow;
        so.FindProperty("prompt").objectReferenceValue = promptGO;
        so.FindProperty("promptText").objectReferenceValue = txt;
        so.ApplyModifiedPropertiesWithoutUndo();

        UIBuildUtil.EnsureFolder("Assets", "Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, CheckpointPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------------- Yardimcilar ----------------

    private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, Vector3 pos, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private static Sprite ImportSprite(string path, float ppu, Vector2 pivot)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static LayerMask ReadGroundLayer(PlayerMovement player)
    {
        var so = new SerializedObject(player);
        var prop = so.FindProperty("groundLayer");
        int v = prop != null ? prop.intValue : 0;
        return v != 0 ? v : (1 << 6);
    }

    private static Vector3 FindGroundPoint(Vector3 around, LayerMask ground)
    {
        Physics2D.SyncTransforms();
        var hit = Physics2D.Raycast((Vector2)around + Vector2.up * 6f, Vector2.down, 30f, ground);
        return hit.collider != null ? (Vector3)hit.point : around;
    }
}
