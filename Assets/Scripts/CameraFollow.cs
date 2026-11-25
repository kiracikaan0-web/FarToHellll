using UnityEngine;

/// <summary>
/// Kamerayi oyuncuyu yumusakca takip ettirir. Oyuncunun baktigi yone biraz onden bakar.
/// Main Camera'ya ekle, Target'a Player'i ver (bos birakirsan PlayerMovement'i kendisi bulur).
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [Tooltip("Hedefe gore kamera konumu (y: karakterin biraz ustune bak)")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 2f);
    [Tooltip("Takip yumusakligi (sn). Kucuk = daha siki takip")]
    [SerializeField] private float smoothTime = 0.15f;
    [Tooltip("Oyuncunun baktigi yone kac birim onden bakilsin")]
    [SerializeField] private float lookAhead = 2f;
    [SerializeField] private float lookAheadSmooth = 0.4f;

    [Header("Sinirlar (istege bagli)")]
    [SerializeField] private bool useMinY = false;
    [SerializeField] private float minY = 0f;

    [Header("Sarsinti")]
    [Tooltip("Sarsinti titresim hizi")]
    [SerializeField, Min(1f)] private float shakeFrequency = 30f;

    private Vector3 velocity;
    private Vector3 smoothPos;      // sarsintisiz kamera konumu
    private bool hasSmoothPos;

    // GameFeel.Shake ile eklenen sarsinti (tum kameralar ortak)
    private static float shakeStrength;
    private static float shakeEnd;
    private static float shakeDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetShake() { shakeStrength = 0f; shakeEnd = 0f; shakeDuration = 0f; }

    /// <summary>Kamerayi sars (guc: birim, sure: sn). Ust uste gelenlerde en gucluyu tutar.</summary>
    public static void AddShake(float strength, float duration)
    {
        float remaining = Mathf.Max(0f, shakeEnd - Time.unscaledTime);
        float currentStrength = shakeDuration > 0f ? shakeStrength * (remaining / shakeDuration) : 0f;
        if (strength < currentStrength) return;
        shakeStrength = strength;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeEnd = Time.unscaledTime + shakeDuration;
    }
    private float currentLookAhead;
    private float lookAheadVelocity;
    private PlayerMovement player;

    private void Start()
    {
        if (target == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p != null) target = p.transform;
        }
        if (target != null)
        {
            player = target.GetComponent<PlayerMovement>();
            transform.position = new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
        }
        smoothPos = transform.position;
        hasSmoothPos = true;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        float wantedLook = player != null ? player.FacingDirection * lookAhead : 0f;
        currentLookAhead = Mathf.SmoothDamp(currentLookAhead, wantedLook, ref lookAheadVelocity, lookAheadSmooth);

        Vector3 goal = new Vector3(
            target.position.x + offset.x + currentLookAhead,
            target.position.y + offset.y,
            transform.position.z);
        if (useMinY) goal.y = Mathf.Max(goal.y, minY);

        if (!hasSmoothPos) { smoothPos = transform.position; hasSmoothPos = true; }
        smoothPos = Vector3.SmoothDamp(smoothPos, goal, ref velocity, smoothTime);

        // Sarsinti: hit-stop sirasinda da devam etsin diye gercek zamanla
        Vector3 shake = Vector3.zero;
        float remaining = shakeEnd - Time.unscaledTime;
        if (remaining > 0f && shakeDuration > 0f)
        {
            float k = remaining / shakeDuration;          // 1 -> 0 azalir
            float s = shakeStrength * k * k;
            float t = Time.unscaledTime * shakeFrequency;
            shake = new Vector3((Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * s, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * s, 0f);
        }
        transform.position = smoothPos + shake;
    }
}
