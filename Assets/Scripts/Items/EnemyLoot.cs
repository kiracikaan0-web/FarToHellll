using UnityEngine;

/// <summary>
/// Dusman olunce esya dusurur.
/// Bu component'i eklemesen de her dusman (EnemyBody olan her obje) varsayilan olarak 1 Altin dusurur.
/// Bir dusmana farkli miktar vermek istersen ona bu component'i ekleyip ayarla.
/// </summary>
public class EnemyLoot : MonoBehaviour
{
    public const int DefaultGold = 1;

    [Tooltip("Dusurdugu altin (min, max)")]
    [SerializeField] private Vector2Int gold = new Vector2Int(DefaultGold, DefaultGold);
    [Tooltip("Dusurme ihtimali (1 = her zaman dusurur)")]
    [SerializeField, Range(0f, 1f)] private float dropChance = 1f;

    public int RollGold()
    {
        if (Random.value > dropChance) return 0;
        return Random.Range(Mathf.Min(gold.x, gold.y), Mathf.Max(gold.x, gold.y) + 1);
    }

    // ---------------- Tum dusmanlar icin otomatik ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Health.AnyDied -= OnAnyDied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        Health.AnyDied -= OnAnyDied;
        Health.AnyDied += OnAnyDied;
    }

    private static void OnAnyDied(Health h)
    {
        if (h == null || h.GetComponent<PlayerMovement>() != null) return;
        var loot = h.GetComponent<EnemyLoot>();
        if (loot == null && h.GetComponent<EnemyBody>() == null) return;   // dusman degil

        int amount = loot != null ? loot.RollGold() : DefaultGold;
        if (amount <= 0) return;

        float k = Mathf.Abs(h.transform.lossyScale.y);
        Vector2 pos = (Vector2)h.transform.position + Vector2.up * (1.5f * k);
        // her altin ayri para olarak sacilsin (cok fazlaysa 10'a kadar grupla)
        int pieces = Mathf.Min(amount, 10);
        for (int i = 0; i < pieces; i++)
        {
            int part = amount / pieces + (i < amount % pieces ? 1 : 0);
            ItemPickup.Spawn(ItemDefinition.Gold, part, pos, Mathf.Clamp(k, 1f, 2f));
        }
    }
}
