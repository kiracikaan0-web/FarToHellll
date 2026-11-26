using UnityEngine;

/// <summary>
/// Sallanan tehlike (krokide "iskelet dolu depo"dan sarkan). Obje = baglanti noktasi; cocuk objeler
/// (ip ve kafatasi top) bu noktanin etrafinda doner. Top'taki DamageZone hasar verir.
/// </summary>
public class Pendulum : MonoBehaviour
{
    [Tooltip("Sallanma acisi (derece, merkezden her iki yana)")]
    [SerializeField, Range(5f, 85f)] private float amplitude = 50f;
    [Tooltip("Bir tam gidis-gelis suresi (sn)")]
    [SerializeField, Min(0.2f)] private float period = 3.2f;
    [SerializeField, Range(0f, 1f)] private float phase = 0f;
    [SerializeField] private Transform bob;
    [SerializeField] private float bobSpin = 0.6f;

    private void Update()
    {
        float a = amplitude * Mathf.Sin((Time.time / period + phase) * Mathf.PI * 2f);
        transform.localRotation = Quaternion.Euler(0f, 0f, a);
        // top hafifce kendi etrafinda donsun (hareket hissi)
        if (bob != null) bob.localRotation = Quaternion.Euler(0f, 0f, -a * bobSpin);
    }
}
