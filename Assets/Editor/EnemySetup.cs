using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Skeleton2 dusmanini hazirlar ve sahneye koyar.
/// Unity menusu: Tools > FarToHell > Dusman Ekle: Skeleton2
///
///  - Assets/Animations/Enemies/Skeleton2.controller (Idle, Attack, Throw)
///  - Assets/Prefabs/Enemies/BoneProjectile.prefab (kemik)
///  - Assets/Prefabs/Enemies/Skeleton2.prefab (dusman)
///  - Oyuncunun 8 birim sagina bir Skeleton2 koyar ve sahneyi kaydeder
/// Tekrar calistirinca prefablar varsa onlari kullanir ve sahneye bir dusman daha ekler.
/// Baska yerlere de koymak icin prefabi Project penceresinden sahneye surukle.
/// </summary>
public static class EnemySetup
{
    private const string AnimFolder = "Assets/Animations/Enemies";
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const string ControllerPath = AnimFolder + "/Skeleton2.controller";
    private const string IdleClipPath = AnimFolder + "/Skeleton2Idle.anim";
    private const string EnemyPrefabPath = PrefabFolder + "/Skeleton2.prefab";
    private const string BonePrefabPath = PrefabFolder + "/BoneProjectile.prefab";

    private const string AttackSheet = "Assets/Sprites/Skeleton2/Skeleton2Attack_Sheet.png";
    private const string AttackClip = "Assets/Sprites/Skeleton2/Skeleton2Attack.anim";
    private const string ThrowClip = "Assets/Sprites/Skeleton2/Skeleton2Throw.anim";
    private const string BoneSheet = "Assets/Sprites/Skeleton2/BoneProjectile_Sheet.png";

    [MenuItem("Tools/FarToHell/Dusman Ekle: Skeleton2")]
    public static void AddSkeleton2()
    {
        // --- Kaynaklar ---
        var idleSprite = LoadSprite(AttackSheet, "Skeleton2Attack_0");
        var boneSprite = LoadSprite(BoneSheet, "BoneProjectile_0");
        var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClip);
        var thr = AssetDatabase.LoadAssetAtPath<AnimationClip>(ThrowClip);
        if (idleSprite == null || boneSprite == null || attack == null || thr == null)
        {
            EditorUtility.DisplayDialog("Dusman Ekle", "Skeleton2 sprite/animasyon dosyalari bulunamadi (Assets/Sprites/Skeleton2).", "Tamam");
            return;
        }

        // Prefabi yeniden kurmak isteniyor mu?
        bool rebuild = true;
        if (System.IO.File.Exists(EnemyPrefabPath))
        {
            int choice = EditorUtility.DisplayDialogComplex("Dusman Ekle",
                "Skeleton2 prefabi zaten var.\n\n" +
                "'Mevcudu kullan': Inspector'da yaptigin ayarlar korunur, sahneye bir dusman daha eklenir.\n" +
                "'Bastan kur': prefab varsayilan ayarlarla yeniden olusturulur.",
                "Mevcudu kullan", "Vazgec", "Bastan kur");
            if (choice == 1) return;
            rebuild = choice == 2;
        }

        // Oyuncunun oldugu sahne (gerekirse acilir)
        var player = PlayerStatsSetup.FindPlayerOpeningSceneIfNeeded();
        if (player == null) return;
        LayerMask ground = ReadGroundLayer(player);

        UIBuildUtil.EnsureFolder("Assets", "Animations");
        UIBuildUtil.EnsureFolder("Assets/Animations", "Enemies");
        UIBuildUtil.EnsureFolder("Assets", "Prefabs");
        UIBuildUtil.EnsureFolder("Assets/Prefabs", "Enemies");

        GameObject enemyPrefab;
        if (rebuild)
        {
            var controller = BuildController(idleSprite, attack, thr);
            var bone = BuildBonePrefab(boneSprite, ground);
            enemyPrefab = BuildEnemyPrefab(idleSprite, controller, bone, ground);
        }
        else
        {
            enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        }

        // --- Sahneye koy ---
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, player.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(instance, "Skeleton2");
        instance.transform.position = FindGroundPoint(player.transform.position + new Vector3(8f, 0f, 0f), ground);
        EditorSceneManager.MarkSceneDirty(instance.scene);
        EditorSceneManager.SaveScene(instance.scene);
        Selection.activeGameObject = instance;

        EditorUtility.DisplayDialog("Dusman Ekle",
            "Skeleton2 oyuncunun sagina eklendi ve sahne kaydedildi.\n\n" +
            "Prefab: " + EnemyPrefabPath + "\n" +
            "Ayarlar: prefabi sec > Inspector > SkeletonEnemy ve Health.\n" +
            "Prefabdaki degisiklik sahnedeki tum Skeleton2'lere yansir.\n\n" +
            "Sahnede dusmani secince renkli daireler: sari = gorme, kirmizi = pence alani, mavi = kemik cikisi.",
            "Tamam");
    }

    // ---------------- Animator ----------------

    private static AnimatorController BuildController(Sprite idleSprite, AnimationClip attack, AnimationClip thr)
    {
        // Idle klibi: saldirinin ilk karesi (Skeleton2.png farkli olcek/pivotta oldugu icin)
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (idle == null)
        {
            idle = new AnimationClip { frameRate = 12 };
            AssetDatabase.CreateAsset(idle, IdleClipPath);
        }
        AnimationUtility.SetObjectReferenceCurve(idle,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
            new[] { new ObjectReferenceKeyframe { time = 0f, value = idleSprite } });
        var settings = AnimationUtility.GetAnimationClipSettings(idle);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(idle, settings);

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = controller.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle", new Vector3(250, 0));
        sIdle.motion = idle;
        sIdle.writeDefaultValues = false;
        var sAttack = sm.AddState("Attack", new Vector3(100, 150));
        sAttack.motion = attack;
        sAttack.writeDefaultValues = false;
        var sThrow = sm.AddState("Throw", new Vector3(400, 150));
        sThrow.motion = thr;
        sThrow.writeDefaultValues = false;
        sm.defaultState = sIdle;

        // Saldirilar kodla baslar, bitince Idle'a doner
        foreach (var s in new[] { sAttack, sThrow })
        {
            var t = s.AddTransition(sIdle);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.hasFixedDuration = true;
            t.duration = 0f;
        }
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ---------------- Prefablar ----------------

    private static EnemyProjectile BuildBonePrefab(Sprite boneSprite, LayerMask ground)
    {
        var go = new GameObject("BoneProjectile");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = boneSprite;
        sr.sortingOrder = 10;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.4f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        var proj = go.AddComponent<EnemyProjectile>();
        var so = new SerializedObject(proj);
        so.FindProperty("blockLayers").intValue = ground.value;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, BonePrefabPath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<EnemyProjectile>();
    }

    private static GameObject BuildEnemyPrefab(Sprite idleSprite, AnimatorController controller, EnemyProjectile bone, LayerMask ground)
    {
        var root = new GameObject("Skeleton2");

        // Gorsel alt obje (Animator burada; yururken sallanma bunu oynatir)
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = idleSprite;
        sr.sortingOrder = -1; // oyuncunun hemen arkasinda
        var anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        var rb = root.AddComponent<Rigidbody2D>();
        rb.mass = 5f; // oyuncu kolayca itemesin
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Govde collider'i (kol haric): sprite olculerinden hesaplandi
        var cap = root.AddComponent<CapsuleCollider2D>();
        cap.direction = CapsuleDirection2D.Vertical;
        cap.size = new Vector2(1.9f, 3.4f);
        cap.offset = new Vector2(-0.3f, 1.75f);

        var health = root.AddComponent<Health>();
        var hso = new SerializedObject(health);
        hso.FindProperty("maxHealth").intValue = 60;
        hso.FindProperty("invulnerableAfterHit").floatValue = 0.15f; // oyuncunun 2'li kombosu ikisi de vursun
        hso.ApplyModifiedPropertiesWithoutUndo();

        var enemy = root.AddComponent<SkeletonEnemy>();
        var eso = new SerializedObject(enemy);
        eso.FindProperty("bonePrefab").objectReferenceValue = bone;
        eso.FindProperty("groundLayer").intValue = ground.value;
        eso.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<EnemyHealthBar>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ---------------- Yardimcilar ----------------

    private static Sprite LoadSprite(string sheetPath, string spriteName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().FirstOrDefault(s => s.name == spriteName);
    }

    private static LayerMask ReadGroundLayer(PlayerMovement player)
    {
        var so = new SerializedObject(player);
        var prop = so.FindProperty("groundLayer");
        int v = prop != null ? prop.intValue : 0;
        return v != 0 ? v : (1 << 6);
    }

    private static Vector3 FindGroundPoint(Vector3 around, LayerMask ground)
    {
        Physics2D.SyncTransforms();
        var hit = Physics2D.Raycast((Vector2)around + Vector2.up * 6f, Vector2.down, 30f, ground);
        return hit.collider != null ? (Vector3)hit.point : around;
    }
}
