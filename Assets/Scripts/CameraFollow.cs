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

    private Vector3 velocity;
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

        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}
