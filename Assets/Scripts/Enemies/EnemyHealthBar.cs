using UnityEngine;

/// <summary>
/// Dusmanin basinin ustunde kucuk can bari. Vurulunca gorunur, bir sure sonra kaybolur.
/// Renkler UI Theme'in HUD renklerinden gelir. Bar dusmanin alt objesi degildir (dusman donunce ters donmesin diye).
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyHealthBar : MonoBehaviour
{
    [Tooltip("Dusmanin ayagina gore konum")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 4.3f);
    [SerializeField] private Vector2 size = new Vector2(1.8f, 0.14f);
    [Tooltip("Vurulduktan sonra kac sn gorunsun (0 = hep gorunsun)")]
    [SerializeField, Min(0f)] private float showTime = 3f;
    [SerializeField, Min(0.01f)] private float fadeTime = 0.4f;
    [SerializeField, Min(0f)] private float chipDelay = 0.4f;
    [SerializeField, Min(0.01f)] private float chipSpeed = 0.8f;
    [SerializeField] private int sortingOrder = 20;

    private static Sprite pixel;
    private Health health;
    private Transform root;
    private SpriteRenderer back, chip, fill;
    private float visibleUntil;
    private float shownChip = 1f;
    private float chipWaitUntil;
    private Color backColor, chipColor, fillColor;

    private void Awake()
    {
        health = GetComponent<Health>();
        var t = UITheme.Default;
        backColor = t != null ? t.hudBarBack : new Color(0.05f, 0.01f, 0.02f, 0.75f);
        chipColor = t != null ? t.hudHealthChip : new Color(0.95f, 0.72f, 0.3f, 1f);
        fillColor = t != null ? t.hudHealth : new Color(0.7f, 0.07f, 0.05f, 1f);
        Build();
    }

    private void OnEnable() => health.Damaged += OnDamaged;
    private void OnDisable() => health.Damaged -= OnDamaged;

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }

    private void Build()
    {
        if (pixel == null)
            pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);

        root = new GameObject(name + "_HealthBar").transform;
        back = CreatePart("Back", 0.03f, sortingOrder);
        chip = CreatePart("Chip", 0f, sortingOrder + 1);
        fill = CreatePart("Fill", 0f, sortingOrder + 2);
        SetAlpha(0f);
    }

    private SpriteRenderer CreatePart(string partName, float pad, int order)
    {
        var go = new GameObject(partName);
        go.transform.SetParent(root, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = pixel;
        r.sortingOrder = order;
        go.transform.localPosition = new Vector3(-size.x * 0.5f - pad, 0f, 0f);
        go.transform.localScale = new Vector3(size.x + pad * 2f, size.y + pad * 2f, 1f);
        return r;
    }

    private void OnDamaged(DamageInfo info)
    {
        visibleUntil = Time.time + showTime;
        chipWaitUntil = Time.time + chipDelay;
    }

    private void LateUpdate()
    {
        if (root == null) return;
        // dusman buyutulunce bar da kafanin ustune cikar
        root.position = (Vector2)transform.position + offset * Mathf.Abs(transform.lossyScale.y);

        float hp = health.Normalized;
        if (hp > shownChip) shownChip = hp;
        else if (Time.time >= chipWaitUntil) shownChip = Mathf.MoveTowards(shownChip, hp, chipSpeed * Time.deltaTime);
        SetWidth(fill, hp);
        SetWidth(chip, shownChip);

        float alpha;
        if (health.IsDead) alpha = Mathf.MoveTowards(CurrentAlpha(), 0f, Time.deltaTime / fadeTime);
        else if (showTime <= 0f) alpha = 1f;
        else alpha = Mathf.Clamp01((visibleUntil - Time.time) / fadeTime);
        SetAlpha(alpha);
    }

    private void SetWidth(SpriteRenderer r, float value)
    {
        var s = r.transform.localScale;
        s.x = size.x * Mathf.Clamp01(value);
        r.transform.localScale = s;
    }

    private float CurrentAlpha() => fill != null ? fill.color.a / Mathf.Max(0.001f, fillColor.a) : 0f;

    private void SetAlpha(float a)
    {
        back.color = new Color(backColor.r, backColor.g, backColor.b, backColor.a * a);
        chip.color = new Color(chipColor.r, chipColor.g, chipColor.b, chipColor.a * a);
        fill.color = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * a);
    }
}
