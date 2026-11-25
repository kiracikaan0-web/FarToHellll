using UnityEngine;

/// <summary>
/// Dusman mermisi (Skeleton2'nin kemigi): duz bir cizgide gider, donerek ucar.
/// Oyuncuya carpinca hasar verir ve kaybolur; zemine/duvara carpinca kaybolur.
/// Oyuncu dash atiyorsa (i-frame) icinden gecer.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float lifetime = 3f;
    [Tooltip("Saniyede donme acisi")]
    [SerializeField] private float spinSpeed = 720f;
    [SerializeField, Min(0f)] private float knockback = 6f;
    [Tooltip("Bu katmanlara carpinca kaybolur (zemin/duvar)")]
    [SerializeField] private LayerMask blockLayers = 1 << 6;
    [Tooltip("Duvar kontrolu yaricapi")]
    [SerializeField, Min(0.01f)] private float radius = 0.3f;

    private Vector2 velocity;
    private int damage;
    private GameObject owner;
    private float dieAt;
    private bool launched;

    public void Launch(Vector2 direction, float speed, int dmg, GameObject source)
    {
        velocity = direction.normalized * speed;
        damage = dmg;
        owner = source;
        dieAt = Time.time + lifetime;
        launched = true;
        if (direction.x > 0f) spinSpeed = -Mathf.Abs(spinSpeed); // gittigi yone dogru donsun
    }

    private void Update()
    {
        if (!launched) return;
        Vector2 pos = transform.position;
        Vector2 step = velocity * Time.deltaTime;

        // Duvar/zemin kontrolu (hizli giderken icinden gecmesin)
        var hit = Physics2D.CircleCast(pos, radius, velocity.normalized, step.magnitude, blockLayers);
        if (hit.collider != null && (owner == null || !hit.collider.transform.IsChildOf(owner.transform)))
        {
            Destroy(gameObject);
            return;
        }

        transform.position = pos + step;
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        if (Time.time >= dieAt) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!launched) return;
        if (owner != null && other.transform.IsChildOf(owner.transform)) return;
        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;
        var h = player.GetComponent<Health>();
        // Dash ile kacildiysa (hasar engellendiyse) kemik icinden gecip yoluna devam eder
        if (h != null && h.TakeDamage(new DamageInfo(damage, transform.position, knockback)))
            Destroy(gameObject);
    }
}
