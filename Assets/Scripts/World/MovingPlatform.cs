using UnityEngine;

/// <summary>
/// Iki nokta arasinda gidip gelen platform (asansor). Kinematic Rigidbody2D ile hareket eder;
/// ustundeki oyuncu ve dusmanlar tasinir.
/// Bolum kurucu A (alt durak) ve B (ust durak) noktalarini ayarlar.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private Vector2 pointA;
    [SerializeField] private Vector2 pointB;
    [SerializeField, Min(0.1f)] private float speed = 3.5f;
    [Tooltip("Duraklarda bekleme (sn)")]
    [SerializeField, Min(0f)] private float waitTime = 1.2f;

    private Rigidbody2D rb;
    private Vector2 target;
    private float waitUntil;
    private Vector2 lastPos;
    private Rigidbody2D rider;

    public void SetPoints(Vector2 a, Vector2 b)
    {
        pointA = a;
        pointB = b;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.position = pointA;
        transform.position = pointA;
        target = pointB;
        lastPos = pointA;
    }

    private void FixedUpdate()
    {
        Vector2 pos = rb.position;
        if (Time.time >= waitUntil)
        {
            Vector2 next = Vector2.MoveTowards(pos, target, speed * Time.fixedDeltaTime);
            rb.MovePosition(next);
            if ((next - target).sqrMagnitude < 0.0001f)
            {
                target = target == pointA ? pointB : pointA;
                waitUntil = Time.time + waitTime;
            }
            // Ustundekini yatayda da tasi (asansor dikey oldugu icin genelde 0)
            if (rider != null)
            {
                Vector2 delta = next - pos;
                if (Mathf.Abs(delta.x) > 0f) rider.position += new Vector2(delta.x, 0f);
            }
        }
        lastPos = pos;
    }

    private void OnCollisionStay2D(Collision2D c)
    {
        if (c.rigidbody != null && c.rigidbody.bodyType == RigidbodyType2D.Dynamic &&
            c.transform.position.y > transform.position.y)
            rider = c.rigidbody;
    }

    private void OnCollisionExit2D(Collision2D c)
    {
        if (c.rigidbody == rider) rider = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pointA, pointB);
        Gizmos.DrawWireSphere(pointA, 0.3f);
        Gizmos.DrawWireSphere(pointB, 0.3f);
    }
}
