using UnityEngine;

/// <summary>
/// Arka plan resmini kameraya kilitler ve ekrani kaplayacak sekilde olcekler (en-boy orani korunur, bos kenar kalmaz).
///
/// Editorde (Play kapaliyken) normal objeler gibi ayarlanir:
///  - Move araciyla (W) surukle: resim kameraya gore o kadar kayar (Offset'e yazilir)
///  - Scale araciyla (R) ya da Transform Scale ile buyut/kucult: Size'a yazilir (1 = ekrani tam kaplar)
/// Oyunda bu ayarlar korunur, resim kamerayla birlikte gider.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class CameraFillBackground : MonoBehaviour
{
    [Tooltip("Bos birakilirsa Main Camera kullanilir")]
    [SerializeField] private Camera targetCamera;
    [Tooltip("Kameradan ne kadar geride dursun (z)")]
    [SerializeField] private float depth = 20f;
    [Tooltip("Boyut: 1 = ekrani tam kaplar, 1.5 = yarim kat buyuk. Scale araciyla surukleyince de degisir.")]
    [SerializeField, Min(1f)] private float overscan = 1.02f;
    [Tooltip("Kameranin ortasina gore kayma (dunya birimi). Move araciyla surukleyince de degisir.")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    private SpriteRenderer sr;
    private Vector3 lastPos, lastScale;
    private bool placed;

    private void OnEnable() { sr = GetComponent<SpriteRenderer>(); placed = false; Fit(); }
    private void LateUpdate() => Fit();

    private void Fit()
    {
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || sr == null || sr.sprite == null) return;
        Vector2 s = sr.sprite.bounds.size;
        if (s.x <= 0f || s.y <= 0f) return;

        float h = cam.orthographic ? cam.orthographicSize * 2f : 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * cam.aspect;
        float cover = Mathf.Max(w / s.x, h / s.y);            // ekrani tam kaplayan olcek
        Vector3 c = cam.transform.position;

        // editorde elle tasindi / olceklendiyse onu ayar olarak al (yoksa her kare geri cekilirdi)
        if (!Application.isPlaying && placed)
        {
            bool changed = false;
            if ((transform.position - lastPos).sqrMagnitude > 1e-8f)
            { offset = new Vector2(transform.position.x - c.x, transform.position.y - c.y); changed = true; }
            if ((transform.localScale - lastScale).sqrMagnitude > 1e-8f)
            { overscan = Mathf.Max(1f, Mathf.Abs(transform.localScale.x) / cover); changed = true; }
#if UNITY_EDITOR
            if (changed) UnityEditor.EditorUtility.SetDirty(this);   // sahneyle birlikte kaydedilsin
#endif
        }

        // Not: cok kaydirirsan kenarda kamera rengi (koyu) gorunebilir; Size'i buyutursen kapanir
        float k = cover * Mathf.Max(1f, overscan);
        transform.position = new Vector3(c.x + offset.x, c.y + offset.y, c.z + depth);
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(k, k, 1f);
        lastPos = transform.position; lastScale = transform.localScale; placed = true;
    }
}
