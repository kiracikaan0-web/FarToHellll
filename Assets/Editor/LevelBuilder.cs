using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>
/// Bolum 1'i Assets/Levels/Level1.txt haritasindan kurar.
/// Unity menusu: Tools > FarToHell > Bolum 1 Olustur
///
///  - Kirmizi tile'lardan Tilemap (yuzey: kayali, ic: duz, lav: catlakli) + collider
///  - Ucan platformlar (tek yonlu), asansor, sarkac, aralikli lav selaleleri
///  - Oyuncu, kontrol noktalari, Skeleton2'ler
///  - Satici / boss / esya / harita gecisi icin yer tutucular (sistemleri yazilinca degisecek)
///  - Kamera + arka plan
///  - Build Settings: 0 = MainMenu, 1 = Level1; Ana menudeki BASLA artik Level1'i acar
/// Harita metnini degistirip tekrar calistirirsan bolum bastan kurulur.
/// Kurulduktan sonra Tile Palette ile elle de boyayabilirsin (Assets/Tiles).
/// </summary>
public static class LevelBuilder
{
    private const string LevelText = "Assets/Levels/Level1.txt";
    private const string ScenePath = "Assets/Scenes/Level1.unity";
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string OldGameScene = "Assets/circle.unity";
    private const string ArtFolder = "Assets/Sprites/Tiles/";
    private const string TileFolder = "Assets/Tiles/";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemies/Skeleton2.prefab";
    private const string CheckpointPrefabPath = "Assets/Prefabs/Checkpoint.prefab";
    private const string PlatformPrefabPath = "Assets/Prefabs/Level/FloatingPlatform.prefab";
    private const float Cell = 4f;

    private static List<string> rows;
    private static int W, H;

    [MenuItem("Tools/FarToHell/Bolum 1 Olustur")]
    public static void Build()
    {
        // --- Harita ---
        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(LevelText);
        if (textAsset == null) { Fail("Harita bulunamadi: " + LevelText); return; }
        rows = textAsset.text.Replace("\r", "").Split('\n')
            .Where(l => l.Length > 0 && !l.StartsWith(";")).ToList();
        H = rows.Count;
        W = rows.Max(r => r.Length);
        if (H == 0) { Fail("Harita bos."); return; }

        // --- Gerekenler ---
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (enemyPrefab == null) { Fail("Skeleton2 prefabi yok. Once Tools > FarToHell > Dusman Ekle: Skeleton2 calistir."); return; }
        var checkpointPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CheckpointPrefabPath);
        if (checkpointPrefab == null) { Fail("Kontrol noktasi prefabi yok. Once Tools > FarToHell > Kontrol Noktasi Ekle calistir."); return; }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Bolum 1", ScenePath + " zaten var. Bastan kurulsun mu?\n(Sahnede elle yaptigin degisiklikler silinir; harita metni korunur.)", "Evet", "Vazgec"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var playerPrefab = GetOrCreatePlayerPrefab();
        if (playerPrefab == null) return;
        int groundLayer = ReadGroundLayer(playerPrefab);

        // --- Gorseller ---
        var rockSprite = ImportSprite(ArtFolder + "Tile_RedRocks.png", 128f, new Vector2(0.5f, 0.5f), true);
        var plainSprite = ImportSprite(ArtFolder + "Tile_RedPlain.png", 128f, new Vector2(0.5f, 0.5f), true);
        var lavaSprite = ImportSprite(ArtFolder + "Tile_RedLava.png", 128f, new Vector2(0.5f, 0.5f), true);
        var platformSprite = ImportSprite(ArtFolder + "Platform_Red.png", 145f, new Vector2(0.5f, 0.96f), false);
        var skullHut = ImportSprite(ArtFolder + "Deco_SkullHut.png", 140f, new Vector2(0.5f, 0.02f), false);
        var skullBall = ImportSprite(ArtFolder + "Hazard_SkullBall.png", 128f, new Vector2(0.5f, 0.5f), false);
        if (rockSprite == null || plainSprite == null || lavaSprite == null || platformSprite == null || skullHut == null || skullBall == null)
        { Fail("Tile gorselleri bulunamadi: " + ArtFolder); return; }

        UIBuildUtil.EnsureFolder("Assets", "Tiles");
        var rockTile = MakeTile("Red_Surface", rockSprite);
        var plainTile = MakeTile("Red_Fill", plainSprite);
        var lavaTile = MakeTile("Red_Lava", lavaSprite);
        var platformPrefab = MakePlatformPrefab(platformSprite, groundLayer);

        // --- Kamera + arka plan ---
        var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 8f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        camGO.AddComponent<AudioListener>();
        var follow = camGO.AddComponent<CameraFollow>();
        var fso = new SerializedObject(follow);
        fso.FindProperty("useMinY").boolValue = true;
        fso.FindProperty("minY").floatValue = Cell + 4f; // zeminin alti gorunmesin
        fso.ApplyModifiedPropertiesWithoutUndo();

        // --- Tilemap ---
        var gridGO = new GameObject("Grid");
        var grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(Cell, Cell, 0f);
        var ground = CreateTilemap("Ground", gridGO.transform, groundLayer, -10);
        var lava = CreateTilemap("Lava", gridGO.transform, groundLayer, -9);

        var levelRoot = new GameObject("Level").transform;
        var hazards = new GameObject("Hazards").transform; hazards.SetParent(levelRoot);
        var platforms = new GameObject("Platforms").transform; platforms.SetParent(levelRoot);
        var enemies = new GameObject("Enemies").transform; enemies.SetParent(levelRoot);
        var markers = new GameObject("Markers").transform; markers.SetParent(levelRoot);
        var decor = new GameObject("Decor").transform; decor.SetParent(levelRoot);

        var theme = UIBuildUtil.GetOrCreateTheme();
        GameObject playerInstance = null;
        int fallIndex = 0;

        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                char c = At(x, y);
                var cellPos = new Vector3Int(x, y, 0);
                Vector3 feet = new Vector3(x * Cell + Cell * 0.5f, y * Cell, 0f);   // karenin alt ortasi
                Vector3 top = feet + Vector3.up * Cell;                                // karenin ust ortasi
                switch (c)
                {
                    case '#':
                        ground.SetTile(cellPos, IsSolid(x, y + 1) ? plainTile : rockTile);
                        break;
                    case 'L':
                        lava.SetTile(cellPos, lavaTile);
                        break;
                    case 'b':
                        var p = (GameObject)PrefabUtility.InstantiatePrefab(platformPrefab, scene);
                        p.transform.SetParent(platforms, true);
                        p.transform.position = top;
                        break;
                    case 'S':
                        playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
                        playerInstance.transform.position = feet + Vector3.up * 0.02f;
                        break;
                    case 'C':
                        var cp = (GameObject)PrefabUtility.InstantiatePrefab(checkpointPrefab, scene);
                        cp.transform.SetParent(levelRoot, true);
                        cp.transform.position = feet;
                        break;
                    case 'M':
                    case 'm':
                        var e = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, scene);
                        e.transform.SetParent(enemies, true);
                        e.transform.position = feet + Vector3.up * 0.02f;
                        if (c == 'm')
                        {
                            var se = e.GetComponent<SkeletonEnemy>();
                            var eso = new SerializedObject(se);
                            eso.FindProperty("patrolDistance").floatValue = 0.6f; // platformdan dusmesin
                            eso.ApplyModifiedPropertiesWithoutUndo();
                        }
                        break;
                    case 'K':
                        var hut = new GameObject("SkullHut");
                        hut.transform.SetParent(decor, false);
                        hut.transform.position = feet;
                        var hsr = hut.AddComponent<SpriteRenderer>();
                        hsr.sprite = skullHut;
                        hsr.sortingOrder = -6;
                        break;
                    case '$': CreateMarker(markers, feet, LevelMarker.Kind.Merchant, "SATICI\n(yakında)", theme); break;
                    case 'B': CreateMarker(markers, feet, LevelMarker.Kind.Boss, "BOSS\n(yakında)", theme); break;
                    case 'I': CreateMarker(markers, feet, LevelMarker.Kind.Item, "EŞYA", theme); break;
                    case 'D': CreateMarker(markers, feet, LevelMarker.Kind.MapExit, "HARİTA GEÇİŞİ\n(yakında)", theme); break;
                    case 'F':
                        CreateLavaFall(hazards, x, y, lavaSprite, fallIndex++);
                        break;
                    case 'E':
                        CreateElevator(platforms, x, y, platformSprite, groundLayer);
                        break;
                    case 'P':
                        CreatePendulum(hazards, top, skullBall);
                        break;
                }
            }
        }

        CreateLavaDamage(hazards);
        SetupComposite(ground.gameObject);
        SetupComposite(lava.gameObject);

        if (playerInstance == null) Debug.LogWarning("Haritada 'S' (oyuncu baslangici) yok.");
        else cam.transform.position = new Vector3(playerInstance.transform.position.x, playerInstance.transform.position.y + 2f, -10f);

        BackgroundSetup.BuildBackground(cam);

        UIBuildUtil.EnsureFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        UpdateBuildSettingsAndMenu();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("Bolum 1",
            "Bolum kuruldu: " + ScenePath + $" ({W}x{H} kare)\n\n" +
            "Ana menudeki BASLA artik bu bolumu acar.\n\n" +
            "Yer tutucular (satici, boss, esya, harita gecisi) sadece isaret; sistemleri yazilinca degisecek.\n" +
            "Haritayi degistirmek: " + LevelText + " dosyasini duzenleyip bu menuyu tekrar calistir\n" +
            "ya da Tile Palette ile sahnede elle boya (Assets/Tiles).", "Tamam");
    }

    // ---------------- Harita yardimcilari ----------------

    private static char At(int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return '#';
        string row = rows[H - 1 - y];
        return x < row.Length ? row[x] : '.';
    }

    private static bool IsSolid(int x, int y)
    {
        char c = At(x, y);
        return c == '#' || c == 'L';
    }

    // ---------------- Tilemap ----------------

    private static Tilemap CreateTilemap(string name, Transform parent, int layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        var tm = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = order;
        return tm;
    }

    private static void SetupComposite(GameObject go)
    {
        var tc = go.AddComponent<TilemapCollider2D>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        var comp = go.AddComponent<CompositeCollider2D>();
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;
    }

    private static Tile MakeTile(string name, Sprite sprite)
    {
        string path = TileFolder + name + ".asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, path);
        }
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        EditorUtility.SetDirty(tile);
        AssetDatabase.SaveAssets();
        return tile;
    }

    // ---------------- Nesneler ----------------

    private static GameObject MakePlatformPrefab(Sprite sprite, int layer)
    {
        var root = new GameObject("FloatingPlatform") { layer = layer };
        var sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -8;
        var box = root.AddComponent<BoxCollider2D>();
        box.size = new Vector2(Cell * 0.92f, 0.5f);
        box.offset = new Vector2(0f, -0.25f);
        box.usedByEffector = true;
        var eff = root.AddComponent<PlatformEffector2D>();
        eff.useOneWay = true;
        eff.surfaceArc = 160f;
        UIBuildUtil.EnsureFolder("Assets", "Prefabs");
        UIBuildUtil.EnsureFolder("Assets/Prefabs", "Level");
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlatformPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void CreateElevator(Transform parent, int x, int y, Sprite sprite, int layer)
    {
        // Ayni sutunda 'e' (ust durak) ara
        int topY = -1;
        for (int yy = y + 1; yy < H; yy++) if (At(x, yy) == 'e') { topY = yy; break; }
        if (topY < 0) { Debug.LogWarning($"Asansor ({x},{y}) icin ust durak 'e' bulunamadi."); return; }

        var go = new GameObject("Elevator") { layer = layer };
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -7;
        sr.color = new Color(1f, 0.85f, 0.75f, 1f);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(Cell * 0.92f, 0.5f);
        box.offset = new Vector2(0f, -0.25f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var mp = go.AddComponent<MovingPlatform>();
        Vector2 a = new Vector2(x * Cell + Cell * 0.5f, y * Cell);
        Vector2 b = new Vector2(x * Cell + Cell * 0.5f, topY * Cell);
        mp.SetPoints(a, b);
        go.transform.position = a;
    }

    private static void CreateLavaFall(Transform parent, int x, int y, Sprite lavaSprite, int index)
    {
        // Selale F karesinin tepesinden baslar, alttaki ilk kati zemine kadar akar
        int yy = y;
        while (yy > 0 && !IsSolid(x, yy - 1)) yy--;
        float topY = (y + 1) * Cell;
        float length = topY - yy * Cell;

        var go = new GameObject("LavaFall");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x * Cell + Cell * 0.5f, topY, 0f);
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        var dz = go.AddComponent<DamageZone>();
        var dso = new SerializedObject(dz);
        dso.FindProperty("damage").intValue = 20;
        dso.FindProperty("knockback").floatValue = 9f;
        dso.FindProperty("repeatInterval").floatValue = 0.5f;
        dso.ApplyModifiedPropertiesWithoutUndo();

        var stream = new GameObject("Stream");
        stream.transform.SetParent(go.transform, false);
        var sr = stream.AddComponent<SpriteRenderer>();
        sr.sprite = lavaSprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.sortingOrder = 5;
        sr.color = new Color(1f, 0.85f, 0.6f, 1f);

        var lf = go.AddComponent<LavaFall>();
        var lso = new SerializedObject(lf);
        lso.FindProperty("stream").objectReferenceValue = sr;
        lso.ApplyModifiedPropertiesWithoutUndo();
        lf.Setup(length, 1.6f, index * 2.1f);
    }

    private static void CreatePendulum(Transform parent, Vector3 pivot, Sprite ballSprite)
    {
        const float length = 17f;
        var root = new GameObject("Pendulum");
        root.transform.SetParent(parent, false);
        root.transform.position = pivot;
        var pen = root.AddComponent<Pendulum>();

        var rope = new GameObject("Rope");
        rope.transform.SetParent(root.transform, false);
        rope.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f);
        var rsr = rope.AddComponent<SpriteRenderer>();
        rsr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        rsr.drawMode = SpriteDrawMode.Sliced;
        rsr.size = new Vector2(0.22f, length);
        rsr.color = new Color(0.25f, 0.16f, 0.12f, 1f);
        rsr.sortingOrder = 3;

        var ball = new GameObject("SkullBall");
        ball.transform.SetParent(root.transform, false);
        ball.transform.localPosition = new Vector3(0f, -length, 0f);
        var bsr = ball.AddComponent<SpriteRenderer>();
        bsr.sprite = ballSprite;
        bsr.sortingOrder = 4;
        var col = ball.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.85f;
        var rb = ball.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var dz = ball.AddComponent<DamageZone>();
        var dso = new SerializedObject(dz);
        dso.FindProperty("damage").intValue = 30;
        dso.FindProperty("knockback").floatValue = 12f;
        dso.FindProperty("repeatInterval").floatValue = 0.8f;
        dso.ApplyModifiedPropertiesWithoutUndo();

        var pso = new SerializedObject(pen);
        pso.FindProperty("bob").objectReferenceValue = ball.transform;
        pso.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateLavaDamage(Transform parent)
    {
        // Yan yana lav karelerini tek bir hasar kutusunda birlestir
        for (int y = 0; y < H; y++)
        {
            int x = 0;
            while (x < W)
            {
                if (At(x, y) != 'L') { x++; continue; }
                int start = x;
                while (x < W && At(x, y) == 'L') x++;
                int n = x - start;
                var go = new GameObject("LavaDamage");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(start * Cell + n * Cell * 0.5f, (y + 1) * Cell + 0.4f, 0f);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(n * Cell - 0.4f, 1.2f);
                var dz = go.AddComponent<DamageZone>();
                var dso = new SerializedObject(dz);
                dso.FindProperty("damage").intValue = 25;
                dso.FindProperty("knockback").floatValue = 10f;
                dso.FindProperty("repeatInterval").floatValue = 0.6f;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }

    private static void CreateMarker(Transform parent, Vector3 feet, LevelMarker.Kind kind, string text, UITheme theme)
    {
        var root = new GameObject("Marker_" + kind);
        root.transform.SetParent(parent, false);
        root.transform.position = feet;

        Vector2 size; Color color; float labelY;
        switch (kind)
        {
            case LevelMarker.Kind.Merchant: size = new Vector2(1.6f, 2.6f); color = new Color(0.3f, 0.9f, 0.45f, 0.8f); labelY = 4.2f; break;
            case LevelMarker.Kind.Boss: size = new Vector2(4f, 6f); color = new Color(1f, 0.2f, 0.2f, 0.6f); labelY = 7f; break;
            case LevelMarker.Kind.MapExit: size = new Vector2(2.6f, 4.6f); color = new Color(0.4f, 0.6f, 1f, 0.7f); labelY = 5.6f; break;
            default: size = new Vector2(0.8f, 0.8f); color = new Color(1f, 0.85f, 0.3f, 0.9f); labelY = 2.4f; break;
        }

        var icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform, false);
        icon.transform.localPosition = new Vector3(0f, kind == LevelMarker.Kind.Item ? 1.2f : size.y * 0.5f, 0f);
        if (kind == LevelMarker.Kind.Item) icon.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var sr = icon.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = color;
        sr.sortingOrder = 2;

        // Dunya uzayinda etiket
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(root.transform, false);
        var canvas = labelGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;
        var rt = (RectTransform)labelGO.transform;
        rt.sizeDelta = new Vector2(600f, 160f);
        rt.localScale = Vector3.one * 0.01f;
        rt.localPosition = new Vector3(0f, labelY, 0f);
        var t = UIBuildUtil.CreateText("Text", rt, theme.buttonFont, text, 44, new Color(color.r, color.g, color.b, 1f));
        UIBuildUtil.Stretch(t.rectTransform);
        var sh = t.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(3f, -3f);

        var m = root.AddComponent<LevelMarker>();
        var so = new SerializedObject(m);
        so.FindProperty("kind").enumValueIndex = (int)kind;
        so.FindProperty("icon").objectReferenceValue = sr;
        so.FindProperty("label").objectReferenceValue = labelGO;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- Oyuncu prefabi ----------------

    private static GameObject GetOrCreatePlayerPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab != null) return prefab;
        if (!File.Exists(OldGameScene)) { Fail("Player prefabi yok ve " + OldGameScene + " bulunamadi."); return null; }

        // circle sahnesindeki Player'dan prefab olustur (tum ayarlariyla)
        var src = EditorSceneManager.OpenScene(OldGameScene, OpenSceneMode.Additive);
        GameObject player = null;
        foreach (var root in src.GetRootGameObjects())
        {
            var pm = root.GetComponentInChildren<PlayerMovement>(true);
            if (pm != null) { player = pm.gameObject; break; }
        }
        if (player != null)
        {
            UIBuildUtil.EnsureFolder("Assets", "Prefabs");
            prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
        }
        EditorSceneManager.CloseScene(src, true);
        if (prefab == null) Fail(OldGameScene + " icinde Player bulunamadi.");
        return prefab;
    }

    private static int ReadGroundLayer(GameObject playerPrefab)
    {
        var pm = playerPrefab.GetComponent<PlayerMovement>();
        var so = new SerializedObject(pm);
        int mask = so.FindProperty("groundLayer").intValue;
        for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i;
        return 6;
    }

    // ---------------- Gorsel import ----------------

    private static Sprite ImportSprite(string path, float ppu, Vector2 pivot, bool fullRect)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        settings.spriteExtrude = 1;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------- Build Settings + ana menu ----------------

    private static void UpdateBuildSettingsAndMenu()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        if (File.Exists(MainMenuPath)) scenes.Add(new EditorBuildSettingsScene(MainMenuPath, true));
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        foreach (var s in EditorBuildSettings.scenes)
            if (scenes.All(x => x.path != s.path)) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();

        // Ana menudeki BASLA -> Level1
        if (!File.Exists(MainMenuPath)) return;
        var menuScene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        var menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu != null)
        {
            var so = new SerializedObject(menu);
            so.FindProperty("gameSceneName").stringValue = Path.GetFileNameWithoutExtension(ScenePath);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene);
        }
    }

    private static void Fail(string message)
    {
        EditorUtility.DisplayDialog("Bolum 1", message, "Tamam");
    }
}
