using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Skeleton2 (iskelet mob) icin 20 karelik yurume animasyonu: sheet'i boler, Skeleton2Walk.anim klibini kurar
/// ve Skeleton2.controller'a "Walk" state'i ekler. SkeletonEnemy yururken bu state'i hizina gore oynatir.
/// Derlemeden sonra bir kez kendiliginden calisir. Elle: Tools > FarToHell > Iskelet Mob Yurumesini Kur
/// </summary>
public static class Skeleton2WalkSetup
{
    private const string SheetPath = "Assets/Sprites/Skeleton2/Skeleton2Walk_Sheet.png";
    private const string ClipPath = "Assets/Animations/Enemies/Skeleton2Walk.anim";
    private const string ControllerPath = "Assets/Animations/Enemies/Skeleton2.controller";
    private const int FrameW = 740, FrameH = 680;
    private const float Fps = 20f;                                         // 20 kare = 1 sn'lik dongu (hiz koddan ayarlanir)
    private static readonly Vector2 Pivot = new Vector2(0.757f, 0.038f);   // saldiri/bekleme kareleriyle ayni ayak noktasi
    private const string Key = "FarToHell.Skeleton2Walk.v1";

    [InitializeOnLoadMethod]
    private static void Auto()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(Key, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (Build(false)) { EditorPrefs.SetBool(Key, true); Debug.Log("[Mob] Iskelet mob'a 20 karelik yurume animasyonu eklendi (Skeleton2.controller > Walk)."); }
        };
    }

    [MenuItem("Tools/FarToHell/Iskelet Mob Yurumesini Kur")]
    public static void Menu()
    {
        if (Build(true)) EditorUtility.DisplayDialog("Iskelet Mob", "20 karelik yurume kuruldu (Skeleton2.controller > Walk).\nPlay'e basip dene.", "Tamam");
    }

    private static bool Build(bool dialogs)
    {
        AssetDatabase.Refresh();
        var imp = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (imp == null) { if (dialogs) EditorUtility.DisplayDialog("Iskelet Mob", "Sheet bulunamadi: " + SheetPath, "Tamam"); return false; }
        imp.GetSourceTextureWidthAndHeight(out int w, out int h);
        int frames = Mathf.Max(1, w / FrameW);
#pragma warning disable 0618
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 140;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 16384;                     // 20 x 740 = 14800 px
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var meta = new SpriteMetaData[frames];
        for (int i = 0; i < frames; i++)
            meta[i] = new SpriteMetaData
            {
                name = "Skeleton2Walk_" + i,
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = Pivot
            };
        imp.spritesheet = meta;
#pragma warning restore 0618
        imp.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(s => Index(s.name)).ToArray();
        if (sprites.Length == 0) { if (dialogs) EditorUtility.DisplayDialog("Iskelet Mob", "Sheet bolunemedi, Console'a bak.", "Tamam"); return false; }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
        clip.frameRate = Fps;
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / Fps, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { if (dialogs) EditorUtility.DisplayDialog("Iskelet Mob", "Controller bulunamadi: " + ControllerPath, "Tamam"); return false; }
        var sm = controller.layers[0].stateMachine;
        var st = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == "Walk");
        if (st == null) { st = sm.AddState("Walk", new Vector3(500, 120)); st.writeDefaultValues = false; }
        st.motion = clip;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return true;
    }

    private static int Index(string name)
    {
        int u = name.LastIndexOf('_');
        return u >= 0 && int.TryParse(name.Substring(u + 1), out int n) ? n : 0;
    }
}
