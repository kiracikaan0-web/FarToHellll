using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Henuz yapilmamis seyler icin yer tutucu: satici, boss, esya, harita gecisi.
/// Level designer'in koydugu yerleri oyunda ve editorde gosterir; sistemi yazilinca yerine gercegi konur.
/// Hafifce parlar/sallanir. Oyuncu yaklasinca etiketi gorunur.
/// </summary>
public class LevelMarker : MonoBehaviour
{
    public enum Kind { Merchant, Boss, Item, MapExit }

    [SerializeField] private Kind kind;
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private GameObject label;
    [SerializeField, Min(0f)] private float labelDistance = 7f;

    private Transform player;
    private Vector3 iconBase;

    public Kind MarkerKind => kind;

    private void Awake()
    {
        if (icon != null) iconBase = icon.transform.localPosition;
        if (label != null) label.SetActive(false);
    }

    private void Update()
    {
        if (icon != null)
        {
            float t = Time.time;
            if (kind == Kind.Item)
                icon.transform.localPosition = iconBase + Vector3.up * Mathf.Sin(t * 3f) * 0.15f;
            var c = icon.color;
            c.a = 0.65f + 0.3f * Mathf.Sin(t * 4f);
            icon.color = c;
        }

        if (label == null) return;
        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p == null) return;
            player = p.transform;
        }
        label.SetActive(Vector2.Distance(player.position, transform.position) < labelDistance);
    }

    private void OnDrawGizmos()
    {
        Color c;
        switch (kind)
        {
            case Kind.Merchant: c = Color.green; break;
            case Kind.Boss: c = Color.red; break;
            case Kind.MapExit: c = new Color(0.4f, 0.6f, 1f); break;
            default: c = Color.yellow; break;
        }
        Gizmos.color = c;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, new Vector3(1.5f, 3f, 0f));
    }
}
