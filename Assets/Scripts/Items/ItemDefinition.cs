using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyundaki bir esya turu (su an sadece Altin).
/// Ikon: Resources/Items/&lt;id&gt; adinda bir sprite varsa o kullanilir (tasarimci gorseli),
/// yoksa koddan cizilen gecici piksel ikon kullanilir.
/// Yeni esya eklemek icin asagidaki listeye bir satir ekle.
/// </summary>
public sealed class ItemDefinition
{
    public readonly string id;
    public readonly string displayName;
    public readonly int maxStack;
    private readonly System.Func<Sprite> placeholderIcon;
    private Sprite icon;

    private ItemDefinition(string id, string displayName, int maxStack, System.Func<Sprite> placeholderIcon)
    {
        this.id = id;
        this.displayName = displayName;
        this.maxStack = Mathf.Max(1, maxStack);
        this.placeholderIcon = placeholderIcon;
    }

    public Sprite Icon
    {
        get
        {
            if (icon == null) icon = Resources.Load<Sprite>("Items/" + id);
            if (icon == null && placeholderIcon != null) icon = placeholderIcon();
            return icon;
        }
    }

    // ---------------- Esya listesi ----------------

    public static readonly ItemDefinition Gold = new ItemDefinition("gold", "Altın", 9999, PlaceholderIcons.Coin);

    private static readonly Dictionary<string, ItemDefinition> all = new Dictionary<string, ItemDefinition>
    {
        { Gold.id, Gold },
    };

    public static ItemDefinition Get(string id) => id != null && all.TryGetValue(id, out var d) ? d : null;
}

/// <summary>Tasarimci gorseli gelene kadar koddan cizilen piksel ikonlar.</summary>
public static class PlaceholderIcons
{
    private static Sprite coin;

    /// <summary>16x16 piksel altin para.</summary>
    public static Sprite Coin()
    {
        if (coin != null) return coin;
        const int n = 16;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CoinIcon" };
        Color clear = new Color(0, 0, 0, 0);
        Color outline = new Color32(70, 38, 8, 255);
        Color shade = new Color32(176, 112, 22, 255);
        Color body = new Color32(232, 176, 44, 255);
        Color light = new Color32(255, 226, 120, 255);
        Color shine = new Color32(255, 250, 215, 255);
        var px = new Color[n * n];
        Vector2 c = new Vector2(7.5f, 7.5f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x, y);
                float d = Vector2.Distance(p, c);
                Color col = clear;
                if (d <= 7.4f) col = outline;
                if (d <= 6.4f)
                {
                    // asagi-sag golgeli, yukari-sol aydinlik govde
                    float lightness = Vector2.Dot((p - c).normalized, new Vector2(-0.7f, 0.7f));
                    col = lightness > 0.35f ? light : (lightness < -0.45f ? shade : body);
                    if (d > 4.6f && d <= 5.4f) col = shade;          // ic halka
                    if (d <= 4.6f) col = lightness > 0.2f ? light : body;
                }
                px[y * n + x] = col;
            }
        // ortadaki isaret (dikey cizgi) ve parlama
        for (int y = 5; y <= 10; y++) px[y * n + 7] = shade;
        px[12 * n + 4] = shine; px[11 * n + 4] = shine; px[12 * n + 5] = shine;
        tex.SetPixels(px);
        tex.Apply();
        coin = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 16f);
        coin.name = "CoinIcon";
        return coin;
    }
}
