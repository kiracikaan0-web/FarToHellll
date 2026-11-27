using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dusman collider'lari iki parca:
///  - Fiziksel govde: ortadaki kucuk kapsul (eskisi gibi). Oyuncu ona carpar, ustune binemez, dusmani itip kaldiramaz.
///  - Hasar alani: sprite'in tum dis hattini (kafa, kollar, sac, boynuzlar, bacaklar) saran cokgen, TRIGGER.
///    Kilic bu alanin herhangi bir yerine degince hasar verir (PlayerCombat trigger'lari da tarar); fizik etkilemez.
/// Noktalar bekleme karesinin dis hattindan cikarildi; birim, ayak noktasina (pivot) gore. Obje donunce collider da doner.
///
/// Derlemeden sonra bir kez Skeleton2 ve Demon prefablarina uygulanir. Elle: Tools > FarToHell > Dusman Collider'larini Tum Govdeye Yay
/// EnemySetup / DemonWhipSetup prefabi bastan kurarken de bunu kullanir.
/// </summary>
public static class EnemyColliderShapes
{
    public static readonly Vector2[] Skeleton2 = { new Vector2(-1.573f, 3.644f), new Vector2(-1.058f, 3.644f), new Vector2(-0.601f, 3.358f), new Vector2(-0.430f, 2.958f), new Vector2(-0.458f, 2.530f), new Vector2(-0.601f, 2.301f), new Vector2(-0.401f, 2.244f), new Vector2(-0.087f, 2.473f), new Vector2(0.142f, 2.473f), new Vector2(0.227f, 2.387f), new Vector2(0.227f, 2.244f), new Vector2(0.513f, 2.073f), new Vector2(0.427f, 1.758f), new Vector2(0.170f, 1.787f), new Vector2(0.142f, 1.415f), new Vector2(0.027f, 1.273f), new Vector2(0.370f, 1.215f), new Vector2(0.427f, 0.758f), new Vector2(0.542f, 0.530f), new Vector2(0.713f, 0.501f), new Vector2(0.742f, 0.273f), new Vector2(0.942f, 0.215f), new Vector2(0.942f, 0.000f), new Vector2(0.399f, 0.000f), new Vector2(0.227f, 0.158f), new Vector2(0.170f, 0.558f), new Vector2(0.027f, 0.673f), new Vector2(-0.116f, 0.587f), new Vector2(-0.373f, 0.615f), new Vector2(-0.487f, 0.387f), new Vector2(-0.458f, 0.158f), new Vector2(-0.573f, 0.101f), new Vector2(-0.601f, 0.000f), new Vector2(-1.173f, 0.000f), new Vector2(-1.201f, 0.158f), new Vector2(-0.973f, 0.273f), new Vector2(-0.973f, 0.473f), new Vector2(-0.801f, 0.530f), new Vector2(-0.687f, 0.701f), new Vector2(-0.687f, 1.158f), new Vector2(-1.116f, 1.101f), new Vector2(-1.344f, 1.444f), new Vector2(-1.716f, 1.330f), new Vector2(-1.801f, 1.187f), new Vector2(-2.030f, 1.187f), new Vector2(-2.258f, 1.473f), new Vector2(-2.516f, 1.644f), new Vector2(-3.173f, 1.701f), new Vector2(-3.230f, 1.930f), new Vector2(-3.487f, 1.901f), new Vector2(-3.516f, 2.044f), new Vector2(-3.744f, 2.187f), new Vector2(-3.687f, 2.444f), new Vector2(-3.487f, 2.530f), new Vector2(-3.087f, 2.530f), new Vector2(-2.858f, 2.415f), new Vector2(-2.744f, 2.244f), new Vector2(-2.601f, 2.244f), new Vector2(-2.573f, 2.044f), new Vector2(-2.173f, 1.815f), new Vector2(-1.944f, 1.844f), new Vector2(-2.258f, 2.473f), new Vector2(-2.258f, 3.015f), new Vector2(-1.944f, 3.473f) };
    public static readonly Vector2[] Demon = { new Vector2(-0.271f, 3.593f), new Vector2(-0.071f, 3.593f), new Vector2(-0.157f, 3.164f), new Vector2(0.186f, 3.193f), new Vector2(0.329f, 3.336f), new Vector2(0.557f, 3.393f), new Vector2(0.843f, 3.336f), new Vector2(0.929f, 3.136f), new Vector2(0.700f, 3.136f), new Vector2(0.614f, 3.050f), new Vector2(0.814f, 2.907f), new Vector2(1.100f, 2.421f), new Vector2(1.443f, 2.364f), new Vector2(1.443f, 2.221f), new Vector2(1.214f, 2.136f), new Vector2(1.300f, 1.936f), new Vector2(1.186f, 1.907f), new Vector2(1.129f, 1.536f), new Vector2(1.014f, 1.536f), new Vector2(1.129f, 1.364f), new Vector2(1.300f, 1.307f), new Vector2(1.329f, 1.079f), new Vector2(1.243f, 0.993f), new Vector2(1.100f, 0.993f), new Vector2(0.814f, 1.307f), new Vector2(0.643f, 1.250f), new Vector2(0.643f, 1.107f), new Vector2(0.757f, 1.021f), new Vector2(0.871f, 0.679f), new Vector2(0.843f, 0.479f), new Vector2(1.071f, 0.307f), new Vector2(1.071f, 0.164f), new Vector2(0.929f, 0.000f), new Vector2(0.643f, 0.000f), new Vector2(0.643f, 0.107f), new Vector2(0.729f, 0.164f), new Vector2(0.443f, 0.307f), new Vector2(0.386f, 0.421f), new Vector2(0.300f, 0.421f), new Vector2(0.271f, 0.279f), new Vector2(0.300f, 0.000f), new Vector2(-0.157f, 0.000f), new Vector2(-0.186f, 0.079f), new Vector2(-0.014f, 0.164f), new Vector2(-0.043f, 0.393f), new Vector2(-0.243f, 0.479f), new Vector2(-0.271f, 0.707f), new Vector2(-0.557f, 0.764f), new Vector2(-0.586f, 1.107f), new Vector2(-0.414f, 1.050f), new Vector2(-0.100f, 1.107f), new Vector2(-0.014f, 1.336f), new Vector2(-0.386f, 1.450f), new Vector2(-0.471f, 1.250f), new Vector2(-0.614f, 1.307f), new Vector2(-0.671f, 1.221f), new Vector2(-0.786f, 1.221f), new Vector2(-0.814f, 1.364f), new Vector2(-0.671f, 1.793f), new Vector2(-0.929f, 1.764f), new Vector2(-0.986f, 1.793f), new Vector2(-0.929f, 1.964f), new Vector2(-1.100f, 1.993f), new Vector2(-1.100f, 2.107f), new Vector2(-0.986f, 2.136f), new Vector2(-0.786f, 2.393f), new Vector2(-0.757f, 2.764f), new Vector2(-0.557f, 2.993f), new Vector2(-0.443f, 3.479f) };

    /// <summary>Boss'un temas hasari alani: govde, kirbacli kol ve kirbac HARIC (kirbac sadece saldirida vurur).</summary>
    public static readonly Vector2[] DemonContact = { new Vector2(-0.271f, 3.593f), new Vector2(-0.071f, 3.593f), new Vector2(-0.157f, 3.164f), new Vector2(0.186f, 3.193f), new Vector2(0.329f, 3.336f), new Vector2(0.557f, 3.393f), new Vector2(0.843f, 3.336f), new Vector2(0.929f, 3.136f), new Vector2(0.700f, 3.136f), new Vector2(0.614f, 3.050f), new Vector2(0.814f, 2.907f), new Vector2(1.100f, 2.421f), new Vector2(1.443f, 2.364f), new Vector2(1.443f, 2.221f), new Vector2(1.214f, 2.136f), new Vector2(1.300f, 1.936f), new Vector2(1.186f, 1.907f), new Vector2(1.129f, 1.536f), new Vector2(1.014f, 1.536f), new Vector2(1.129f, 1.364f), new Vector2(1.300f, 1.307f), new Vector2(1.329f, 1.079f), new Vector2(1.243f, 0.993f), new Vector2(1.100f, 0.993f), new Vector2(0.814f, 1.307f), new Vector2(0.643f, 1.250f), new Vector2(0.643f, 1.107f), new Vector2(0.757f, 1.021f), new Vector2(0.871f, 0.679f), new Vector2(0.843f, 0.479f), new Vector2(1.071f, 0.307f), new Vector2(1.071f, 0.164f), new Vector2(0.929f, 0.000f), new Vector2(0.643f, 0.000f), new Vector2(0.643f, 0.107f), new Vector2(0.729f, 0.164f), new Vector2(0.443f, 0.307f), new Vector2(0.386f, 0.421f), new Vector2(0.300f, 0.421f), new Vector2(0.271f, 0.279f), new Vector2(0.300f, 0.000f), new Vector2(-0.157f, 0.000f), new Vector2(-0.186f, 0.079f), new Vector2(-0.014f, 0.164f), new Vector2(-0.071f, 0.621f), new Vector2(0.214f, 0.936f), new Vector2(0.014f, 0.879f), new Vector2(-0.014f, 1.336f), new Vector2(-0.243f, 1.364f), new Vector2(-0.329f, 1.450f), new Vector2(-0.414f, 1.421f), new Vector2(-0.471f, 1.250f), new Vector2(-0.614f, 1.307f), new Vector2(-0.671f, 1.221f), new Vector2(-0.786f, 1.221f), new Vector2(-0.814f, 1.364f), new Vector2(-0.671f, 1.793f), new Vector2(-0.929f, 1.764f), new Vector2(-0.986f, 1.793f), new Vector2(-0.929f, 1.964f), new Vector2(-1.100f, 1.993f), new Vector2(-1.100f, 2.107f), new Vector2(-0.986f, 2.136f), new Vector2(-0.786f, 2.393f), new Vector2(-0.757f, 2.764f), new Vector2(-0.557f, 2.993f), new Vector2(-0.443f, 3.479f) };

    private const string SkeletonPrefab = "Assets/Prefabs/Enemies/Skeleton2.prefab";
    private const string DemonPrefab = "Assets/Prefabs/Enemies/Demon.prefab";
    private const string AutoKey = "FarToHell.EnemyHurtbox.v3";

    public static void ApplySkeleton2(GameObject root) => Apply(root, Skeleton2, new Vector2(1.9f, 3.4f), new Vector2(-0.3f, 1.75f));
    public static void ApplyDemon(GameObject root)
    {
        Apply(root, Demon, new Vector2(1.2f, 3.3f), new Vector2(0f, 1.65f));
        ApplyContactArea(root, DemonContact);
    }

    /// <summary>
    /// "ContactArea" alt objesi: temas hasari sadece buraya degince verilir (EnemyBody bunu kendisi bulur).
    /// Boss'ta kirbacli kol bu alanin disinda: elindeki kirbaca degmek hasar vermez.
    /// </summary>
    public static void ApplyContactArea(GameObject root, Vector2[] pts)
    {
        var t = root.transform.Find(EnemyBody.ContactAreaName);
        GameObject go = t != null ? t.gameObject : new GameObject(EnemyBody.ContactAreaName);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        go.layer = root.layer;
        var poly = go.GetComponent<PolygonCollider2D>();
        if (poly == null) poly = go.AddComponent<PolygonCollider2D>();
        poly.isTrigger = true;
        poly.pathCount = 1;
        poly.SetPath(0, pts);
    }

    /// <summary>Fiziksel govde = kapsul (yoksa eklenir, olcusu geri getirilir); tum govdeyi saran cokgen = sadece hasar alani (trigger).</summary>
    public static void Apply(GameObject root, Vector2[] pts, Vector2 bodySize, Vector2 bodyOffset)
    {
        var cap = root.GetComponent<CapsuleCollider2D>();
        if (cap == null) cap = root.AddComponent<CapsuleCollider2D>();
        cap.isTrigger = false;
        cap.direction = CapsuleDirection2D.Vertical;
        cap.size = bodySize;
        cap.offset = bodyOffset;

        var poly = root.GetComponent<PolygonCollider2D>();
        if (poly == null) poly = root.AddComponent<PolygonCollider2D>();
        poly.isTrigger = true;          // sadece kilic vuruslari icin: carpismaz, itmez
        poly.offset = Vector2.zero;
        poly.pathCount = 1;
        poly.SetPath(0, pts);
    }

    [InitializeOnLoadMethod]
    private static void AutoApply()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(AutoKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            int n = ApplyToPrefabs();
            if (n == 0) return;
            EditorPrefs.SetBool(AutoKey, true);
            Debug.Log("[Collider] " + n + " dusman prefabinin kilic tum govdeye vurur; boss'un temas hasari kirbacli kolu saymaz (Skeleton2, Demon).");
        };
    }

    [MenuItem("Tools/FarToHell/Dusman Hasar Alanini Tum Govdeye Yay")]
    public static void Menu()
    {
        int n = ApplyToPrefabs();
        EditorUtility.DisplayDialog("Collider", n + " dusman prefabi guncellendi: kilic tum govdeye vurur, fiziksel govde ortadaki kapsul.\nSahnedeki dusmanlar prefabi kullandigi icin onlar da guncellenir.", "Tamam");
    }

    private static int ApplyToPrefabs()
    {
        int n = 0;
        foreach (var (path, apply) in new (string, System.Action<GameObject>)[] { (SkeletonPrefab, ApplySkeleton2), (DemonPrefab, ApplyDemon) })
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try { apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); n++; }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return n;
    }
}