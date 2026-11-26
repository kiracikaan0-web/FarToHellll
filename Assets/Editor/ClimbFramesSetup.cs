using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Tirmanma karelerini Unity'nin kendi ice aktardigi sprite'lardan isimle bulup baglar:
///  - Player prefabindaki PlayerMovement > Climb Frames
///  - SkeletonClimb.anim klibinin kareleri
///  - Player.controller'daki Climb state'i
/// Tools > FarToHell > Tirmanma Karelerini Bagla
/// </summary>
public static class ClimbFramesSetup
{
    private const string SheetPath = "Assets/Sprites/SkeletonClimb/SkeletonClimb_Sheet.png";
    private const string ClipPath = "Assets/Sprites/SkeletonClimb/SkeletonClimb.anim";
    private const string PrefabPath = "Assets/Prefabs/Player.prefab";
    private const string ControllerPath = "Assets/Animations/Player/Player.controller";

    // sheet: 7 kare, her biri 824x878, pivot kenarin kosesinde
    private const int Frames = 7, FrameW = 824, FrameH = 878;
    private static readonly Vector2 Pivot = new Vector2(0.59992f, 0.40903f);

    [MenuItem("Tools/FarToHell/Tirmanma Karelerini Bagla")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        SliceSheet();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
            .OrderBy(s => FrameIndex(s.name)).ToArray();
        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("Tirmanma", "Sheet'te hic sprite bulunamadi:\n" + SheetPath +
                "\n\nProject'te dosyayi sec, Inspector'da Sprite Mode = Multiple oldugunu kontrol et ve Console'daki hatayi bana gonder.", "Tamam");
            return;
        }

        // 1) prefab
        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        var pm = prefabRoot.GetComponent<PlayerMovement>();
        if (pm != null)
        {
            var so = new SerializedObject(pm);
            var arr = so.FindProperty("climbFrames");
            arr.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        PrefabUtility.UnloadPrefabContents(prefabRoot);

        // 2) klip
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, ClipPath);
        }
        clip.frameRate = 12;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / 12f, value = sprites[sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);

        // 3) controller'daki Climb state'i
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            var sm = controller.layers[0].stateMachine;
            var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Climb");
            if (st == null)
            {
                st = sm.AddState("Climb", new Vector3(650, 0));
                st.writeDefaultValues = false;
                var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle");
                if (idle != null)
                {
                    var t = st.AddTransition(idle);
                    t.hasExitTime = true; t.exitTime = 1f; t.hasFixedDuration = true; t.duration = 0f;
                }
            }
            st.motion = clip;
            EditorUtility.SetDirty(controller);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Tirmanma", sprites.Length + " kare baglandi (" + sprites[0].name + " ... " + sprites[sprites.Length - 1].name + ").\n\nPlay'e basip tekrar dene.", "Tamam");
    }

#pragma warning disable 0618 // spritesheet eski ama hala calisiyor; ek paket gerektirmiyor
    private static void SliceSheet()
    {
        var imp = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (imp == null) { Debug.LogError("Tirmanma sheet'i bulunamadi: " + SheetPath); return; }
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 140;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 8192;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var meta = new SpriteMetaData[Frames];
        for (int i = 0; i < Frames; i++)
            meta[i] = new SpriteMetaData
            {
                name = "SkeletonClimb_" + i,
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = Pivot
            };
        imp.spritesheet = meta;
        imp.SaveAndReimport();
    }
#pragma warning restore 0618

    private static int FrameIndex(string name)
    {
        int u = name.LastIndexOf('_');
        int n;
        return u >= 0 && int.TryParse(name.Substring(u + 1), out n) ? n : 0;
    }
}
