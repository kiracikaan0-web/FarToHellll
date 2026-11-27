using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Map1'in farkli yerlerine iskelet (Skeleton2) yerlestirir.
/// Tools > FarToHell > Map1: Iskeletleri Yerlestir
///  - Tilemap'te uzerinde iskeletin sigacagi kadar bos alan olan, yeterince genis duz zeminleri bulur
///  - Karakterin baslangicina ve Seytan Kiz'a yakin yerleri atlar, iskeletleri haritaya yayar
///  - Iskeletler karakterle ayni boyda olur (Map1'de karakter 1.5 olcekte)
///  - Hepsi "Iskeletler" objesinin altina konur; tekrar calistirirsan eskileri silip yeniden dagitir (Ctrl+Z ile geri alinir)
/// Tek tek yerini degistirmek istersen Hierarchy'den secip surukleyebilirsin.
/// </summary>
public static class SkeletonPlacer
{
    private const string MapScene = "Assets/Scenes/Map1.unity";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Skeleton2.prefab";
    private const string RootName = "Iskeletler";

    private const int MaxSkeletons = 8;
    private const float MinSpacing = 22f;        // iki iskelet arasi en az (dunya birimi)
    private const float AvoidPlayer = 22f;       // baslangica bu kadar yakin koyma
    private const float AvoidDemon = 14f;
    private const float SkeletonHeight = 4.3f;   // olcek 1 iken boy
    private const float MinPlatformWidth = 7f;   // olcek 1 iken: devriye icin gereken duz zemin

    private struct Platform
    {
        public Vector3 left, right;   // ust yuzey uclari (dunya)
        public float Width => right.x - left.x;
        public Vector3 Center => (left + right) * 0.5f;
    }

    [MenuItem("Tools/FarToHell/Map1: Iskeletleri Yerlestir")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Fail("Iskelet prefabi bulunamadi: " + PrefabPath); return; }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != MapScene)
        {
            if (!File.Exists(MapScene)) { Fail(MapScene + " bulunamadi."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
        }

        var player = Object.FindAnyObjectByType<PlayerMovement>();
        float k = player != null ? Mathf.Abs(player.transform.lossyScale.y) : 1f;
        var solid = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude)
            .Where(t => t.GetComponent<TilemapCollider2D>() != null).ToArray();
        if (solid.Length == 0) { Fail("Map1'de collision'li tilemap yok."); return; }

        var platforms = FindPlatforms(solid, SkeletonHeight * k, MinPlatformWidth * k);
        var avoid = new List<(Vector3 pos, float r)>();
        if (player != null) avoid.Add((player.transform.position, AvoidPlayer));
        foreach (var d in Object.FindObjectsByType<DemonEnemy>(FindObjectsInactive.Exclude)) avoid.Add((d.transform.position, AvoidDemon));

        // Genis zeminler once; birbirinden ve yasak bolgelerden uzak olanlari sec
        var picks = new List<Platform>();
        foreach (var p in platforms.OrderByDescending(p => p.Width))
        {
            if (picks.Count >= MaxSkeletons) break;
            var c = p.Center;
            if (avoid.Any(a => Vector2.Distance(c, a.pos) < a.r)) continue;
            if (picks.Any(q => Vector2.Distance(q.Center, c) < MinSpacing)) continue;
            picks.Add(p);
        }
        if (picks.Count == 0) { Fail("Iskelet koyacak uygun duz zemin bulunamadi."); return; }
        picks = picks.OrderBy(p => p.Center.x).ToList();

        Undo.SetCurrentGroupName("Iskeletleri Yerlestir");
        int group = Undo.GetCurrentGroup();

        var oldRoot = GameObject.Find(RootName);
        if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, RootName);

        for (int i = 0; i < picks.Count; i++)
        {
            var p = picks[i];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(go, "Iskelet");
            go.name = "Iskelet_" + (i + 1);
            go.transform.SetParent(root.transform, false);
            go.transform.position = p.Center + Vector3.up * 0.05f;
            float face = (i % 2 == 0) ? 1f : -1f;   // yarisi sola yarisi saga baksin
            go.transform.localScale = new Vector3(k * face, k, 1f);

            // devriye zeminden tasmasin
            var enemy = go.GetComponent<SkeletonEnemy>();
            if (enemy != null)
            {
                var so = new SerializedObject(enemy);
                var pd = so.FindProperty("patrolDistance");
                if (pd != null) pd.floatValue = Mathf.Clamp(p.Width * 0.5f - 1.5f * k, 1f, 6f * k);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // can bari kafanin ustunde kalsin
            foreach (var bar in go.GetComponentsInChildren<EnemyHealthBar>(true))
            {
                var bso = new SerializedObject(bar);
                var off = bso.FindProperty("offset");
                if (off != null) off.vector2Value = off.vector2Value * k;
                bso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root;

        EditorUtility.DisplayDialog("Iskeletler",
            picks.Count + " iskelet Map1'e yerlestirildi ve sahne kaydedildi.\n" +
            "Hierarchy > " + RootName + " altinda. Yerlerini surukleyerek degistirebilirsin.\n\n" +
            "Her iskelet olunce 1 Altin dusurur, altin sol alttaki esya barina gelir.\n" +
            "Tekrar calistirirsan iskeletler bastan dagitilir.", "Tamam");
    }

    /// <summary>Ustunde 'height' kadar bos alan olan, en az 'minWidth' genisligindeki duz zeminler.</summary>
    private static List<Platform> FindPlatforms(Tilemap[] maps, float height, float minWidth)
    {
        var result = new List<Platform>();
        foreach (var tm in maps)
        {
            var b = tm.cellBounds;
            var cell = tm.layoutGrid.cellSize;
            int clearCells = Mathf.CeilToInt(height / Mathf.Max(0.01f, cell.y));
            for (int y = b.yMin; y < b.yMax; y++)
            {
                int runStart = int.MinValue;
                for (int x = b.xMin; x <= b.xMax; x++)
                {
                    bool ok = x < b.xMax && IsSurface(maps, tm, new Vector3Int(x, y, 0), clearCells);
                    if (ok && runStart == int.MinValue) runStart = x;
                    if (!ok && runStart != int.MinValue)
                    {
                        Vector3 l = TopLeft(tm, new Vector3Int(runStart, y, 0));
                        Vector3 r = TopLeft(tm, new Vector3Int(x, y, 0));
                        // kenarlardan yarim hucre iceride
                        l.x += cell.x * 0.5f; r.x -= cell.x * 0.5f;
                        var p = new Platform { left = l, right = r };
                        if (p.Width >= minWidth) result.Add(p);
                        runStart = int.MinValue;
                    }
                }
            }
        }
        return result;
    }

    private static bool IsSurface(Tilemap[] maps, Tilemap tm, Vector3Int c, int clearCells)
    {
        if (!tm.HasTile(c)) return false;
        for (int i = 1; i <= clearCells; i++)
            if (AnySolid(maps, tm.GetCellCenterWorld(c + new Vector3Int(0, i, 0)))) return false;
        return true;
    }

    private static bool AnySolid(Tilemap[] maps, Vector3 world)
    {
        foreach (var m in maps) if (m.HasTile(m.WorldToCell(world))) return true;
        return false;
    }

    private static Vector3 TopLeft(Tilemap tm, Vector3Int c)
    {
        var size = tm.layoutGrid.cellSize;
        return tm.GetCellCenterWorld(c) + new Vector3(-size.x * 0.5f, size.y * 0.5f, 0f);
    }

    private static void Fail(string msg) => EditorUtility.DisplayDialog("Iskeletler", msg, "Tamam");
}
