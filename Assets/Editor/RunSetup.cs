using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Kosu animasyonunu kurar: sheet'i 8 kareye boler, SkeletonRun.anim klibini (14 fps, dongulu) olusturur
/// ve Player.controller'a "Run" state'ini ekler. Gecisleri PlayerMovement kodla yapar.
/// Tools > FarToHell > Kosuyu Kur
/// </summary>
public static class RunSetup
{
    private const string SheetPath = "Assets/Sprites/SkeletonRun/SkeletonRun_Sheet.png";
    private const string ClipPath = "Assets/Sprites/SkeletonRun/SkeletonRun.anim";
    private const string ControllerPath = "Assets/Animations/Player/Player.controller";
    private const int FrameW = 740, FrameH = 580;
    private const float Fps = 14f;
    private static readonly Vector2 Pivot = new Vector2(0.7027f, 0.036f);   // ayak hizasi, yurume ile ayni

    [MenuItem("Tools/FarToHell/Kosuyu Kur")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        var imp = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (imp == null) { EditorUtility.DisplayDialog("Kosu", "Sheet bulunamadi: " + SheetPath, "Tamam"); return; }
        imp.GetSourceTextureWidthAndHeight(out int w, out int h);
        int frames = Mathf.Max(1, w / FrameW);

#pragma warning disable 0618 // spritesheet eski ama hala calisiyor; ek paket gerektirmiyor
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 140;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 8192;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var meta = new SpriteMetaData[frames];
        for (int i = 0; i < frames; i++)
            meta[i] = new SpriteMetaData
            {
                name = "SkeletonRun_" + i,
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = Pivot
            };
        imp.spritesheet = meta;
#pragma warning restore 0618
        imp.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(s => Index(s.name)).ToArray();
        if (sprites.Length == 0) { EditorUtility.DisplayDialog("Kosu", "Sheet bolunemedi, Console'a bak.", "Tamam"); return; }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
        clip.frameRate = Fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / Fps, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        string ctrlNote = "";
        if (controller != null)
        {
            var sm = controller.layers[0].stateMachine;
            var st = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == "Run");
            if (st == null) { st = sm.AddState("Run", new Vector3(650, 150)); st.writeDefaultValues = false; }
            st.motion = clip;
            EditorUtility.SetDirty(controller);
            ctrlNote = "\nPlayer.controller'a 'Run' state'i eklendi.";
        }
        else ctrlNote = "\nUYARI: " + ControllerPath + " bulunamadi.";

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Kosu", sprites.Length + " karelik kosu kuruldu (" + Fps + " fps)." + ctrlNote +
            "\n\nOyunda: yonle Shift'e basinca dash atar; Shift'i basili tutmaya devam edersen dash bitince kosar.", "Tamam");
    }

    private static int Index(string name)
    {
        int u = name.LastIndexOf('_');
        return u >= 0 && int.TryParse(name.Substring(u + 1), out int n) ? n : 0;
    }
}
