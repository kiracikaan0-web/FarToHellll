using UnityEngine;

/// <summary>
/// Belirli araliklarla akan lav selalesi (krokideki "belirli araliklarla akiyor, hasar veriyor").
/// Dongu: kapali -> uyari (ince damla, hasar yok) -> akiyor (hasar verir) -> kapali ...
/// Collider ve DamageZone sadece "akiyor" asamasinda aciktir.
/// Bolum kurucu (Tools > FarToHell > Bolum 1 Olustur) boyunu tavandan zemine kadar ayarlar.
/// </summary>
[RequireComponent(typeof(BoxCollider2D), typeof(DamageZone))]
public class LavaFall : MonoBehaviour
{
    [Header("Zamanlama (sn)")]
    [SerializeField, Min(0f)] private float offTime = 2.4f;
    [SerializeField, Min(0f)] private float warnTime = 0.8f;
    [SerializeField, Min(0.1f)] private float onTime = 1.6f;
    [Tooltip("Ayni anda akmasinlar diye her selaleye farkli baslangic")]
    [SerializeField, Min(0f)] private float startOffset = 0f;

    [Header("Gorunum")]
    [SerializeField] private SpriteRenderer stream;
    [SerializeField, Min(0.1f)] private float width = 1.6f;
    [SerializeField, Min(0.1f)] private float length = 12f;
    [SerializeField, Range(0.05f, 1f)] private float warnWidth = 0.25f;

    private BoxCollider2D col;
    private float Cycle => offTime + warnTime + onTime;

    public void Setup(float newLength, float newWidth, float offset)
    {
        length = newLength;
        width = newWidth;
        startOffset = offset;
        ApplyShape(1f);
    }

    private void Awake()
    {
        col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        ApplyShape(1f);
    }

    private void ApplyShape(float widthScale)
    {
        if (col == null) col = GetComponent<BoxCollider2D>();
        // Nesnenin konumu selalenin tepesi; asagi dogru uzar
        col.size = new Vector2(width * 0.8f, length);
        col.offset = new Vector2(0f, -length * 0.5f);
        if (stream != null)
        {
            stream.drawMode = SpriteDrawMode.Tiled;
            stream.size = new Vector2(Mathf.Max(0.05f, width * widthScale), length);
            stream.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f);
        }
    }

    private void Update()
    {
        float t = Mathf.Repeat(Time.time + startOffset, Cycle);
        bool on;
        float visualWidth, alpha;
        if (t < offTime)
        {
            on = false; visualWidth = 0f; alpha = 0f;
        }
        else if (t < offTime + warnTime)
        {
            float k = (t - offTime) / warnTime;
            on = false; visualWidth = warnWidth; alpha = 0.35f + 0.35f * k;
        }
        else
        {
            float k = (t - offTime - warnTime) / onTime;
            on = true;
            // acilirken genisler, kapanirken incelir; hafif titreme
            float edge = Mathf.Min(1f, Mathf.Min(k, 1f - k) * 8f);
            visualWidth = Mathf.Lerp(warnWidth, 1f, edge) * (1f + Mathf.Sin(Time.time * 30f) * 0.04f);
            alpha = 1f;
        }

        col.enabled = on;
        if (stream != null)
        {
            stream.enabled = alpha > 0.01f;
            stream.size = new Vector2(Mathf.Max(0.05f, width * visualWidth), length);
            var c = stream.color; c.a = alpha; stream.color = c;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.5f);
        Gizmos.DrawWireCube(transform.position + Vector3.down * length * 0.5f, new Vector3(width, length, 0f));
    }
}
