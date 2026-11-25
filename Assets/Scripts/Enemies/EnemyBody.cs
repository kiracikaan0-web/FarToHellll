using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dusman govdesi:
///  - Oyuncu dusmanin kafasina inerse kayip yana duser (kafada durulamaz)
///  - Govde collider'i kaygandir (surtunmesiz), oyuncu kenarina tutunamaz
///  - Oyuncu dash/geri adim atarken bu dusmanin icinden gecebilir (PlayerMovement bu listeyi kullanir)
/// SkeletonEnemy bunu kendiliginden ekler; yeni dusman tiplerine de eklemen yeterli.
/// </summary>
[DisallowMultipleComponent]
public class EnemyBody : MonoBehaviour
{
    /// <summary>Sahnedeki tum dusman govdeleri.</summary>
    public static readonly List<EnemyBody> All = new List<EnemyBody>();

    [Tooltip("Kafaya basan oyuncunun yana kayma hizi")]
    [SerializeField, Min(0f)] private float headSlideSpeed = 7f;
    [Tooltip("Oyuncunun ayagi dusmanin tepesinden bu kadar asagidaysa hala 'kafada' sayilir")]
    [SerializeField, Min(0f)] private float headTolerance = 0.35f;

    private Collider2D[] colliders;
    private static PhysicsMaterial2D slippery;

    public Collider2D[] Colliders => colliders;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    private void Awake()
    {
        var list = new List<Collider2D>();
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) list.Add(c);
        colliders = list.ToArray();

        if (slippery == null) slippery = new PhysicsMaterial2D("EnemySlippery") { friction = 0f, bounciness = 0f };
        foreach (var c in colliders)
            if (c.sharedMaterial == null) c.sharedMaterial = slippery;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void OnCollisionEnter2D(Collision2D collision) => CheckHead(collision);
    private void OnCollisionStay2D(Collision2D collision) => CheckHead(collision);

    private void CheckHead(Collision2D collision)
    {
        var player = collision.collider.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        // Oyuncunun ayagi bizim tepemizin hizasinda mi?
        float myTop = float.MinValue;
        foreach (var c in colliders) if (c.enabled) myTop = Mathf.Max(myTop, c.bounds.max.y);
        float playerBottom = collision.collider.bounds.min.y;
        if (playerBottom < myTop - headTolerance) return;

        float dir = player.transform.position.x - transform.position.x;
        player.PushOffEnemyHead(dir, headSlideSpeed);
    }
}
