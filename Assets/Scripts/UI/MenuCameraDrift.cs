using UnityEngine;

/// <summary>
/// Ana menude kamerayi yavasca saga kaydirir; parallax arka plan katmanlari boylece hareketli gorunur.
/// </summary>
public class MenuCameraDrift : MonoBehaviour
{
    [SerializeField] private float speed = 1.2f;
    [Tooltip("Hafif yukari-asagi salinim (birim)")]
    [SerializeField] private float bobAmount = 0.15f;
    [SerializeField] private float bobSpeed = 0.4f;

    private float baseY;

    private void Start() => baseY = transform.position.y;

    private void Update()
    {
        var p = transform.position;
        p.x += speed * Time.unscaledDeltaTime;
        p.y = baseY + Mathf.Sin(Time.unscaledTime * bobSpeed * Mathf.PI * 2f) * bobAmount;
        transform.position = p;
    }
}
