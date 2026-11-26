using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana menu: Basla ve Cikis butonlari, acilista ve oyuna geciste ekran kararmasi (fade).
/// Klavye (W/S, oklar, Enter), gamepad ve fare ile kullanilabilir.
/// Kurulum: Tools > FarToHell > Ana Menu Kur
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Basla'ya basinca acilacak sahnenin adi (Build Settings'te ekli olmali).")]
    [SerializeField] private string gameSceneName = "Map1";
    [SerializeField] private Button firstSelected;
    [Tooltip("Ekrani karartan siyah panelin CanvasGroup'u")]
    [SerializeField] private CanvasGroup fader;
    [SerializeField] private float fadeInDuration = 1.2f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    private bool busy;
    private GameObject lastSelected;

    private void Start()
    {
        Time.timeScale = 1f;

        if (fader != null)
        {
            fader.alpha = 1f;
            fader.blocksRaycasts = true;
            StartCoroutine(FadeIn());
        }

        if (firstSelected != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
    }

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;

        // Fareyle bos bir yere tiklaninca secim kaybolur; klavye/gamepad calismaya devam etsin diye geri sec
        if (es.currentSelectedGameObject != null)
            lastSelected = es.currentSelectedGameObject;
        else if (!busy)
            es.SetSelectedGameObject(lastSelected != null ? lastSelected : firstSelected != null ? firstSelected.gameObject : null);
    }

    public void StartGame()
    {
        if (busy) return;
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError($"MainMenu: '{gameSceneName}' sahnesi Build Settings'te yok. File > Build Profiles (Build Settings) uzerinden ekle.", this);
            return;
        }
        busy = true;
        Checkpoint.ClearProgress(); // yeni oyun: kontrol noktalari sifirdan
        StartCoroutine(LoadGame());
    }

    public void QuitGame()
    {
        if (busy) return;
        busy = true;
        StartCoroutine(Quit());
    }

    private IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f, fadeInDuration);
        fader.blocksRaycasts = false;
    }

    private IEnumerator LoadGame()
    {
        if (fader != null)
        {
            fader.blocksRaycasts = true;
            yield return Fade(fader.alpha, 1f, fadeOutDuration);
        }
        SceneManager.LoadScene(gameSceneName);
    }

    private IEnumerator Quit()
    {
        if (fader != null)
        {
            fader.blocksRaycasts = true;
            yield return Fade(fader.alpha, 1f, fadeOutDuration * 0.6f);
        }
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fader == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fader.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        fader.alpha = to;
    }
}
