using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Kontrol noktasi (sunak). Oyuncu yanina gelip etkilesim tusuna basinca:
///  - Ilk sefer: ates yakilir, "ATES YAKILDI" yazisi cikar, can ve stamina dolar, dogma noktasi burasi olur.
///  - Sonraki seferler: dinlenir. Ekran kararir, bolum yeniden yuklenir (dusmanlar canlanir), oyuncu burada tam canla dogar.
/// Olunce de son kontrol noktasinda dogulur (DeathScreen).
///
/// Tuslar: E, gamepad Y (ust tus)
/// Kurulum: Tools > FarToHell > Kontrol Noktasi Ekle
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Header("Etkilesim")]
    [SerializeField, Min(0.5f)] private float interactRadius = 2.6f;
    [SerializeField] private InputAction interactAction = new InputAction("Interact", InputActionType.Button);
    [SerializeField] private string lightPrompt = "E  ·  Ateşi yak";
    [SerializeField] private string restPrompt = "E  ·  Dinlen";
    [SerializeField] private string litBanner = "ATEŞ YAKILDI";

    [Header("Baglantilar (kurulum menusu doldurur)")]
    [Tooltip("Oyuncunun dogacagi nokta")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject flame;
    [SerializeField] private GameObject embers;
    [SerializeField] private SpriteRenderer glow;
    [SerializeField] private GameObject prompt;
    [SerializeField] private Text promptText;

    [Header("Alev titremesi")]
    [SerializeField, Min(0f)] private float flickerAmount = 0.06f;
    [SerializeField, Min(0f)] private float flickerSpeed = 9f;

    // ---- Kayit (sahne yeniden yuklense de korunur; Ana Menu'den yeni oyun baslayinca silinir) ----
    private static string respawnScene;
    private static string respawnId;
    private static readonly HashSet<string> litIds = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ClearProgress();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>Yeni oyun: tum kontrol noktasi ilerlemesini sil (Ana Menu > Basla bunu cagirir).</summary>
    public static void ClearProgress()
    {
        respawnScene = null;
        respawnId = null;
        litIds.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        PlacePlayerAtRespawn(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => PlacePlayerAtRespawn(scene);

    /// <summary>Sahne yuklenince oyuncuyu son kontrol noktasina koyar (o sahnedeyse).</summary>
    private static void PlacePlayerAtRespawn(Scene scene)
    {
        if (string.IsNullOrEmpty(respawnId) || scene.name != respawnScene) return;
        var player = FindAnyObjectByType<PlayerMovement>();
        if (player == null) return;
        foreach (var cp in FindObjectsByType<Checkpoint>(FindObjectsInactive.Exclude))
        {
            if (cp.Id != respawnId) continue;
            Vector3 p = cp.spawnPoint != null ? cp.spawnPoint.position : cp.transform.position + Vector3.right * 1.5f;
            player.transform.position = new Vector3(p.x, p.y, player.transform.position.z);
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = player.transform.position; rb.linearVelocity = Vector2.zero; }
            // Kamera dogrudan oyuncuya baksin (yumusak takip bastan kaymasin)
            var cam = Camera.main;
            if (cam != null)
                cam.transform.position = new Vector3(p.x, p.y + 2f, cam.transform.position.z);
            break;
        }
    }

    // ---- Ornek ----
    private string id;
    private bool isLit;
    private Transform player;
    private Health playerHealth;
    private bool playerNear;
    private Vector3 flameBaseScale = Vector3.one;
    private float glowBaseAlpha = 1f;
    private bool busy;

    /// <summary>Sahne adi + konum: kopyalanan kontrol noktalari da farkli kimlik alir.</summary>
    public string Id => id;

    private void Awake()
    {
        id = $"{gameObject.scene.name}:{transform.position.x:F1},{transform.position.y:F1}";
        isLit = litIds.Contains(id);

        if (interactAction == null) interactAction = new InputAction("Interact", InputActionType.Button);
        if (interactAction.bindings.Count == 0)
        {
            interactAction.AddBinding("<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonNorth");
        }

        if (flame != null) flameBaseScale = flame.transform.localScale;
        if (glow != null) glowBaseAlpha = glow.color.a;
        ApplyVisuals();
        if (prompt != null) prompt.SetActive(false);
    }

    private void OnEnable() => interactAction.Enable();
    private void OnDisable() => interactAction.Disable();

    private void Update()
    {
        Flicker();

        if (player == null)
        {
            var p = FindAnyObjectByType<PlayerMovement>();
            if (p == null) return;
            player = p.transform;
            playerHealth = p.GetComponent<Health>();
        }

        bool alive = playerHealth == null || !playerHealth.IsDead;
        bool near = alive && !busy && Vector2.Distance(player.position, transform.position) <= interactRadius;
        if (near != playerNear)
        {
            playerNear = near;
            if (prompt != null) prompt.SetActive(near);
        }
        if (near && promptText != null) promptText.text = isLit ? restPrompt : lightPrompt;

        if (!near || PauseMenu.BlocksGameplayInput) return;
        if (interactAction.WasPressedThisFrame())
        {
            if (!isLit) LightFire();
            else Rest();
        }
    }

    private void LightFire()
    {
        isLit = true;
        litIds.Add(id);
        SetAsRespawn();
        RefillPlayer();
        ApplyVisuals();
        if (DeathScreen.Instance != null) DeathScreen.Instance.ShowBanner(litBanner);
    }

    private void Rest()
    {
        SetAsRespawn();
        busy = true;
        if (prompt != null) prompt.SetActive(false);
        var pm = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (pm != null) pm.MovementLocked = true;

        if (DeathScreen.Instance != null) DeathScreen.Instance.FadeAndRespawn();
        else SceneManager.LoadScene(gameObject.scene.buildIndex);
    }

    private void SetAsRespawn()
    {
        respawnScene = gameObject.scene.name;
        respawnId = id;
    }

    private void RefillPlayer()
    {
        if (player == null) return;
        var h = player.GetComponent<Health>();
        if (h != null) h.ResetHealth();
        var s = player.GetComponent<PlayerStamina>();
        if (s != null) s.Refill();
        var f = player.GetComponent<PlayerFlask>();
        if (f != null) f.Refill();
    }

    private void ApplyVisuals()
    {
        if (flame != null) flame.SetActive(isLit);
        if (embers != null) embers.SetActive(!isLit);
        if (glow != null) glow.gameObject.SetActive(isLit);
    }

    private void Flicker()
    {
        if (!isLit || flame == null) return;
        float t = Time.time * flickerSpeed;
        float n = Mathf.PerlinNoise(t, 0.37f) * 2f - 1f;
        float n2 = Mathf.PerlinNoise(0.71f, t * 1.3f) * 2f - 1f;
        flame.transform.localScale = new Vector3(
            flameBaseScale.x * (1f - n2 * flickerAmount * 0.6f),
            flameBaseScale.y * (1f + n * flickerAmount), 1f);
        if (glow != null)
        {
            var c = glow.color;
            c.a = glowBaseAlpha * (0.85f + 0.15f * n);
            glow.color = c;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, interactRadius);
        if (spawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.3f);
        }
    }
}
