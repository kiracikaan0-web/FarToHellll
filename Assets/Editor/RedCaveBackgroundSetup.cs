using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Oyundaki tum dunya arka planlarini kaldirir ve kirmizi-siyah magara resmini koyar.
/// Tools > FarToHell > Kirmizi Magara Arka Planini Kur
///  - Build'deki her sahne acilir: "Background" adli dunya objeleri (UI degil) ve ParallaxLayer'li tum katmanlar silinir
///  - Yerine kameraya kilitli, ekrani tam kaplayan tek bir arka plan konur (CameraFillBackground)
///  - Kamera arka plan rengi koyu yapilir; sahneler kaydedilir
/// UI'daki "Background" (duraklatma paneli resmi) gibi RectTransform'lu objelere dokunulmaz.
/// </summary>
public static class RedCaveBackgroundSetup
{
    private const string SpritePath = "Assets/Sprites/BackgroundRed/Cave_Red.png";

    [MenuItem("Tools/FarToHell/Kirmizi Magara Arka Planini Kur")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var imp = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
        if (imp == null) { EditorUtility.DisplayDialog("Arka Plan", "Resim bulunamadi: " + SpritePath, "Tamam"); return; }
        CaveRedImport.ApplySettings(imp);
        imp.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);

        string startScene = EditorSceneManager.GetActiveScene().path;
        var report = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (string.IsNullOrEmpty(s.path) || !System.IO.File.Exists(s.path)) continue;
            var scene = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            int removed = 0;

            // 1) ParallaxLayer'li tum katmanlar
            foreach (var p in Object.FindObjectsByType<ParallaxLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (p != null) { Object.DestroyImmediate(p.gameObject); removed++; }
            // 2) "Background" adli dunya objeleri (UI haric), eski arka plan dahil
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t == null || t is RectTransform) continue;
                if (t.name == "Background" || t.name == "RedCaveBackground") { Object.DestroyImmediate(t.gameObject); removed++; }
            }

            var cam = Camera.main;
            bool hasWorld = cam != null;
            if (hasWorld)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(6, 3, 4, 255);
                var go = new GameObject("RedCaveBackground");
                SceneManager_Move(go, scene);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = -200;                    // her seyin arkasinda
                go.AddComponent<CameraFillBackground>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add(System.IO.Path.GetFileNameWithoutExtension(s.path) + ": " + removed + " eski obje silindi" + (hasWorld ? ", yeni arka plan kondu" : ", kamera yok (atlandi)"));
        }
        if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Arka Plan", "Kirmizi magara arka plani kuruldu:\n\n" + string.Join("\n", report) +
            "\n\nArka plan kameraya kilitli ve ekrani tam kapliyor; her sahne kaydedildi.\nGeri almak icin git'ten sahneleri geri yukleyebilirsin.", "Tamam");
    }

    public const string CaveSpritePath = SpritePath;

    private static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }
}

/// <summary>
/// Magara arka plani her ice aktarildiginda tam cozunurlukte kalsin: Unity varsayilan olarak 2048 px'e kucultur.
/// Yuksek cozunurluklu resim (3470x1730) icin: 4096 sinir, sikistirma yok, yumusak (Bilinear) filtre.
/// </summary>
public class CaveRedImport : AssetPostprocessor
{
    public static void ApplySettings(TextureImporter imp)
    {
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100;
        // kucuk (piksel) resim: Point = keskin pikseller; buyuk resim: Bilinear = yumusak
        imp.GetSourceTextureWidthAndHeight(out int w, out int h);
        imp.filterMode = w < 1000 ? FilterMode.Point : FilterMode.Bilinear;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 4096;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.npotScale = TextureImporterNPOTScale.None;
    }

    private void OnPreprocessTexture()
    {
        if (assetPath == RedCaveBackgroundSetup.CaveSpritePath) ApplySettings((TextureImporter)assetImporter);
    }

    // resim, bu kural derlenmeden once ice aktarildiysa: derlemeden sonra ayarlari kontrol et, gerekirse duzelt
    [InitializeOnLoadMethod]
    private static void FixAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            var imp = AssetImporter.GetAtPath(RedCaveBackgroundSetup.CaveSpritePath) as TextureImporter;
            if (imp == null) return;
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            var wantFilter = w < 1000 ? FilterMode.Point : FilterMode.Bilinear;
            if (imp.maxTextureSize >= 4096 && imp.filterMode == wantFilter &&
                imp.textureCompression == TextureImporterCompression.Uncompressed) return;
            ApplySettings(imp);
            imp.SaveAndReimport();
            Debug.Log("[Arka Plan] Magara arka plani tam cozunurlukte (" + w + "x" + h + ") ice aktarildi.");
        };
    }
}
