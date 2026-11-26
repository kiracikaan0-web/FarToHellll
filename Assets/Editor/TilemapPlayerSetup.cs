using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Acik sahnedeki tum Tilemap'lere collision ekler (TilemapCollider2D + CompositeCollider2D, zemin katmani)
/// ve Player prefabini tilemap'in ustune, Scene penceresinde baktigin yere en yakin zemine koyar.
/// Tools > FarToHell > Karakteri Tilemap'e Ekle + Collision
/// </summary>
public static class TilemapPlayerSetup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/FarToHell/Karakteri Tilemap'e Ekle + Collision")]
    public static void Run()
    {
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null) { Fail("Player prefabi bulunamadi: " + PlayerPrefabPath); return; }
        int groundLayer = ReadGroundLayer(playerPrefab);

        var tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (tilemaps.Length == 0) { Fail("Acik sahnede Tilemap yok."); return; }

        Undo.SetCurrentGroupName("Karakter + Tilemap Collision");
        int group = Undo.GetCurrentGroup();

        // --- collision ---
        foreach (var tm in tilemaps)
        {
            var go = tm.gameObject;
            Undo.RecordObject(go, "Layer");
            go.layer = groundLayer;

            var tc = go.GetComponent<TilemapCollider2D>();
            if (tc == null) tc = Undo.AddComponent<TilemapCollider2D>(go);
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody2D>(go);
            Undo.RecordObject(rb, "Rigidbody");
            rb.bodyType = RigidbodyType2D.Static;
            var comp = go.GetComponent<CompositeCollider2D>();
            if (comp == null) comp = Undo.AddComponent<CompositeCollider2D>(go);
            Undo.RecordObject(comp, "Composite");
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
            Undo.RecordObject(tc, "TilemapCollider");
            tc.compositeOperation = Collider2D.CompositeOperation.Merge;
        }

        // --- karakter ---
        var existing = Object.FindAnyObjectByType<PlayerMovement>();
        GameObject player;
        if (existing != null)
        {
            player = existing.gameObject;
            Undo.RecordObject(player.transform, "Player position");
        }
        else
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Undo.RegisterCreatedObjectUndo(player, "Player");
        }

        Vector3 look = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        Vector3? spawn = FindSurface(tilemaps, look);
        if (spawn == null) { Undo.CollapseUndoOperations(group); Fail("Tilemap'te ustu bos bir zemin bulunamadi."); return; }
        player.transform.position = spawn.Value;

        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = player;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        EditorSceneManager.MarkSceneDirty(player.scene);

        string camNote = "";
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<CameraFollow>() == null)
            camNote = "\n\nNot: Main Camera'da CameraFollow yok, Play'de kamera karakteri takip etmez.";
        EditorUtility.DisplayDialog("Tamam",
            tilemaps.Length + " tilemap'e collision eklendi (katman: " + LayerMask.LayerToName(groundLayer) + ").\n" +
            "Karakter " + (existing != null ? "tasindi" : "eklendi") + ": " + spawn.Value.ToString("F1") + camNote +
            "\n\nKaydetmek icin Ctrl+S.", "Tamam");
    }

    [MenuItem("Tools/FarToHell/Kamerayi Karaktere Sabitle")]
    public static void FollowCamera()
    {
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player == null) { Fail("Sahnede karakter yok. Once 'Karakteri Tilemap'e Ekle + Collision' calistir."); return; }
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null) { Fail("Sahnede kamera yok."); return; }

        Undo.SetCurrentGroupName("Kamerayi Karaktere Sabitle");
        int group = Undo.GetCurrentGroup();

        // menu kamerasinin kendi kendine kaymasi takibi bozar: kapat
        foreach (var drift in cam.GetComponents<MenuCameraDrift>())
        {
            Undo.RecordObject(drift, "Drift");
            drift.enabled = false;
        }
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = Undo.AddComponent<CameraFollow>(cam.gameObject);
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = player.transform;
        so.ApplyModifiedProperties();

        // kamerayi hemen karakterin ustune getir (Scene/Game penceresinde de dogru gorunsun)
        Undo.RecordObject(cam.transform, "Camera position");
        var off = so.FindProperty("offset").vector2Value;
        var p = player.transform.position;
        cam.transform.position = new Vector3(p.x + off.x, p.y + off.y, cam.transform.position.z);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Selection.activeGameObject = cam.gameObject;
        EditorUtility.DisplayDialog("Tamam", "Kamera artik '" + player.name + "' karakterini takip ediyor.\nKaydetmek icin Ctrl+S.", "Tamam");
    }

    // Scene penceresinde baktigin noktaya en yakin, ustunde 2 hucre bosluk olan zemin hucresi
    private static Vector3? FindSurface(Tilemap[] tilemaps, Vector3 look)
    {
        Vector3? best = null;
        float bestD = float.MaxValue;
        foreach (var tm in tilemaps)
        {
            var b = tm.cellBounds;
            for (int x = b.xMin; x < b.xMax; x++)
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    var c = new Vector3Int(x, y, 0);
                    if (!tm.HasTile(c)) continue;
                    if (AnyTile(tilemaps, tm, c + Vector3Int.up) || AnyTile(tilemaps, tm, c + new Vector3Int(0, 2, 0))) continue;
                    Vector3 top = tm.GetCellCenterWorld(c) + new Vector3(0, tm.layoutGrid.cellSize.y * 0.5f, 0);
                    float d = Vector2.Distance(top, look);
                    if (d < bestD) { bestD = d; best = top + Vector3.up * 0.5f; }
                }
        }
        return best;
    }

    private static bool AnyTile(Tilemap[] all, Tilemap src, Vector3Int cell)
    {
        Vector3 w = src.GetCellCenterWorld(cell);
        foreach (var tm in all) if (tm.HasTile(tm.WorldToCell(w))) return true;
        return false;
    }

    private static int ReadGroundLayer(GameObject playerPrefab)
    {
        var pm = playerPrefab.GetComponent<PlayerMovement>();
        if (pm == null) return 6;
        var so = new SerializedObject(pm);
        int mask = so.FindProperty("groundLayer").intValue;
        for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i;
        return 6;
    }

    private static void Fail(string msg) { EditorUtility.DisplayDialog("Karakter + Collision", msg, "Tamam"); }
}
