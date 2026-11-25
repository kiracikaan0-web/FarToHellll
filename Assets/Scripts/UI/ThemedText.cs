using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Yazinin fontunu ve rengini UI Theme'den alir.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Text))]
public class ThemedText : MonoBehaviour
{
    public enum Role { Title, Hint }

    [Tooltip("Bos birakilirsa varsayilan tema kullanilir")]
    [SerializeField] private UITheme theme;
    [SerializeField] private Role role = Role.Title;

    private void OnEnable() => Apply();
#if UNITY_EDITOR
    private void OnValidate() => Apply();
#endif

    public void Apply()
    {
        var t = theme != null ? theme : UITheme.Default;
        if (t == null) return;
        var text = GetComponent<Text>();
        var font = role == Role.Title ? (t.titleFont != null ? t.titleFont : t.buttonFont) : t.buttonFont;
        if (font != null) text.font = font;
        text.color = role == Role.Title ? t.titleColor : t.hintColor;
    }
}
