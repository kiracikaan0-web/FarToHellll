using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Ayarlar paneli. Open() ile acilir, GERI butonu ya da ESC / gamepad B ile kapanir.
/// Acikken arkadaki menu (Hide When Open) gizlenir; kapaninca secim eski butona doner.
/// Ana menude AYARLAR butonu acar. Kurulum: Tools > FarToHell > Ana Menu Kur
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private Selectable firstSelected;
    [Tooltip("Ayarlar acikken gizlenecek menu (baslik ve butonlar)")]
    [SerializeField] private CanvasGroup hideWhenOpen;
    [Tooltip("Kapaninca secilecek buton (AYARLAR)")]
    [SerializeField] private Selectable returnSelection;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;
    [SerializeField] private InputAction cancelAction = new InputAction("Cancel", InputActionType.Button);

    public bool IsOpen { get; private set; }
    /// <summary>Son kapandigi kare: ayni ESC basisi baska menuyu de kapatmasin diye (PauseMenu bakar).</summary>
    public int LastCloseFrame { get; private set; } = -1;
    private Coroutine routine;

    private void Awake()
    {
        if (cancelAction == null) cancelAction = new InputAction("Cancel", InputActionType.Button);
        if (cancelAction.bindings.Count == 0)
        {
            cancelAction.AddBinding("<Keyboard>/escape");
            cancelAction.AddBinding("<Gamepad>/buttonEast");
        }
        SetGroup(panel, 0f, false);
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void OnEnable() => cancelAction.Enable();
    private void OnDisable() => cancelAction.Disable();

    private void Update()
    {
        if (IsOpen && cancelAction.WasPressedThisFrame()) Close();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (panel != null) panel.gameObject.SetActive(true);
        foreach (var row in GetComponentsInChildren<SettingsRow>(true)) row.Refresh();
        Run(Transition(true));
        Select(firstSelected);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        LastCloseFrame = Time.frameCount;
        Run(Transition(false));
        Select(returnSelection);
    }

    /// <summary>Animasyonsuz aninda kapat (menu tamamen kapanirken).</summary>
    public void CloseImmediate()
    {
        if (routine != null) { StopCoroutine(routine); routine = null; }
        IsOpen = false;
        SetGroup(panel, 0f, false);
        if (panel != null) panel.gameObject.SetActive(false);
        SetGroup(hideWhenOpen, 1f, true);
    }

    private void Run(IEnumerator r)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(r);
    }

    private IEnumerator Transition(bool open)
    {
        // Arkadaki menu ve panel ayni anda yer degistirir
        SetGroup(hideWhenOpen, hideWhenOpen != null ? hideWhenOpen.alpha : 1f, !open);
        SetGroup(panel, panel != null ? panel.alpha : 0f, open);
        float t = 0f;
        float panelFrom = panel != null ? panel.alpha : 0f;
        float hideFrom = hideWhenOpen != null ? hideWhenOpen.alpha : 1f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / fadeDuration);
            if (panel != null) panel.alpha = Mathf.Lerp(panelFrom, open ? 1f : 0f, k);
            if (hideWhenOpen != null) hideWhenOpen.alpha = Mathf.Lerp(hideFrom, open ? 0f : 1f, k);
            yield return null;
        }
        if (panel != null)
        {
            panel.alpha = open ? 1f : 0f;
            if (!open) panel.gameObject.SetActive(false);
        }
        if (hideWhenOpen != null) hideWhenOpen.alpha = open ? 0f : 1f;
        routine = null;
    }

    private static void SetGroup(CanvasGroup g, float alpha, bool interactive)
    {
        if (g == null) return;
        g.alpha = alpha;
        g.interactable = interactive;
        g.blocksRaycasts = interactive;
    }

    private static void Select(Selectable s)
    {
        if (s == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }
}
