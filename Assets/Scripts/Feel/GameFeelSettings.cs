using UnityEngine;

/// <summary>
/// Vurus hissi ayarlari: hit-stop (oyunun salise kadar durmasi), kamera sarsintisi, kemik parcalari ve sesler.
/// Her biri ayri ayri kapatilabilir.
/// Dosya: Assets/Resources/GameFeelSettings.asset  (Tools > FarToHell > Vurus Hissi Ayarlari ile olusur/secilir)
/// Dosya yoksa asagidaki varsayilan degerler kullanilir.
/// </summary>
[CreateAssetMenu(menuName = "FarToHell/Game Feel Settings", fileName = "GameFeelSettings")]
public class GameFeelSettings : ScriptableObject
{
    [Header("Acik / kapali")]
    public bool hitStop = true;
    public bool cameraShake = true;
    public bool hitParticles = true;
    public bool hitSounds = true;

    [Header("Hit-stop (sn) - oyun bu kadar sure donar")]
    [Range(0f, 0.3f)] public float hitStopEnemyHit = 0.05f;
    [Range(0f, 0.3f)] public float hitStopEnemyKill = 0.12f;
    [Range(0f, 0.3f)] public float hitStopPlayerHurt = 0.08f;

    [Header("Kamera sarsintisi (guc, sure)")]
    public Vector2 shakeEnemyHit = new Vector2(0.10f, 0.12f);
    public Vector2 shakeEnemyKill = new Vector2(0.22f, 0.25f);
    public Vector2 shakePlayerHurt = new Vector2(0.30f, 0.22f);

    [Header("Parcalar")]
    [Range(0, 40)] public int particlesEnemyHit = 8;
    [Range(0, 60)] public int particlesEnemyKill = 18;
    [Range(0, 40)] public int particlesPlayerHurt = 10;
    public Color enemyParticleColor = new Color(0.95f, 0.86f, 0.68f, 1f); // kemik
    public Color playerParticleColor = new Color(0.75f, 0.88f, 1f, 1f);   // oyuncunun mavimsi kemigi

    [Header("Ses seviyeleri")]
    [Range(0f, 1f)] public float hitVolume = 0.8f;
    [Range(0f, 1f)] public float hurtVolume = 0.9f;
    [Range(0f, 1f)] public float deathVolume = 0.8f;

    private static GameFeelSettings cached;
    private static GameFeelSettings fallback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => cached = null;

    public static GameFeelSettings Current
    {
        get
        {
            if (cached == null) cached = Resources.Load<GameFeelSettings>("GameFeelSettings");
            if (cached != null) return cached;
            if (fallback == null) fallback = CreateInstance<GameFeelSettings>();
            return fallback;
        }
    }
}
