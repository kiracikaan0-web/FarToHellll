using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gri, firtinali arka plani acik sahneye kurar (tilemap'in arkasina, parallax ile).
/// Tools > FarToHell > Gri Arka Plan Kur
///  - Assets/Sprites/BackgroundGray altindaki 4 katmanin import ayarlarini yapar
///  - Sahnedeki eski "Background" objesini kaldirip yenisini kurar (Ctrl+Z ile geri alinir)
/// </summary>
public static class GrayBackgroundSetup
{
    private const string Folder = "Assets/Sprites/BackgroundGray/";

    // dosya adi, parallax (1 = kameraya yapisik), siralama (kucuk = daha arkada)
    private static readonly (string file, float parallax, int order)[] Layers =
    {
        ("BG_Gray_0_Sky",  1.00f, -100),
        ("BG_Gray_1_Far",  0.85f,  -90),
        ("BG_Gray_2_Mid",  0.65f,  -80),
        ("BG_Gray_3_Near", 0.45f,  -70),
    };

    [MenuItem("Tools/FarToHell/Gri Arka Plan Kur")]
    public static void Setup()
    {
        var cam = Camera.main;
        if (cam == null) { EditorUtility.DisplayDialog("Gri Arka Plan", "Sahnede 'MainCamera' etiketli kamera yok.", "Tamam"); return; }

        var sprites = new Sprite[Layers.Length];
        for (int i = 0; i < Layers.Length; i++)
        {
            string path = Folder + Layers[i].file + ".png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { EditorUtility.DisplayDialog("Gri Arka Plan", "Bulunamadi: " + path, "Tamam"); return; }
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100;
            imp.mipmapEnabled = true;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        Undo.SetCurrentGroupName("Gri Arka Plan Kur");
        int group = Undo.GetCurrentGroup();

        var old = GameObject.Find("Background");
        if (old != null) Undo.DestroyObjectImmediate(old);

        Undo.RecordObject(cam, "Kamera");
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(46, 47, 52, 255);

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
            so.ApplyModifiedPropertiesWithoutUndo();
            float scale = 2f * cam.orthographicSize * 1.15f / sprites[i].bounds.size.y;   // editorde de dogru boyutta gorunsun
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog("Gri Arka Plan", "Gri arka plan tilemap'in arkasina kuruldu.\nPlay'de karakter yurudukce katmanlar farkli hizlarda kayar.\n\nKaydetmek icin Ctrl+S. Geri almak icin Ctrl+Z.", "Tamam");
    }
}
