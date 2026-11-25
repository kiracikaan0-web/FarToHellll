using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESC (veya gamepad Start) ile acilan duraklatma menusu: Devam Et, Ana Menu, Cikis.
/// Oyun durur (Time.timeScale = 0), muzik kisilir, oyuncu kontrolleri kilitlenir.
/// Prefab: Assets/Prefabs/UI/PauseMenu.prefab — her oyun sahnesine bir tane koymak yeterli.
/// Kurulum: Tools > FarToHell > Duraklatma Menusu Kur
/// </summary>
public class PauseMenu : MonoBehaviour
{
    /// <summary>Oyun su an duraklatilmis mi?</summary>
    public static bool IsPaused { get; private set; }

    /// <summary>
    /// Oyuncu scriptleri bu true iken input okumamali. Menu kapandigi karede de true kalir;
    /// boylece "Devam Et"e tiklayan fare tusu karakterin saldirmasina sebep olmaz.
    /// </summary>
    public static bool BlocksGameplayInput => IsPaused || Time.frameCount <= resumeFrame;
    private static int resumeFrame = -1;

    [SerializeField] private CanvasGroup root;
    [SerializeField] private Button firstSelected;
    [Tooltip("Ana Menu butonunun acacagi sahne (Build Settings'te ekli olmali)")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private float fadeDuration = 0.15f;
    [Tooltip("Duraklatildiginda genel ses seviyesi")]
    [SerializeField, Range(0f, 1f)] private float pausedVolume = 0.4f;
    [SerializeField] private InputAction pauseAction = new InputAction("Pause", InputActionType.Button);
    [Tooltip("Duraklatma icindeki ayarlar paneli (kurulum menusu baglar)")]
    [SerializeField] private SettingsMenu settings;

    private Coroutine fadeRoutine;
    private bool leaving;

    /// <summary>Resources icindeki prefab: Assets/UI/Resources/PauseMenu.prefab</summary>
    public const string ResourcePath = "PauseMenu";

    // "Enter Play Mode Options" ile Domain Reload kapaliyken static degerler bir onceki Play'den
    // kalabilir; her Play basinda sifirla.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsPaused = false;
        resumeFrame = -1;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Duraklatma menusu oyuncu olan her sahneye otomatik eklenir; sahnelere elle koymaya gerek yok.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitAutoSpawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SpawnIfNeeded();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SpawnIfNeeded();

    private static void SpawnIfNeeded()
    {
        if (FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<PlayerMovement>() == null) return; // sadece oyun sahnelerinde (ana menude degil)

        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("PauseMenu: Assets/UI/Resources/PauseMenu.prefab bulunamadi. Tools > FarToHell > Duraklatma Menusu Kur ile olustur.");
            return;
        }
        var go = Instantiate(prefab);
        go.name = prefab.name;
    }

    private void Awake()
    {
        if (pauseAction == null) pauseAction = new InputAction("Pause", InputActionType.Button);
        if (pauseAction.bindings.Count == 0)
        {
            pauseAction.AddBinding("<Keyboard>/escape");
            pauseAction.AddBinding("<Gamepad>/start");
        }

        EnsureEventSystem();
        IsPaused = false;
        SetVisible(false, true);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private void OnEnable() => pauseAction.Enable();

    private void OnDisable()
    {
        pauseAction.Disable();
        if (IsPaused) RestoreGame();
    }

    private void Update()
    {
        if (leaving) return;
        if (pauseAction.WasPressedThisFrame())
        {
            // Ayarlar aciksa ESC once ayarlari kapatir (SettingsMenu kendisi yapar), oyuna donmez
            if (settings != null && (settings.IsOpen || settings.LastCloseFrame == Time.frameCount)) return;
            if (IsPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        if (IsPaused || leaving) return;
        IsPaused = true;
        Time.timeScale = 0f;
        GameSettings.SetMasterDuck(pausedVolume); // ayarlardaki genel sesin uzerine kisar
        SetVisible(true, false);

        if (firstSelected != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }
    }

    public void Resume()
    {
        if (!IsPaused || leaving) return;
        if (settings != null) settings.CloseImmediate();
        RestoreGame();
        SetVisible(false, false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void GoToMainMenu()
    {
        if (leaving) return;
        if (!Application.CanStreamedLevelBeLoaded(mainMenuScene))
        {
            Debug.LogError($"PauseMenu: '{mainMenuScene}' sahnesi Build Settings'te yok. Tools > FarToHell > Ana Menu Kur ile olustur.", this);
            return;
        }
        leaving = true;
        StartCoroutine(LeaveToMenu());
    }

    public void QuitGame()
    {
        if (leaving) return;
        leaving = true;
#if UNITY_EDITOR
        RestoreGame();
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LeaveToMenu()
    {
        yield return FadeRoot(root != null ? root.alpha : 1f, 0f, fadeDuration);
        RestoreGame();
        SceneManager.LoadScene(mainMenuScene);
    }

    private void RestoreGame()
    {
        IsPaused = false;
        resumeFrame = Time.frameCount;
        Time.timeScale = 1f;
        GameSettings.SetMasterDuck(1f);
    }

    private void SetVisible(bool visible, bool instant)
    {
        if (root == null) return;
        root.interactable = visible;
        root.blocksRaycasts = visible;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        if (instant || !isActiveAndEnabled)
        {
            root.alpha = visible ? 1f : 0f;
            root.gameObject.SetActive(visible);
            return;
        }
        root.gameObject.SetActive(true);
        fadeRoutine = StartCoroutine(FadeRoot(root.alpha, visible ? 1f : 0f, fadeDuration, !visible));
    }

    private IEnumerator FadeRoot(float from, float to, float duration, bool deactivateAtEnd = false)
    {
        if (root == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            root.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        root.alpha = to;
        if (deactivateAtEnd) root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (IsPaused) RestoreGame();
    }
}
