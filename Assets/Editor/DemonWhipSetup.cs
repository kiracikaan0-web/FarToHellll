using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Seytan kiz dusmanini hazirlar ve sahneye koyar.
/// Tools > FarToHell > Dusman Ekle: Seytan Kiz
///  - Demon_Whip_Sheet.png (18 kare) ve Demon_Walk_Sheet.png (20 kare) bolunur (140 PPU, pivot ayaklarda)
///  - Demon_Idle.anim, Demon_WhipAttack.anim (20 fps), Demon_Walk.anim (20 fps, dongulu)
///  - Assets/Animations/Enemies/Demon.controller (Idle, Walk, Whip)
///  - Assets/Prefabs/Enemies/Demon.prefab (DemonEnemy, Health, can bari)
///  - Oyuncunun 8 birim sagina bir tane koyar
/// Animasyonlar her calistirmada guncellenir; "Mevcudu kullan" secilirse prefab ayarlari korunur.
/// Tools > FarToHell > Seytan Kiz: Animasyonlari Guncelle  -> sahneye eklemeden sadece animasyonlari yeniler.
/// </summary>
public static class DemonWhipSetup
{
    private const string SheetPath = "Assets/Sprites/Demon/Demon_Whip_Sheet.png";
    private const string WalkSheetPath = "Assets/Sprites/Demon/Demon_Walk_Sheet.png";
    private const string WhipClipPath = "Assets/Sprites/Demon/Demon_WhipAttack.anim";
    private const string IdleClipPath = "Assets/Sprites/Demon/Demon_Idle.anim";
    private const string WalkClipPath = "Assets/Sprites/Demon/Demon_Walk.anim";
    private const string ControllerPath = "Assets/Animations/Enemies/Demon.controller";
    private const string PrefabPath = "Assets/Prefabs/Enemies/Demon.prefab";
    private const int FrameW = 747, FrameH = 535;
    private const float Fps = 20f, WalkFps = 20f;
    private static readonly Vector2 Pivot = new Vector2(0.6881f, 0.0262f);   // ayaklarin ortasi

    [MenuItem("Tools/FarToHell/Dusman Ekle: Seytan Kiz")]
    public static void AddDemon()
    {
        bool rebuild = true;
        if (System.IO.File.Exists(PrefabPath))
        {
            int choice = EditorUtility.DisplayDialogComplex("Seytan Kiz",
                "Demon prefabi zaten var.\n\n'Mevcudu kullan': ayarlarin korunur (animasyonlar yine guncellenir), sahneye bir tane daha eklenir.\n'Bastan kur': prefab varsayilan ayarlarla yeniden olusturulur.",
                "Mevcudu kullan", "Vazgec", "Bastan kur");
            if (choice == 1) return;
            rebuild = choice == 2;
        }

        var player = PlayerStatsSetup.FindPlayerOpeningSceneIfNeeded();
        if (player == null) return;
        int ground = ReadGroundLayer(player);

        var controller = BuildAnimations(out Sprite idleSprite);
        if (controller == null) return;

        GameObject prefab = rebuild ? BuildPrefab(idleSprite, controller, ground) : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!rebuild) ApplyMobility();

        int tilemaps = EnsureTilemapCollision(ground);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(inst, "Seytan Kiz");
        inst.transform.position = FindGroundPoint(player.transform.position + new Vector3(8f, 0f, 0f), ground);
        EditorSceneManager.MarkSceneDirty(inst.scene);
        Selection.activeGameObject = inst;

        EditorUtility.DisplayDialog("Seytan Kiz",
            "Seytan kiz oyuncunun 8 birim sagina, zemine eklendi. Kaydetmek icin Ctrl+S.\n" +
            tilemaps + " tilemap'in collision'i kontrol edildi; kiz tilemap'lerin uzerinde durur ve duvarlardan gecemez.\n" +
            "Kirbac vurusu oyuncunun canini %25 azaltir. Yururken yurume animasyonu oynar.\n\n" +
            "Prefab: " + PrefabPath + "\n" +
            "Ayarlar: Inspector > DemonEnemy (hasar, menzil, hiz) ve Health (can).\n" +
            "Boyut: sahnedeki objenin Scale degerini degistir (vurus alani da beraber buyur).\n\n" +
            "Baska yerlere koymak icin prefabi Project penceresinden sahneye surukle.", "Tamam");
    }

    [MenuItem("Tools/FarToHell/Seytan Kiz: Animasyonlari Guncelle")]
    public static void UpdateAnimations()
    {
        var controller = BuildAnimations(out _);
        if (controller == null) return;
        ApplyMobility();
        EditorUtility.DisplayDialog("Seytan Kiz", "Animasyonlar guncellendi (Idle, Walk, Whip).\nBoss daha hareketli: devriye 1.6, kovalama 3.2 hiz; uzaktaysan ustune atilip kirbaclar.\nSahnedeki seytan kizlar Play'de yeni animasyonlari kullanir.", "Tamam");
    }

    /// <summary>Sheet'leri boler, klipleri ve controller'i olusturur/gunceller (controller yerinde guncellenir, prefab baglantisi kopmaz).</summary>
    private static AnimatorController BuildAnimations(out Sprite idleSprite)
    {
        idleSprite = null;
        var sprites = SliceSheet(SheetPath, "Demon_Whip_");
        if (sprites == null) return null;
        idleSprite = sprites[0];
        var whip = BuildClip(WhipClipPath, sprites, false, Fps);
        var idle = BuildClip(IdleClipPath, new[] { sprites[0] }, true, Fps);
        var walkSprites = SliceSheet(WalkSheetPath, "Demon_Walk_");
        var walk = walkSprites != null ? BuildClip(WalkClipPath, walkSprites, true, WalkFps) : null;
        UIBuildUtil.EnsureFolder("Assets", "Animations");
        UIBuildUtil.EnsureFolder("Assets/Animations", "Enemies");
        UIBuildUtil.EnsureFolder("Assets", "Prefabs");
        UIBuildUtil.EnsureFolder("Assets/Prefabs", "Enemies");
        return BuildController(idle, whip, walk);
    }

    /// <summary>Mevcut prefabi yeni hareketli ayarlara getirir (hiz, adim boyu). Diger ayarlara dokunmaz.</summary>
    private static void ApplyMobility()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var enemy = prefab != null ? prefab.GetComponent<DemonEnemy>() : null;
        if (enemy == null) return;
        var so = new SerializedObject(enemy);
        so.FindProperty("patrolSpeed").floatValue = 1.6f;
        so.FindProperty("chaseSpeed").floatValue = 3.2f;
        so.FindProperty("walkStrideLength").floatValue = 1.07f;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
    }

    private static Sprite[] SliceSheet(string path, string prefix)
    {
        AssetDatabase.Refresh();
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { EditorUtility.DisplayDialog("Seytan Kiz", "Sheet bulunamadi: " + path, "Tamam"); return null; }
        imp.GetSourceTextureWidthAndHeight(out int w, out int h);
        int frames = Mathf.Max(1, w / FrameW);
#pragma warning disable 0618
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Multiple;
        imp.spritePixelsPerUnit = 140;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 16384;
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
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => Index(s.name)).ToArray();
        if (sprites.Length == 0) { EditorUtility.DisplayDialog("Seytan Kiz", "Sheet bolunemedi, Console'a bak: " + path, "Tamam"); return null; }
        return sprites;
    }

    private static AnimationClip BuildClip(string path, Sprite[] sprites, bool loop, float fps)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = fps;
        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = sprites.Length / fps, value = sprites[loop ? 0 : sprites.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
        var s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController BuildController(AnimationClip idle, AnimationClip whip, AnimationClip walk)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = controller.layers[0].stateMachine;
        AnimatorState Get(string name, Vector3 pos)
        {
            var st = sm.states.Select(x => x.state).FirstOrDefault(x => x.name == name);
            if (st == null) { st = sm.AddState(name, pos); st.writeDefaultValues = false; }
            return st;
        }
        var sIdle = Get("Idle", new Vector3(250, 0));
        sIdle.motion = idle;
        var sWhip = Get("Whip", new Vector3(250, 150));
        sWhip.motion = whip;
        if (walk != null) Get("Walk", new Vector3(500, 0)).motion = walk;
        sm.defaultState = sIdle;
        if (sWhip.transitions.Length == 0)
        {
            var t = sWhip.AddTransition(sIdle);
            t.hasExitTime = true; t.exitTime = 1f; t.hasFixedDuration = true; t.duration = 0f;
        }
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static GameObject BuildPrefab(Sprite idleSprite, AnimatorController controller, int ground)
    {
        var root = new GameObject("Demon");
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = idleSprite;
        sr.sortingOrder = -1;
        var anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        var rb = root.AddComponent<Rigidbody2D>();
        rb.mass = 4f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Govde (kirbac haric): ayaklardan basa kadar
        var cap = root.AddComponent<CapsuleCollider2D>();
        cap.direction = CapsuleDirection2D.Vertical;
        cap.size = new Vector2(1.2f, 3.3f);
        cap.offset = new Vector2(0f, 1.65f);

        var health = root.AddComponent<Health>();
        var hso = new SerializedObject(health);
        hso.FindProperty("maxHealth").intValue = 70;
        hso.FindProperty("invulnerableAfterHit").floatValue = 0.15f;
        hso.ApplyModifiedPropertiesWithoutUndo();

        var enemy = root.AddComponent<DemonEnemy>();
        var eso = new SerializedObject(enemy);
        eso.FindProperty("groundLayer").intValue = ground;
        eso.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<EnemyHealthBar>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Acik sahnedeki tilemap'lere zemin katmani + TilemapCollider2D + CompositeCollider2D ekler (oyuncudakiyle ayni kurulum).</summary>
    private static int EnsureTilemapCollision(int groundMask)
    {
        int layer = 0;
        for (int i = 0; i < 32; i++) if ((groundMask & (1 << i)) != 0) { layer = i; break; }
        var tilemaps = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var tm in tilemaps)
        {
            var go = tm.gameObject;
            Undo.RecordObject(go, "Layer");
            go.layer = layer;
            var tc = go.GetComponent<UnityEngine.Tilemaps.TilemapCollider2D>();
            if (tc == null) tc = Undo.AddComponent<UnityEngine.Tilemaps.TilemapCollider2D>(go);
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
        return tilemaps.Length;
    }

    private static int ReadGroundLayer(PlayerMovement player)
    {
        var prop = new SerializedObject(player).FindProperty("groundLayer");
        int v = prop != null ? prop.intValue : 0;
        return v != 0 ? v : (1 << 6);
    }

    private static Vector3 FindGroundPoint(Vector3 around, int ground)
    {
        Physics2D.SyncTransforms();
        var hit = Physics2D.Raycast((Vector2)around + Vector2.up * 6f, Vector2.down, 30f, ground);
        return hit.collider != null ? (Vector3)hit.point : around;
    }

    private static int Index(string name)
    {
        int u = name.LastIndexOf('_');
        return u >= 0 && int.TryParse(name.Substring(u + 1), out int n) ? n : 0;
    }
}
