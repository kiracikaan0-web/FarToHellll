using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Image'in gorselini UI Theme'den alir. Tasarimci temadaki gorseli degistirince burasi da degisir.
/// "Show When Empty": temada gorsel yoksa bunun yerine gosterilecek obje (ornegin duz yazi basligi).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ThemedGraphic : MonoBehaviour
{
    [Tooltip("Bos birakilirsa varsayilan tema (Assets/UI/Resources/UITheme) kullanilir")]
    [SerializeField] private UITheme theme;
    [SerializeField] private UITheme.Slot slot;
    [Tooltip("Temada bu gorsel yoksa Image gizlensin mi?")]
    [SerializeField] private bool hideIfEmpty = false;
    [Tooltip("Temada gorsel yoksa gosterilecek, varsa gizlenecek obje (istege bagli)")]
    [SerializeField] private GameObject showWhenEmpty;

    private void OnEnable() => Apply();
#if UNITY_EDITOR
    private void OnValidate() => Apply();
#endif

    public void Apply()
    {
        var t = theme != null ? theme : UITheme.Default;
        if (t == null) return;
        var img = GetComponent<Image>();
        var sprite = t.Get(slot);
        bool has = sprite != null;

        if (has)
        {
            img.sprite = sprite;
            // 9-slice kenarlari olan gorseller (cerceve, panel) bozulmadan uzasin
            if (sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        }
        if (hideIfEmpty) img.enabled = has;
        if (showWhenEmpty != null) showWhenEmpty.SetActive(!has);
    }
}
