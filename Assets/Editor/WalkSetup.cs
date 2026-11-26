using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Yeni 8 karelik yurume sheet'ini Unity'nin kendi araclariyla boler ve SkeletonWalk.anim klibini
/// 8 kare / 16 fps olarak yeniden kurar (dongulu). Player.controller'daki Walk state'i ayni klibi kullandigi icin
/// baska bir sey degistirmek gerekmez.
/// Tools > FarToHell > Yeni Yurumeyi Kur
/// </summary>
public static class WalkSetup
{
    private const string SheetPath = "Assets/Sprites/SkeletonWalk/SkeletonWalk_Sheet.png";
    private const string ClipPath = "Assets/Sprites/SkeletonWalk/SkeletonWalk.anim";
    private const int FrameW = 660, FrameH = 580;
    private const float Fps = 16f;
    private static readonly Vector2 Pivot = new Vector2(0.66667f, 0.036f);   // ayak hizasi, idle ile ayni

    [MenuItem("Tools/FarToHell/Yeni Yurumeyi Kur")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        var imp = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        if (imp == null || tex == null) { EditorUtility.DisplayDialog("Yurume", "Sheet bulunamadi: " + SheetPath, "Tamam"); return; }

        // gercek resim genisligine gore kare sayisi (5280 / 660 = 8)
        imp.GetSourceTextureWidthAndHeight(out int w, out int h);
        int frames = Mathf.Max(1, w / FrameW);

#pragma warning disable 0618 // spritesheet eski ama hala calisiyor; ek paket gerektirmiyor
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.maxTextureSize = 8192;
        var meta = new SpriteMetaData[frames];
        for (int i = 0; i < frames; i++)
            meta[i] = new SpriteMetaData
            {
                name = "SkeletonWalk_" + i,
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = Pivot
            };
        imp.spritesheet = meta;
#pragma warning restore 0618
        imp.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
            .OrderBy(s => Index(s.name)).ToArray();
        if (sprites.Length == 0) { EditorUtility.DisplayDialog("Yurume", "Sheet bolunemedi, Console'a bak.", "Tamam"); return; }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
        clip.frameRate = Fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        // son anahtar: dongu suresini tam 8 kare yapar (ilk kareye donus)
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / Fps, value = sprites[0] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Yurume", sprites.Length + " karelik yurume kuruldu (" + Fps + " fps).\nPlay'e basip dene.", "Tamam");
    }

    private static int Index(string name)
    {
        int u = name.LastIndexOf('_');
        return u >= 0 && int.TryParse(name.Substring(u + 1), out int n) ? n : 0;
    }
}
