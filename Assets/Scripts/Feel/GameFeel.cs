using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vurus hissi: her hasar ve olumde otomatik calisir (Health olaylarini dinler), sahneye eklemeye gerek yok.
///  - Hit-stop: oyun salise kadar donar, vurus "agir" hissettirir
///  - Kamera sarsintisi (CameraFollow uzerinden)
///  - Kemik parcalari sacilir
///  - Vurus / hasar / olum sesleri
/// Ayarlar ve ac/kapa: Tools > FarToHell > Vurus Hissi Ayarlari
/// Baska scriptlerden: GameFeel.Shake(...), GameFeel.HitStop(...), GameFeel.Burst(...), GameFeel.PlaySound(...)
/// </summary>
public class GameFeel : MonoBehaviour
{
    private static GameFeel instance;
    private static Sprite pixel;

    private AudioSource audioSource;
    private readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    private float hitStopUntil;
    private bool hitStopActive;

    // ---------------- Kurulum ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (instance != null) return;
        var go = new GameObject("GameFeel");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameFeel>();
    }

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        Health.AnyDamaged += OnAnyDamaged;
        Health.AnyDied += OnAnyDied;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        Health.AnyDamaged -= OnAnyDamaged;
        Health.AnyDied -= OnAnyDied;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (hitStopActive && !PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // Sahne degisirken hit-stop ortada kalmasin
        if (hitStopActive && !PauseMenu.IsPaused) Time.timeScale = 1f;
        hitStopActive = false;
    }

    // ---------------- Olaylar ----------------

    private void OnAnyDamaged(Health victim, DamageInfo info)
    {
        var s = GameFeelSettings.Current;
        bool isPlayer = victim.GetComponent<PlayerMovement>() != null;
        bool killing = victim.IsDead || victim.Current <= 0;
        Vector2 point = HitPoint(victim, info);

        if (isPlayer)
        {
            if (s.hitStop) HitStop(s.hitStopPlayerHurt);
            if (s.cameraShake) Shake(s.shakePlayerHurt.x, s.shakePlayerHurt.y);
            if (s.hitParticles) Burst(point, s.particlesPlayerHurt, s.playerParticleColor, Mathf.Sign(point.x - info.sourcePosition.x));
            if (s.hitSounds) PlaySound("PlayerHurt", s.hurtVolume);
        }
        else if (!killing)
        {
            if (s.hitStop) HitStop(s.hitStopEnemyHit);
            if (s.cameraShake) Shake(s.shakeEnemyHit.x, s.shakeEnemyHit.y);
            if (s.hitParticles) Burst(point, s.particlesEnemyHit, s.enemyParticleColor, Mathf.Sign(point.x - info.sourcePosition.x));
            if (s.hitSounds) PlaySound("HitEnemy", s.hitVolume);
        }
        // oldurucu vurus: OnAnyDied daha guclu tepki verir
    }

    private void OnAnyDied(Health victim)
    {
        if (victim.GetComponent<PlayerMovement>() != null) return; // oyuncu olumu: olum ekrani yonetir
        var s = GameFeelSettings.Current;
        Vector2 point = victim.transform.position + Vector3.up * 1.6f;
        if (s.hitStop) HitStop(s.hitStopEnemyKill);
        if (s.cameraShake) Shake(s.shakeEnemyKill.x, s.shakeEnemyKill.y);
        if (s.hitParticles) Burst(point, s.particlesEnemyKill, s.enemyParticleColor, 0f);
        if (s.hitSounds)
        {
            PlaySound("HitEnemy", s.hitVolume);
            PlaySound("EnemyDeath", s.deathVolume);
        }
    }

    private static Vector2 HitPoint(Health victim, DamageInfo info)
    {
        var col = victim.GetComponent<Collider2D>();
        Vector2 c = col != null ? (Vector2)col.bounds.center : (Vector2)victim.transform.position + Vector2.up * 1.5f;
        float halfW = col != null ? col.bounds.extents.x : 0.5f;
        float dir = Mathf.Sign(info.sourcePosition.x - c.x);
        return new Vector2(c.x + dir * halfW * 0.7f, c.y + Random.Range(-0.3f, 0.4f));
    }

    // ---------------- Hit-stop ----------------

    public static void HitStop(float seconds)
    {
        if (instance == null || seconds <= 0f || PauseMenu.IsPaused) return;
        instance.hitStopUntil = Mathf.Max(instance.hitStopUntil, Time.unscaledTime + seconds);
        if (!instance.hitStopActive) instance.StartCoroutine(instance.HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        hitStopActive = true;
        Time.timeScale = 0f;
        while (Time.unscaledTime < hitStopUntil)
        {
            if (PauseMenu.IsPaused) { hitStopActive = false; yield break; } // duraklatma zamani yonetir
            yield return null;
        }
        hitStopActive = false;
        if (!PauseMenu.IsPaused) Time.timeScale = 1f;
    }

    // ---------------- Sarsinti ----------------

    public static void Shake(float strength, float duration) => CameraFollow.AddShake(strength, duration);

    // ---------------- Parcalar ----------------

    public static void Burst(Vector2 position, int count, Color color, float direction)
    {
        if (instance == null || count <= 0) return;
        if (pixel == null)
            pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Chip");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixel;
            sr.sortingOrder = 40;
            var c = color * Random.Range(0.8f, 1.05f); c.a = 1f;
            sr.color = c;
            float size = Random.Range(0.08f, 0.2f);
            go.transform.localScale = new Vector3(size * Random.Range(1f, 2.2f), size, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float dirX = direction == 0f ? Random.Range(-1f, 1f) : direction * Random.Range(0.2f, 1f) + Random.Range(-0.35f, 0.35f);
            var vel = new Vector2(dirX * Random.Range(3f, 7f), Random.Range(2f, 7f));
            instance.StartCoroutine(instance.ChipRoutine(go.transform, sr, vel, Random.Range(0.35f, 0.7f)));
        }
    }

    private IEnumerator ChipRoutine(Transform tr, SpriteRenderer sr, Vector2 vel, float life)
    {
        float t = 0f;
        float spin = Random.Range(-720f, 720f);
        Color start = sr.color;
        while (t < life && tr != null)
        {
            float dt = Time.deltaTime; // hit-stop sirasinda parcalar da donar
            t += dt;
            vel.y -= 22f * dt;
            tr.position += (Vector3)(vel * dt);
            tr.Rotate(0f, 0f, spin * dt);
            float k = t / life;
            sr.color = new Color(start.r, start.g, start.b, 1f - k * k);
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }

    // ---------------- Ses ----------------

    /// <summary>Resources/SFX/Feel icindeki "name" ile baslayan seslerden birini calar (HitEnemy1, HitEnemy2...).</summary>
    public static void PlaySound(string name, float volume = 1f)
    {
        if (instance == null) return;
        var arr = instance.GetClips(name);
        if (arr.Length == 0) return;
        var clip = arr[Random.Range(0, arr.Length)];
        instance.audioSource.pitch = Random.Range(0.93f, 1.07f);
        instance.audioSource.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    private AudioClip[] GetClips(string name)
    {
        if (clips.TryGetValue(name, out var arr)) return arr;
        var list = new List<AudioClip>();
        foreach (var c in Resources.LoadAll<AudioClip>("SFX/Feel"))
            if (c.name.StartsWith(name)) list.Add(c);
        arr = list.ToArray();
        clips[name] = arr;
        return arr;
    }
}
