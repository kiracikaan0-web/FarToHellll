using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cehennem arka planini acik sahneye kurar.
/// Unity menusu: Tools > FarToHell > Arka Plan Kur
///
///  - Assets/Sprites/Background altindaki 4 katmanin import ayarlarini yapar (yuksek kalite)
///  - Sahnede "Background" objesi olusturur, katmanlari parallax ile ekler (eskisi varsa yenisiyle degisir)
///  - Kamera arka plan rengini koyu yapar
///  - Istersen kamerayi yaklastirir (Size 8) ve oyuncuyu takip ettirir (CameraFollow)
/// Ctrl+Z ile geri alinabilir. Ana menu kurulumu da BuildBackground() fonksiyonunu kullanir.
/// </summary>
public static class BackgroundSetup
{
    private const string Folder = "Assets/Sprites/Background/";

    // dosya adi, parallax, siralama (kucuk = daha arkada), dikey kaydirma
    private static readonly (string file, float parallax, int order, float yOffset)[] Layers =
    {
        ("BG_0_Sky",          1.00f, -100, 0f),
        ("BG_1_FarMountains", 0.85f,  -90, 0f),
        ("BG_2_Spires",       0.65f,  -80, 0f),
        ("BG_3_NearRocks",    0.40f,  -70, -0.05f),
    };

    [MenuItem("Tools/FarToHell/Arka Plan Kur")]
    public static void Setup()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Arka Plan Kur", "Sahnede 'MainCamera' etiketli kamera yok.", "Tamam");
            return;
        }

        Undo.SetCurrentGroupName("Arka Plan Kur");
        int group = Undo.GetCurrentGroup();

        bool follow = EditorUtility.DisplayDialog("Arka Plan Kur",
            "Kamera yaklastirilsin (Size " + cam.orthographicSize + " -> 8) ve oyuncuyu takip etsin mi?\n\n" +
            "Karakter ekranda daha buyuk ve net gorunur, arka plan da hareket ettikce parallax ile kayar.",
            "Evet", "Hayir, kamerayi elleme");
        if (follow)
        {
            Undo.RecordObject(cam, "Kamera");
            cam.orthographicSize = 8f;
            var player = Object.FindAnyObjectByType<PlayerMovement>();
            if (cam.GetComponent<CameraFollow>() == null)
                Undo.AddComponent<CameraFollow>(cam.gameObject);
            if (player != null)
            {
                Undo.RecordObject(cam.transform, "Kamera");
                var p = player.transform.position;
                cam.transform.position = new Vector3(p.x, p.y + 2f, cam.transform.position.z);
            }
        }

        var root = BuildBackground(cam);
        if (root == null) return;

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog("Arka Plan Kur",
            "Arka plan kuruldu. Play'e basip karakterle yuru, katmanlar farkli hizlarda kayacak.\n\n" +
            "Sahneyi kaydetmeyi unutma (Ctrl+S).", "Tamam");
    }

    /// <summary>Katmanlari import edip verilen kameraya gore "Background" objesini olusturur. Hata olursa null doner.</summary>
    public static GameObject BuildBackground(Camera cam)
    {
        // --- Import ayarlari ---
        var sprites = new Sprite[Layers.Length];
        for (int i = 0; i < Layers.Length; i++)
        {
            string path = Folder + Layers[i].file + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                EditorUtility.DisplayDialog("Arka Plan", "Bulunamadi: " + path, "Tamam");
                return null;
            }
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !importer.mipmapEnabled ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = true;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // --- Eski arka plani kaldir ---
        var old = GameObject.Find("Background");
        if (old != null) Undo.DestroyObjectImmediate(old);

        // --- Kamera rengi ---
        Undo.RecordObject(cam, "Kamera");
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(20, 4, 10, 255);

        // --- Katmanlar ---
        var root = new GameObject("Background");
        Undo.RegisterCreatedObjectUndo(root, "Background");
        root.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 10f);

        for (int i = 0; i < Layers.Length; i++)
        {
            var go = new GameObject(Layers[i].file);
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprites[i];
            sr.sortingOrder = Layers[i].order;

            var layer = go.AddComponent<ParallaxLayer>();
            var so = new SerializedObject(layer);
            so.FindProperty("parallax").floatValue = Layers[i].parallax;
            so.FindProperty("verticalOffset").floatValue = Layers[i].yOffset;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Editorde de dogru boyutta gorunsun
            float scale = 2f * cam.orthographicSize * 1.15f / sprites[i].bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }
        return root;
    }
}
