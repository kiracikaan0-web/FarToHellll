using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Kilic savurma sesi. Saldiri tusuna basildiginda (sol tik / J / gamepad X)
/// Resources/SFX/SwordSwing klasorundeki seslerden rastgele birini, hafif farkli
/// perdeyle calar; boylece art arda savurmalar ayni duyulmaz.
///
/// Sahneye eklemeye gerek yok: oyun baslarken PlayerMovement olan objeye kendini ekler.
/// Ileride saldiri animasyonu/scripti yazdiginda o scriptten veya bir Animation Event'ten
/// PlaySwing() cagirabilirsin; o zaman "Play On Input" kutusunu kapat.
/// </summary>
public class SwordSwingSound : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private bool playOnInput = true;
    [SerializeField] private InputAction attackAction = new InputAction("Attack", InputActionType.Button);

    [Header("Ses")]
    [SerializeField] private AudioClip[] swingClips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    [Tooltip("Iki savurma arasi en kisa sure (sn)")]
    [SerializeField] private float cooldown = 0.3f;

    private AudioSource source;
    private float nextSwingTime;
    private int lastIndex = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        AttachToPlayer();
        SceneManager.sceneLoaded += (scene, mode) => AttachToPlayer();
    }

    private static void AttachToPlayer()
    {
        PlayerMovement player = FindAnyObjectByType<PlayerMovement>();
        if (player != null && player.GetComponent<SwordSwingSound>() == null)
            player.gameObject.AddComponent<SwordSwingSound>();
    }

    private void Awake()
    {
        if (swingClips == null || swingClips.Length == 0)
            swingClips = Resources.LoadAll<AudioClip>("SFX/SwordSwing");

        if (attackAction == null)
            attackAction = new InputAction("Attack", InputActionType.Button);
        if (attackAction.bindings.Count == 0)
        {
            attackAction.AddBinding("<Mouse>/leftButton");
            attackAction.AddBinding("<Keyboard>/j");
            attackAction.AddBinding("<Gamepad>/buttonWest");
        }

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private void OnEnable() { attackAction.Enable(); }
    private void OnDisable() { attackAction.Disable(); }

    private void Update()
    {
        if (playOnInput && attackAction.WasPressedThisFrame())
            PlaySwing();
    }

    /// <summary>Savurma sesini calar. Saldiri scriptinden veya Animation Event'ten cagrilabilir.</summary>
    public void PlaySwing()
    {
        if (Time.time < nextSwingTime || swingClips == null || swingClips.Length == 0) return;
        nextSwingTime = Time.time + cooldown;

        int index = Random.Range(0, swingClips.Length);
        if (swingClips.Length > 1 && index == lastIndex)
            index = (index + 1) % swingClips.Length;
        lastIndex = index;

        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(swingClips[index], volume);
    }
}
