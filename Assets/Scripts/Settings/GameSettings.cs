using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyun ayarlari: ses (genel, muzik, efekt) ve goruntu (tam ekran, cozunurluk, dikey senkron).
/// PlayerPrefs ile kaydedilir; oyun her acildiginda otomatik yuklenip uygulanir.
/// Muzik: BackgroundMusic, efektler: SwordSwingSound ve benzerleri bu degerleri kullanir.
/// </summary>
public static class GameSettings
{
    private const string KeyMaster = "settings.masterVolume";
    private const string KeyMusic = "settings.musicVolume";
    private const string KeySfx = "settings.sfxVolume";
    private const string KeyFullscreen = "settings.fullscreen";
    private const string KeyResW = "settings.resWidth";
    private const string KeyResH = "settings.resHeight";
    private const string KeyVSync = "settings.vsync";

    /// <summary>Herhangi bir ayar degisince.</summary>
    public static event Action Changed;

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.8f;
    public static float SfxVolume { get; private set; } = 1f;
    public static bool Fullscreen { get; private set; } = true;
    public static bool VSync { get; private set; } = true;
    public static Vector2Int Resolution { get; private set; }

    /// <summary>Duraklatma menusu gibi yerlerin genel sesi gecici olarak kismasi icin carpan (1 = normal).</summary>
    public static float MasterDuck { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Changed = null; // Domain Reload kapaliyken eski aboneler kalmasin
        MasterDuck = 1f;
        MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.8f);
        SfxVolume = PlayerPrefs.GetFloat(KeySfx, 1f);
        Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        VSync = PlayerPrefs.GetInt(KeyVSync, 1) == 1;
        Resolution = new Vector2Int(
            PlayerPrefs.GetInt(KeyResW, Screen.currentResolution.width),
            PlayerPrefs.GetInt(KeyResH, Screen.currentResolution.height));
        ApplyAudio();
        ApplyDisplay();
    }

    // ---------------- Ses ----------------

    public static void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KeyMaster, MasterVolume); ApplyAudio(); Save(); }
    public static void SetMusicVolume(float v) { MusicVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KeyMusic, MusicVolume); Save(); }
    public static void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KeySfx, SfxVolume); Save(); }

    /// <summary>Genel sesi gecici olarak kis (1 = normal). Kaydedilmez.</summary>
    public static void SetMasterDuck(float duck)
    {
        MasterDuck = Mathf.Clamp01(duck);
        ApplyAudio();
    }

    private static void ApplyAudio() => AudioListener.volume = MasterVolume * MasterDuck;

    // ---------------- Goruntu ----------------

    public static void SetFullscreen(bool on) { Fullscreen = on; PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0); ApplyDisplay(); Save(); }
    public static void SetVSync(bool on) { VSync = on; PlayerPrefs.SetInt(KeyVSync, on ? 1 : 0); ApplyDisplay(); Save(); }

    public static void SetResolution(Vector2Int r)
    {
        Resolution = r;
        PlayerPrefs.SetInt(KeyResW, r.x);
        PlayerPrefs.SetInt(KeyResH, r.y);
        ApplyDisplay();
        Save();
    }

    /// <summary>Ekranin destekledigi cozunurlukler (tekrarsiz, kucukten buyuge).</summary>
    public static List<Vector2Int> AvailableResolutions()
    {
        var list = new List<Vector2Int>();
        foreach (var r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }

    public static void CycleResolution(int dir)
    {
        var list = AvailableResolutions();
        int i = list.IndexOf(Resolution);
        if (i < 0) i = list.Count - 1;
        i = (i + dir + list.Count) % list.Count;
        SetResolution(list[i]);
    }

    private static void ApplyDisplay()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
#if !UNITY_EDITOR
        // Editorde pencere boyutunu Game penceresi belirler; sadece derlenmis oyunda uygula
        if (Resolution.x > 0 && Resolution.y > 0)
            Screen.SetResolution(Resolution.x, Resolution.y,
                Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
    }

    private static void Save()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
