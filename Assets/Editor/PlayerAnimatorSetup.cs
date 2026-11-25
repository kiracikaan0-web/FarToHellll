using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tek tikla oyuncunun Animator Controller'ini kurar.
/// Unity menusu: Tools > FarToHell > Player Animator Kur
///
/// Yaptiklari:
///  1. Assets/Animations/Player/SkeletonIdle.anim klibini olusturur (Idle icin sadece tek resim vardi).
///  2. Assets/Animations/Player/Player.controller'i durumlar, gecisler ve parametrelerle olusturur.
///  3. Acik sahnedeki PlayerMovement'li objeye Animator + PlayerCombat ekler, controller'i baglar,
///     sprite'i iskelet yapar ve (istersen) collider ile Groundcheck'i yeni sprite'a gore ayarlar.
///
/// Tekrar calistirmak guvenlidir: controller bastan kurulur. Sahnedeki degisiklikler Ctrl+Z ile geri alinabilir.
/// </summary>
public static class PlayerAnimatorSetup
{
    private const string Folder = "Assets/Animations/Player";
    private const string ControllerPath = Folder + "/Player.controller";
    private const string IdleClipPath = Folder + "/SkeletonIdle.anim";
    private const string IdleSpritePath = "Assets/Sprites/SkeletonIdle/SkeletonIdle.png";

    private const string WalkPath = "Assets/Sprites/SkeletonWalk/SkeletonWalk.anim";
    private const string RisePath = "Assets/Sprites/SkeletonJump/SkeletonJumpRise.anim";
    private const string FallPath = "Assets/Sprites/SkeletonJump/SkeletonJumpFall.anim";
    private const string DashPath = "Assets/Sprites/SkeletonDash/SkeletonDash.anim";
    private const string DashForwardPath = "Assets/Sprites/SkeletonDashForward/SkeletonDashForward.anim";
    private const string AttackPath = "Assets/Sprites/SkeletonAttack/SkeletonAttack.anim";
    private const string SlashPath = "Assets/Sprites/SkeletonSlash/SkeletonSlash.anim";

    [MenuItem("Tools/FarToHell/Player Animator Kur")]
    public static void Setup()
    {
        // --- Klipleri yukle ---
        var walk = LoadClip(WalkPath);
        var rise = LoadClip(RisePath);
        var fall = LoadClip(FallPath);
        var dash = LoadClip(DashPath);
        var dashForward = LoadClip(DashForwardPath);
        var attack = LoadClip(AttackPath);
        var slash = LoadClip(SlashPath);
        Sprite idleSprite = AssetDatabase.LoadAllAssetsAtPath(IdleSpritePath).OfType<Sprite>().FirstOrDefault();

        if (walk == null || rise == null || fall == null || dash == null || dashForward == null ||
            attack == null || slash == null || idleSprite == null)
        {
            EditorUtility.DisplayDialog("Player Animator Kur",
                "Bazi animasyon dosyalari bulunamadi. Console'daki uyarilara bak.", "Tamam");
            return;
        }

        EnsureFolder("Assets", "Animations");
        EnsureFolder("Assets/Animations", "Player");

        AnimationClip idle = CreateIdleClip(idleSprite);
        SetLoop(walk, true);

        // --- Controller ---
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            if (!EditorUtility.DisplayDialog("Player Animator Kur",
                    ControllerPath + " zaten var. Bastan kurulsun mu?\n(Controller'da elle yaptigin degisiklikler silinir.)",
                    "Evet, yeniden kur", "Vazgec"))
                return;
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;
        sm.entryPosition = new Vector3(-200, 0);
        sm.anyStatePosition = new Vector3(-200, 300);
        sm.exitPosition = new Vector3(900, 0);

        // Isimler koddaki Animator.StringToHash("...") ile ayni olmali!
        var sIdle = AddState(sm, "Idle", idle, 50, 0);
        var sWalk = AddState(sm, "Walk", walk, 350, 0);
        var sRise = AddState(sm, "JumpRise", rise, 50, -150);
        var sFall = AddState(sm, "JumpFall", fall, 350, -150);
        var sAttack = AddState(sm, "Attack", attack, 50, 150);
        var sSlash = AddState(sm, "Slash", slash, 350, 150);
        var sDashForward = AddState(sm, "DashForward", dashForward, 50, 300);
        var sDash = AddState(sm, "Dash", dash, 350, 300);
        sm.defaultState = sIdle;

        // Yer hareketleri
        Transition(sIdle, sWalk).Cond(AnimatorConditionMode.Greater, 0.1f, "Speed").Cond(AnimatorConditionMode.If, 0, "IsGrounded");
        Transition(sWalk, sIdle).Cond(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Ziplama / dusme (Idle ve Walk'tan)
        foreach (var ground in new[] { sIdle, sWalk })
        {
            Transition(ground, sRise).Cond(AnimatorConditionMode.IfNot, 0, "IsGrounded").Cond(AnimatorConditionMode.Greater, 0.1f, "VelocityY");
            Transition(ground, sFall).Cond(AnimatorConditionMode.IfNot, 0, "IsGrounded").Cond(AnimatorConditionMode.Less, -0.1f, "VelocityY");
        }
        Transition(sRise, sFall).Cond(AnimatorConditionMode.Less, 0f, "VelocityY");
        Transition(sRise, sIdle).Cond(AnimatorConditionMode.If, 0, "IsGrounded").Cond(AnimatorConditionMode.Less, 0.1f, "VelocityY");
        Transition(sFall, sIdle).Cond(AnimatorConditionMode.If, 0, "IsGrounded");

        // Aksiyonlar kodla (animator.Play) baslatilir, bitince Idle'a doner.
        foreach (var action in new[] { sAttack, sSlash, sDash, sDashForward })
            ExitTransition(action, sIdle);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string sceneResult = SetupPlayerInScene(controller, idleSprite);

        Selection.activeObject = controller;
        EditorUtility.DisplayDialog("Player Animator Kur",
            "Controller hazir: " + ControllerPath + "\n\n" + sceneResult, "Tamam");
    }

    // ---------------- Sahne ----------------

    private static string SetupPlayerInScene(AnimatorController controller, Sprite idleSprite)
    {
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player == null)
            return "Acik sahnede PlayerMovement olan obje yok. Player'in oldugu sahneyi (circle) acip bu menuyu tekrar calistir.";

        GameObject go = player.gameObject;
        Undo.SetCurrentGroupName("Player Animator Kur");
        int group = Undo.GetCurrentGroup();

        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(go);
        Undo.RecordObject(animator, "Animator");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        if (go.GetComponent<PlayerCombat>() == null)
            Undo.AddComponent<PlayerCombat>(go);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            Undo.RecordObject(sr, "Sprite");
            sr.sprite = idleSprite;
            sr.drawMode = SpriteDrawMode.Simple;
        }

        string extra = "";
        if (EditorUtility.DisplayDialog("Player Animator Kur",
                "Player'in collider'i ve Groundcheck'i yeni iskelet sprite'ina gore ayarlansin mi?\n\n" +
                "Iskelet sprite'larinin pivotu ayakta, eski collider ise ortaya gore ayarli. " +
                "Ayarlanmazsa karakter zemine gomulu ya da havada gorunebilir.",
                "Evet, ayarla", "Hayir, dokunma"))
        {
            FitColliderAndGroundCheck(player, idleSprite);
            extra = "Collider ve Groundcheck ayarlandi. ";
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;

        return "'" + go.name + "' objesine Animator ve PlayerCombat eklendi. " + extra +
               "\n\nSahneyi kaydetmeyi unutma (Ctrl+S).";
    }

    private static void FitColliderAndGroundCheck(PlayerMovement player, Sprite sprite)
    {
        Bounds b = sprite.bounds; // pivot'a gore yerel sinirlar
        float height = b.size.y * 0.85f;
        float width = b.size.x * 0.35f;

        var capsule = player.GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            Undo.RecordObject(capsule, "Collider");
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(width, height);
            capsule.offset = new Vector2(0f, height * 0.5f);
        }
        else
        {
            var box = player.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Undo.RecordObject(box, "Collider");
                box.size = new Vector2(width, height);
                box.offset = new Vector2(0f, height * 0.5f);
            }
        }

        var so = new SerializedObject(player);
        var gcProp = so.FindProperty("groundCheck");
        var gc = gcProp != null ? gcProp.objectReferenceValue as Transform : null;
        if (gc != null)
        {
            Undo.RecordObject(gc, "Groundcheck");
            gc.localPosition = Vector3.zero; // ayak hizasi
        }
    }

    // ---------------- Yardimcilar ----------------

    private static AnimationClip LoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) Debug.LogWarning("Animasyon bulunamadi: " + path);
        return clip;
    }

    private static AnimationClip CreateIdleClip(Sprite sprite)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        if (clip == null)
        {
            clip = new AnimationClip { frameRate = 12 };
            AssetDatabase.CreateAsset(clip, IdleClipPath);
        }

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = sprite }
        });
        SetLoop(clip, true);
        return clip;
    }

    private static void SetLoop(AnimationClip clip, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (settings.loopTime == loop) return;
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, float x, float y)
    {
        var state = sm.AddState(name, new Vector3(x, y));
        state.motion = motion;
        state.writeDefaultValues = false;
        return state;
    }

    private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = 0f; // sprite animasyonlarinda gecis yumusatmasi olmaz
        return t;
    }

    private static void ExitTransition(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 1f;
        t.hasFixedDuration = true;
        t.duration = 0f;
    }

    private static AnimatorStateTransition Cond(this AnimatorStateTransition t, AnimatorConditionMode mode, float threshold, string param)
    {
        t.AddCondition(mode, threshold, param);
        return t;
    }
}
