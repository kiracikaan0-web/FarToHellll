using UnityEngine;

/// <summary>
/// Yerdeki toplanabilir esya (dusmandan dusen altin vb.).
/// Dusunce havaya sicrar, yere konar, hafifce suzulur; oyuncu yaklasinca ona dogru ucup cantaya girer.
/// Canta doluysa yerde kalmaya devam eder.
/// Olusturmak icin: ItemPickup.Spawn(ItemDefinition.Gold, 1, konum)
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private string itemId = "gold";
    [SerializeField, Min(1)] private int amount = 1;
    [Tooltip("Yerdeki boyutu (dunya birimi)")]
    [SerializeField, Min(0.05f)] private float size = 0.8f;
    [Tooltip("Oyuncu bu mesafeye gelince esya ona dogru ucar")]
    [SerializeField, Min(0f)] private float magnetRadius = 3.5f;
    [SerializeField, Min(0f)] private float magnetSpeed = 18f;
    [Tooltip("Dustukten sonra bu sure boyunca toplanamaz (sicrayisi gorulsun)")]
    [SerializeField, Min(0f)] private float pickupDelay = 0.45f;
    [SerializeField] private LayerMask groundLayer = 1 << 6;
    [SerializeField] private float gravity = 30f;

    private ItemDefinition item;
    private SpriteRenderer sr;
    private Transform player;
    private Vector2 velocity;
    private bool grounded, magnet;
    private float born;
    private float bobPhase;
    private Vector3 restPos;
    private float spawnY;

    public static ItemPickup Spawn(ItemDefinition item, int amount, Vector2 position, float sizeScale = 1f)
    {
        if (item == null || amount <= 0) return null;
        var go = new GameObject("Pickup_" + item.id);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        var p = go.AddComponent<ItemPickup>();   // Awake burada calisir
        p.itemId = item.id;
        p.amount = amount;
        p.size *= Mathf.Max(0.1f, sizeScale);
        p.magnetRadius *= Mathf.Max(0.1f, sizeScale);
        p.velocity = new Vector2(Random.Range(-3f, 3f), Random.Range(9f, 12f)) * Mathf.Sqrt(Mathf.Max(0.1f, sizeScale));
        p.Setup();
        return p;
    }

    private void Awake() { born = Time.time; spawnY = transform.position.y; }
    private void Start() { if (sr == null) Setup(); }

    private void Setup()
    {
        item = ItemDefinition.Get(itemId);
        if (item == null) { Destroy(gameObject); return; }
        sr = gameObject.GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = item.Icon;
        sr.sortingOrder = 40;
        if (sr.sprite != null)
        {
            float w = Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y);
            if (w > 0.0001f) transform.localScale = Vector3.one * (size / w);
        }
        bobPhase = Random.value * 10f;
    }

    private void Update()
    {
        if (item == null) return;
        float dt = Time.deltaTime;
        if (player == null)
        {
            var pm = FindAnyObjectByType<PlayerMovement>();
            if (pm != null) player = pm.transform;
        }

        Vector2 target = player != null ? (Vector2)player.position + Vector2.up * (1.6f * Mathf.Abs(player.lossyScale.y)) : Vector2.zero;
        bool canTake = Time.time - born >= pickupDelay && player != null && Inventory.HasRoomFor(item);

        if (canTake && !magnet && Vector2.Distance(transform.position, target) <= magnetRadius) magnet = true;
        if (magnet && !canTake) { magnet = false; grounded = false; velocity = Vector2.zero; }

        if (magnet)
        {
            Vector2 pos = Vector2.MoveTowards(transform.position, target, magnetSpeed * dt);
            transform.position = pos;
            if (Vector2.Distance(pos, target) < 0.35f) Collect();
            return;
        }

        if (!grounded) Fall(dt);
        else
        {
            // yerde hafifce suzulme
            float bob = (Mathf.Sin((Time.time + bobPhase) * 3f) * 0.5f + 0.5f) * size * 0.18f;
            transform.position = restPos + Vector3.up * bob;
        }
        // parilti: hafif nabiz
        if (sr != null)
        {
            float glow = 0.85f + 0.15f * Mathf.Sin((Time.time + bobPhase) * 5f);
            sr.color = new Color(glow + 0.15f, glow + 0.1f, glow, 1f);
        }
    }

    private void Fall(float dt)
    {
        velocity.y -= gravity * dt;
        Vector2 pos = transform.position;
        Vector2 step = velocity * dt;
        float half = size * 0.5f;

        // yatay: duvara carpinca dur
        if (Mathf.Abs(step.x) > 0f)
        {
            var hx = Physics2D.Raycast(pos, new Vector2(Mathf.Sign(step.x), 0f), Mathf.Abs(step.x) + half, groundLayer);
            if (hx.collider != null) { step.x = 0f; velocity.x = 0f; }
        }
        // dikey: yere konunca dur
        if (step.y < 0f)
        {
            var hy = Physics2D.Raycast(pos, Vector2.down, -step.y + half, groundLayer);
            if (hy.collider != null)
            {
                restPos = new Vector3(pos.x + step.x, hy.point.y + half, 0f);
                transform.position = restPos;
                grounded = true;
                velocity = Vector2.zero;
                return;
            }
        }
        else if (step.y > 0f)
        {
            var hu = Physics2D.Raycast(pos, Vector2.up, step.y + half, groundLayer);
            if (hu.collider != null) { step.y = 0f; velocity.y = 0f; }
        }
        transform.position = pos + step;
        // haritanin cok altina dustuyse yok et
        if (transform.position.y < spawnY - 200f) Destroy(gameObject);
    }

    private void Collect()
    {
        int left = Inventory.Add(item, amount);
        if (left < amount) GameFeel.PlaySound("Pickup", 0.8f);
        if (left <= 0) { Destroy(gameObject); return; }
        amount = left;          // canta doldu: kalan yerde kalsin
        magnet = false;
        grounded = false;
        velocity = new Vector2(0f, 4f);
    }
}
