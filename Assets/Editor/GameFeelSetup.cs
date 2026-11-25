using UnityEditor;
using UnityEngine;

/// <summary>
/// Vurus hissi ayarlari dosyasini olusturur/secer.
/// Unity menusu: Tools > FarToHell > Vurus Hissi Ayarlari
/// Dosya: Assets/Resources/GameFeelSettings.asset
/// Hit-stop, kamera sarsintisi, parcalar ve sesler buradan ayri ayri acilip kapatilir.
/// </summary>
public static class GameFeelSetup
{
    private const string AssetPath = "Assets/Resources/GameFeelSettings.asset";

    [MenuItem("Tools/FarToHell/Vurus Hissi Ayarlari")]
    public static void SelectSettings()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameFeelSettings>(AssetPath);
        if (asset == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            asset = ScriptableObject.CreateInstance<GameFeelSettings>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}

/// <summary>Sise ikonlari kendiliginden Sprite olarak import edilsin.</summary>
public class FlaskIconImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/UI/Resources/FlaskIcon")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
