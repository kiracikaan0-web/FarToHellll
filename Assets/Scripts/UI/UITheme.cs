using UnityEngine;

/// <summary>
/// Oyunun tum UI gorunumu tek yerde: fontlar, renkler ve gorseller.
/// Tasarimci yeni bir gorsel verdiginde sadece bu dosyadaki ilgili kutuya surukleyip birakmak yeterli;
/// ana menu ve duraklatma menusu otomatik guncellenir.
///
/// Varsayilan tema: Assets/UI/Resources/UITheme.asset (Tools > FarToHell menuleri olusturur).
/// Rehber: Assets/UI/README_TASARIM.md
/// </summary>
[CreateAssetMenu(menuName = "FarToHell/UI Theme", fileName = "UITheme")]
public class UITheme : ScriptableObject
{
    public enum Slot
    {
        MenuTitle,      // Ana menu basligi (logo)
        MenuVignette,   // Ana menu kenar karartmasi
        PausePanel,     // Duraklatma menusunun arka paneli
        PauseTitle,     // Duraklatma basligi gorseli (bos birakilirsa yazi kullanilir)
        ButtonDeco,     // Secili butonun yanindaki sus (bos = elmas)
        ButtonBackground, // Buton arka plani (bos = gorunmez)
        HudBarFrame     // Can/stamina barinin cercevesi (bos = ince cizgi)
    }

    [Header("Fontlar")]
    public Font buttonFont;
    public Font titleFont;

    [Header("Butonlar")]
    public Color buttonNormal = new Color(0.80f, 0.72f, 0.64f, 0.85f);
    public Color buttonSelected = new Color(1f, 0.80f, 0.48f, 1f);
    public Color buttonOutline = new Color(0.25f, 0.03f, 0.02f, 0.9f);
    public Color decoColor = new Color(1f, 0.55f, 0.22f, 1f);
    [Range(1f, 1.5f)] public float buttonSelectedScale = 1.12f;

    [Header("Yazilar")]
    public Color titleColor = new Color(1f, 0.62f, 0.30f, 1f);
    public Color hintColor = new Color(0.80f, 0.65f, 0.55f, 0.55f);

    [Header("Duraklatma")]
    public Color pauseOverlay = new Color(0.03f, 0f, 0.01f, 0.65f);

    [Header("HUD (can / stamina barlari)")]
    public Color hudHealth = new Color(0.70f, 0.07f, 0.05f, 1f);
    public Color hudHealthChip = new Color(0.95f, 0.72f, 0.30f, 1f);
    public Color hudStamina = new Color(0.30f, 0.62f, 0.25f, 1f);
    public Color hudStaminaEmpty = new Color(0.85f, 0.25f, 0.15f, 1f);
    public Color hudBarBack = new Color(0.05f, 0.01f, 0.02f, 0.75f);
    public Color hudBarBorder = new Color(0.55f, 0.40f, 0.28f, 0.9f);

    [Header("Gorseller")]
    public Sprite menuTitle;
    public Sprite menuVignette;
    public Sprite pausePanel;
    public Sprite pauseTitle;
    public Sprite buttonDeco;
    public Sprite buttonBackground;
    public Sprite hudBarFrame;

    public Sprite Get(Slot slot)
    {
        switch (slot)
        {
            case Slot.MenuTitle: return menuTitle;
            case Slot.MenuVignette: return menuVignette;
            case Slot.PausePanel: return pausePanel;
            case Slot.PauseTitle: return pauseTitle;
            case Slot.ButtonDeco: return buttonDeco;
            case Slot.ButtonBackground: return buttonBackground;
            case Slot.HudBarFrame: return hudBarFrame;
        }
        return null;
    }

    private static UITheme cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => cached = null;

    /// <summary>Assets/UI/Resources/UITheme.asset</summary>
    public static UITheme Default
    {
        get
        {
            if (cached == null) cached = Resources.Load<UITheme>("UITheme");
            return cached;
        }
    }

#if UNITY_EDITOR
    // Tema degisince acik sahnedeki UI aninda guncellensin
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            foreach (var g in FindObjectsByType<ThemedGraphic>(FindObjectsInactive.Include)) g.Apply();
            foreach (var t in FindObjectsByType<ThemedText>(FindObjectsInactive.Include)) t.Apply();
            foreach (var b in FindObjectsByType<MenuButtonFX>(FindObjectsInactive.Include)) b.ApplyTheme();
            foreach (var h in FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include)) h.ApplyTheme();
        };
    }
#endif
}
