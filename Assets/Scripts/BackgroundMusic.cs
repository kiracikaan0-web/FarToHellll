using UnityEngine;

/// <summary>
/// Arka plan muzigini dongude calar ve sahneler arasi kaybolmaz.
/// Sahneye eklemeye gerek yok: oyun baslarken kendini otomatik olusturur
/// ve Resources klasorundeki varsayilan parcayi (DefaultTrack) yukler.
/// Istersen bir GameObject'e elle ekleyip farkli bir klip/ses seviyesi verebilirsin.
/// </summary>
public class BackgroundMusic : MonoBehaviour
{
    private static BackgroundMusic instance;

    // Resources klasorundeki varsayilan parca:
    // "Audio/HellPolyphonic" (cok sesli ambiyans), "Audio/HellAmbient" (sakin/urkutucu),
    // "Audio/HellRiff" (yavaslatilmis gallop riff), "Audio/HellGallop" (tam metal parca),
    // "Audio/HellTheme" (karanlik/saskin)
    private const string DefaultTrack = "Audio/HellPolyphonic";

    [SerializeField] private AudioClip musicClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    private AudioSource source;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (instance != null) return;
        new GameObject("BackgroundMusic").AddComponent<BackgroundMusic>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicClip == null)
            musicClip = Resources.Load<AudioClip>(DefaultTrack);

        source = gameObject.AddComponent<AudioSource>();
        source.clip = musicClip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.Play();
    }

    private void OnValidate()
    {
        if (source != null) source.volume = volume;
    }
}
