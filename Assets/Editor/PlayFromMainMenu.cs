using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play'e basinca hangi sahne acik olursa olsun oyun Ana Menu'den (Build Settings'teki 0. sahne) baslar.
/// Unity menusu: Tools > FarToHell > Play'i Ana Menuden Baslat  (isaretliyken acik; tiklayinca acilir/kapanir)
/// Kapaliyken Unity'nin normal davranisi: acik olan sahne baslar (bir bolumu dogrudan test etmek icin).
/// Bu ayar sadece senin bilgisayarinda gecerlidir (EditorPrefs), arkadasini etkilemez.
/// </summary>
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    private const string MenuPath = "Tools/FarToHell/Play'i Ana Menuden Baslat";
    private const string PrefKey = "FarToHell.PlayFromMainMenu";

    static PlayFromMainMenu()
    {
        // Editor acildiginda ve scriptler derlendiginde ayari uygula
        EditorApplication.delayCall += Apply;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath, false, 0)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
        Debug.Log(Enabled
            ? "Play artik Ana Menu'den baslayacak."
            : "Play artik acik olan sahneden baslayacak.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void Apply()
    {
        if (!Enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }
        var scenes = EditorBuildSettings.scenes;
        SceneAsset first = null;
        foreach (var s in scenes)
        {
            if (!s.enabled) continue;
            first = AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path);
            if (first != null) break;
        }
        EditorSceneManager.playModeStartScene = first;
    }
}
