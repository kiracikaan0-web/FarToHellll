using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ekran mesajlari ve gecisler:
///  - Oyuncu olunce: kisa bekleme, ekran kararir, ortada "OLDUN" yazisi belirir, sonra son kontrol noktasinda dogulur
///  - Kontrol noktasi yakilinca: "ATES YAKILDI" bandi
///  - Dinlenince / dogunca: siyaha kararip acilma
/// Renk, font ve (istege bagli) "OLDUN" gorseli UI Theme'den gelir.
/// Prefab: Assets/UI/Resources/DeathScreen.prefab — Player olan her sahneye otomatik eklenir.
/// Kurulum: Tools > FarToHell > Olum Ekrani Kur
/// </summary>
public class DeathScreen : MonoBehaviour
{
    public const string ResourcePath = "DeathScreen";
    public static DeathScreen Instance { get; private set; }

    [Header("Baglantilar (kurulum menusu doldurur)")]
    [SerializeField] private CanvasGroup deathGroup;
    [SerializeField] private Image band;
    [SerializeField] private Text deathText;
    [SerializeField] private Image deathImage;
    [SerializeField] private CanvasGroup bannerGroup;
    [SerializeField] private Text bannerText;
    [SerializeField] private CanvasGroup fader;

    [Header("Olum")]
    [SerializeField] private string deathMessage = "ÖLDÜN";
    [Tooltip("Oldukten sonra yazi gelmeden once bekleme (sn)")]
    [SerializeField, Min(0f)] private float deathDelay = 0.8f;
    [SerializeField, Min(0.01f)] private float deathFadeIn = 1.6f;
    [Tooltip("Yazinin ekranda kalma suresi (sn)")]
    [SerializeField, Min(0f)] private float deathHold = 2.2f;

    [Header("Gecisler")]
    [SerializeField, Min(0.01f)] private float fadeToBlack = 1.0f;
    [SerializeField, Min(0.01f)] private float fadeFromBlack = 0.9f;

    [Header("Bant (ATES YAKILDI)")]
    [SerializeField, Min(0.01f)] private float bannerFadeIn = 0.5f;
    [SerializeField, Min(0f)] private float bannerHold = 1.8f;
    [SerializeField, Min(0.01f)] private float bannerFadeOut = 0.8f;

    [Header("Tema")]
    [Tooltip("Bos birakilirsa varsayilan tema kullanilir")]
    [SerializeField] private UITheme theme;

    private static bool fadeInOnNextLoad;
    private Health playerHealth;
    private bool sequenceRunning;
    private Coroutine bannerRoutine;

    // --- Otomatik ekleme ---
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        fadeInOnNextLoad = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

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
        if (FindAnyObjectByType<DeathScreen>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<PlayerMovement>() == null) return;
        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning("DeathScreen: Assets/UI/Resources/DeathScreen.prefab bulunamadi. Tools > FarToHell > Olum Ekrani Kur ile olustur.");
            return;
        }
        var go = Instantiate(prefab);
        go.name = prefab.name;
    }

    // --- Yasam dongusu ---
    private void Awake()
    {
        Instance = this;
        ApplyTheme();
        SetGroup(deathGroup, 0f);
        SetGroup(bannerGroup, 0f);
        if (fader != null)
        {
            fader.alpha = fadeInOnNextLoad ? 1f : 0f;
            fader.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        var p = FindAnyObjectByType<PlayerMovement>();
        if (p != null)
        {
            playerHealth = p.GetComponent<Health>();
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        }
        if (fadeInOnNextLoad)
        {
            fadeInOnNextLoad = false;
            StartCoroutine(Fade(fader, 1f, 0f, fadeFromBlack));
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        if (Instance == this) Instance = null;
    }

    private void OnPlayerDied()
    {
        if (sequenceRunning) return;
        StartCoroutine(DeathSequence());
    }

    /// <summary>Kontrol noktasi bandi (ornegin "ATES YAKILDI").</summary>
    public void ShowBanner(string message)
    {
        if (bannerText != null) bannerText.text = message;
        if (bannerRoutine != null) StopCoroutine(bannerRoutine);
        bannerRoutine = StartCoroutine(BannerSequence());
    }

    /// <summary>Siyaha kararip son kontrol noktasinda yeniden dogar (dinlenme).</summary>
    public void FadeAndRespawn()
    {
        if (sequenceRunning) return;
        StartCoroutine(RespawnSequence());
    }

    // --- Siralar ---
    private IEnumerator DeathSequence()
    {
        sequenceRunning = true;
        if (deathText != null) deathText.text = deathMessage;
        yield return Wait(deathDelay);

        // Bant ve yazi yavasca belirir, yazi hafifce kuculerek yerine oturur
        Transform word = deathImage != null && deathImage.enabled ? deathImage.transform : (deathText != null ? deathText.transform : null);
        float t = 0f;
        while (t < deathFadeIn)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / deathFadeIn);
            SetGroup(deathGroup, Mathf.SmoothStep(0f, 1f, k));
            if (word != null) word.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        yield return Wait(deathHold);
        yield return RespawnSequence();
    }

    private IEnumerator RespawnSequence()
    {
        sequenceRunning = true;
        if (fader != null)
        {
            fader.blocksRaycasts = true;
            yield return Fade(fader, fader.alpha, 1f, fadeToBlack);
        }
        fadeInOnNextLoad = true;
        // Checkpoint, sahne yuklenince oyuncuyu son kontrol noktasina koyar; yoksa bolum basinda dogar
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private IEnumerator BannerSequence()
    {
        yield return Fade(bannerGroup, bannerGroup != null ? bannerGroup.alpha : 0f, 1f, bannerFadeIn);
        yield return Wait(bannerHold);
        yield return Fade(bannerGroup, 1f, 0f, bannerFadeOut);
        bannerRoutine = null;
    }

    // --- Yardimcilar ---
    public void ApplyTheme()
    {
        var t = theme != null ? theme : UITheme.Default;
        if (t == null) return;
        var font = t.titleFont != null ? t.titleFont : t.buttonFont;
        if (deathText != null)
        {
            if (font != null) deathText.font = font;
            deathText.color = t.deathTextColor;
        }
        if (band != null) band.color = t.deathBandColor;
        if (bannerText != null)
        {
            if (font != null) bannerText.font = font;
            bannerText.color = t.bannerColor;
        }
        if (deathImage != null)
        {
            deathImage.sprite = t.deathTitle;
            deathImage.enabled = t.deathTitle != null;
            if (deathText != null) deathText.enabled = t.deathTitle == null;
        }
    }

    private static void SetGroup(CanvasGroup g, float a)
    {
        if (g == null) return;
        g.alpha = a;
        g.blocksRaycasts = false;
        g.interactable = false;
    }

    private static IEnumerator Fade(CanvasGroup g, float from, float to, float duration)
    {
        if (g == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        g.alpha = to;
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }
}
