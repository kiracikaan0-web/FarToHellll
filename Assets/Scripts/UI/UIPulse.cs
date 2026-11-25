using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI ogesine hafif "nefes alma" efekti verir (olcek ve parlaklik). Menu basligi icin.
/// </summary>
public class UIPulse : MonoBehaviour
{
    [SerializeField] private float scaleAmount = 0.015f;
    [SerializeField] private float brightnessAmount = 0.08f;
    [SerializeField] private float speed = 0.35f;

    private Graphic graphic;
    private Vector3 baseScale;

    private void Awake()
    {
        graphic = GetComponent<Graphic>();
        baseScale = transform.localScale;
    }

    private void Update()
    {
        float s = Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f);
        transform.localScale = baseScale * (1f + s * scaleAmount);
        if (graphic != null)
        {
            float b = 1f - brightnessAmount * 0.5f + s * brightnessAmount * 0.5f;
            graphic.color = new Color(b, b, b, graphic.color.a);
        }
    }
}
