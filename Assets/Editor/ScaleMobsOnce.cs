using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Bir kez: Skeleton2 moblarini 1.5 kat buyutur.
///  - Build'deki sahnelerdeki tum Skeleton2'ler (SkeletonEnemy) 1.5 kat buyutulur (bakis yonu korunur)
///  - Prefab da ayni boyuta getirilir, yeni eklenen moblar sahnedekilerle ayni boyda olur
/// Acik ve kaydedilmemis degisiklikleri olan sahneye sadece isaretlenir (Ctrl+S ile sen kaydedersin);
/// acik olmayan sahneler acilip kaydedilir. Pence alani, kemik atisi, gorus ve can bari boyutla birlikte buyur.
/// </summary>
public static class ScaleMobsOnce
{
    private const string Key = "FarToHell.MobScale15.v1";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Skeleton2.prefab";
    private const float Factor = 1.5f;

    [InitializeOnLoadMethod]
    private static void Auto()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(Key, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorPrefs.SetBool(Key, true);   // ne olursa olsun iki kez buyutme
            int n = Apply();
            Debug.Log("[Mob] " + n + " Skeleton2 1.5 kat buyutuldu (prefab dahil).");
        };
    }

    private static int Apply()
    {
        int count = 0;
        // prefab: kok olcegi 1.5 kat (sahnede olcegi degistirilmemis yeni moblar icin)
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var s = root.transform.localScale;
            root.transform.localScale = new Vector3(s.x * Factor, s.y * Factor, s.z);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        foreach (var bs in EditorBuildSettings.scenes)
        {
            if (string.IsNullOrEmpty(bs.path) || !System.IO.File.Exists(bs.path)) continue;
            var scene = EditorSceneManager.GetSceneByPath(bs.path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(bs.path, OpenSceneMode.Additive);
            int here = 0;
            foreach (var go in scene.GetRootGameObjects())
                foreach (var e in go.GetComponentsInChildren<SkeletonEnemy>(true))
                {
                    var t = e.transform;
                    // prefab'dan gelen ve sahnede olcegi degistirilmemis mob zaten prefabla buyudu
                    if (PrefabUtility.IsPartOfPrefabInstance(t) && !HasScaleOverride(t)) { here++; continue; }
                    Undo.RecordObject(t, "Mob 1.5x");
                    t.localScale = new Vector3(t.localScale.x * Factor, t.localScale.y * Factor, t.localScale.z);
                    here++;
                }
            count += here;
            if (here > 0) EditorSceneManager.MarkSceneDirty(scene);
            // acik olmayan sahne: kaydet ve kapat; acik sahne: sadece degisti olarak isaretle (Ctrl+S ile kaydedersin)
            if (!wasLoaded) { if (here > 0) EditorSceneManager.SaveScene(scene); EditorSceneManager.CloseScene(scene, true); }
        }
        return count;
    }

    private static bool HasScaleOverride(Transform t)
    {
        foreach (var m in PrefabUtility.GetPropertyModifications(t.gameObject) ?? new PropertyModification[0])
            if (m.target == PrefabUtility.GetCorrespondingObjectFromSource(t) && m.propertyPath.StartsWith("m_LocalScale")) return true;
        return false;
    }
}
