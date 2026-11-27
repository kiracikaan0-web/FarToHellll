using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Boss'un "kirbacla cekilme" animasyonu: sheet'i boler (20 kare, kirbac karesiyle ayni boyut ve ayak noktasi),
/// Demon_Grapple.anim klibini kurar (20 fps, dongusuz: son karede kol ileride bekler) ve Demon.controller'a
/// "Grapple" state'i ekler. Kirbacin kendisi oyunda cizilir (DemonEnemy), boylece her mesafeye/aciya uzanir.
/// Derlemeden sonra bir kez kendiliginden calisir. Elle: Tools > FarToHell > Seytan Kiz: Kirbacla Sarip Cekmeyi Kur
/// </summary>
public static class DemonGrappleSetup
{
    private const string SheetPath = "Assets/Sprites/Demon/Demon_Grapple_Sheet.png";
    private const string ClipPath = "Assets/Sprites/Demon/Demon_Grapple.anim";
    private const string ControllerPath = "Assets/Animations/Enemies/Demon.controller";
    private const int FrameW = 747, FrameH = 535;
    private const float Fps = 20f;
    private static readonly Vector2 Pivot = new Vector2(0.6881f, 0.0262f);   // kirbac/yurume kareleriyle ayni
    private const string Key = "FarToHell.DemonGrapple.v2";
    private const string YankSheetPath = "Assets/Sprites/Demon/Demon_Yank_Sheet.png";
    private const string YankClipPath = "Assets/Sprites/Demon/Demon_Yank.anim";

    [InitializeOnLoadMethod]
    private static void Auto()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(Key, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (Build(false)) { EditorPrefs.SetBool(Key, true); Debug.Log("[Boss] Kirbacla sarip cekme saldirisi eklendi (Demon.controller > Grapple 20 kare + Yank 16 kare)."); }
        };
    }

    [MenuItem("Tools/FarToHell/Seytan Kiz: Kirbacla Sarip Cekmeyi Kur")]
    public static void Menu()
    {
        if (Build(true)) EditorUtility.DisplayDialog("Seytan Kiz", "Kirbacla sarip cekme animasyonlari kuruldu (Demon.controller > Grapple, Yank).\nPlay'e basip dene.", "Tamam");
    }

    private static bool Build(bool dialogs)
    {
        return BuildOne(dialogs, SheetPath, ClipPath, "Demon_Grapple_", "Grapple", new Vector3(500, 240))
            && BuildOne(dialogs, YankSheetPath, YankClipPath, "Demon_Yank_", "Yank", new Vector3(500, 360));
    }

    private static bool BuildOne(bool dialogs, string SheetPath, string ClipPath, string prefix, string stateName, Vector3 pos)
    {
        AssetDatabase.Refresh();
        var imp = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (imp == null) { if (dialogs) EditorUtility.DisplayDialog("Seytan Kiz", "Sheet bulunamadi: " + SheetPath, "Tamam"); return false; }
        imp.GetSourceTextureWidthAndHeight(out int w, out int h);
        int frames = Mathf.Max(1, w / FrameW);
#pragma warning disable 0618
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 140;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 16384;                     // 20 x 747 = 14940 px
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var meta = new SpriteMetaData[frames];
        for (int i = 0; i < frames; i++)
            meta[i] = new SpriteMetaData
            {
                name = prefix + i,
                rect = new Rect(i * FrameW, 0, FrameW, FrameH),
                alignment = (int)SpriteAlignment.Custom,
                pivot = Pivot
            };
        imp.spritesheet = meta;
#pragma warning restore 0618
        imp.SaveAndReimport();

        var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().OrderBy(s => Index(s.name)).ToArray();
        if (sprites.Length == 0) { if (dialogs) EditorUtility.DisplayDialog("Seytan Kiz", "Sheet bolunemedi, Console'a bak.", "Tamam"); return false; }

        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
        clip.frameRate = Fps;
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / Fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / Fps, value = sprites[sprites.Length - 1] };   // son kare: kol ileride bekler
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) { if (dialogs) EditorUtility.DisplayDialog("Seytan Kiz", "Controller bulunamadi: " + ControllerPath, "Tamam"); return false; }
        var sm = controller.layers[0].stateMachine;
        var st = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == stateName);
        if (st == null) { st = sm.AddState(stateName, pos); st.writeDefaultValues = false; }
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
