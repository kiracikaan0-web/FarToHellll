using UnityEngine;

/// <summary>
/// Icine giren ya da icinde duran Health'li objelere hasar verir: diken, lav, test bolgesi.
/// Objede "Is Trigger" acik bir Collider2D olmali.
/// Hizli ekleme: Tools > FarToHell > Test Hasar Bolgesi Ekle
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class DamageZone : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 10;
    [Tooltip("Iceride durulursa kac saniyede bir tekrar vursun (Health'in i-frame suresi de gecerli)")]
    [SerializeField, Min(0.05f)] private float repeatInterval = 0.6f;
    [Tooltip("Vurulani bolgenin merkezinden uzaga itme gucu")]
    [SerializeField, Min(0f)] private float knockback = 8f;
    [Tooltip("Sadece oyuncuya mi vursun? Kapaliysa dusmanlara da vurur.")]
    [SerializeField] private bool playerOnly = false;

    private float nextHitTime;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => TryHit(other, true);
    private void OnTriggerStay2D(Collider2D other) => TryHit(other, false);

    private void TryHit(Collider2D other, bool entering)
    {
        if (!entering && Time.time < nextHitTime) return;
        var health = other.GetComponentInParent<Health>();
        if (health == null) return;
        if (playerOnly && health.GetComponent<PlayerMovement>() == null) return;

        if (health.TakeDamage(new DamageInfo(damage, transform.position, knockback)))
            nextHitTime = Time.time + repeatInterval;
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.35f);
        Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
