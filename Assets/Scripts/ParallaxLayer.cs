using UnityEngine;

/// <summary>
/// Sonsuz kayan parallax arka plan katmani.
/// parallax = 1  -> kameraya yapisik (gokyuzu gibi, hic kaymaz)
/// parallax = 0  -> dunyayla birlikte durur (zemin gibi)
/// Aradaki degerler: uzak katmanlar yavas, yakin katmanlar hizli kayar.
///
/// Katman, kameranin boyuna (Orthographic Size) gore kendini olcekler ve sag/soluna kopya koyarak
/// yatayda bosluk birakmadan tekrar eder. Kamera boyutu oyun sirasinda degisirse yeniden olceklenir.
/// Kurulum: Tools > FarToHell > Arka Plan Kur
/// </summary>
// Kamera (CameraFollow) LateUpdate'te hareket ettikten SONRA calissin; yoksa arka plan bir kare
// geriden gelir ve hareket ederken titrer/cift gorunur.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxLayer : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float parallax = 0.5f;
    [Tooltip("Katman yuksekligi = kamera yuksekligi x bu deger")]
    [SerializeField] private float heightMultiplier = 1.15f;
    [Tooltip("Kamera merkezine gore dikey kaydirma (dunya birimi, kamera yuksekligine oranli: 0.1 = yuksekligin %10'u)")]
    [SerializeField] private float verticalOffset = 0f;

    private Camera cam;
    private SpriteRenderer sr;
    private float startPos;
    private float length;
    private float lastOrthoSize = -1f;
    private float lastAspect = -1f;

    private void Start()
    {
        cam = Camera.main;
        sr = GetComponent<SpriteRenderer>();
        if (cam == null || sr.sprite == null)
        {
            enabled = false;
            return;
        }

        CreateTile(-1);
        CreateTile(1);
        Refit();
        startPos = transform.position.x - cam.transform.position.x * parallax;
    }

    private void CreateTile(int side)
    {
        var tile = new GameObject(name + (side < 0 ? "_L" : "_R"));
        tile.transform.SetParent(transform, false);
        tile.transform.localPosition = new Vector3(side * sr.sprite.bounds.size.x, 0f, 0f);
        var t = tile.AddComponent<SpriteRenderer>();
        t.sprite = sr.sprite;
        t.sharedMaterial = sr.sharedMaterial;
        t.color = sr.color;
        t.sortingLayerID = sr.sortingLayerID;
        t.sortingOrder = sr.sortingOrder;
    }

    private void Refit()
    {
        lastOrthoSize = cam.orthographicSize;
        lastAspect = cam.aspect;
        float camHeight = 2f * cam.orthographicSize;
        float scale = camHeight * heightMultiplier / sr.sprite.bounds.size.y;
        transform.localScale = new Vector3(scale, scale, 1f);
        length = sr.sprite.bounds.size.x * scale;
    }

    private void LateUpdate()
    {
        if (!Mathf.Approximately(cam.orthographicSize, lastOrthoSize) || !Mathf.Approximately(cam.aspect, lastAspect))
            Refit();

        Vector3 camPos = cam.transform.position;
        float travelled = camPos.x * (1f - parallax); // kameranin katmana gore kat ettigi yol
        float dist = camPos.x * parallax;

        float y = camPos.y + verticalOffset * 2f * cam.orthographicSize;
        transform.position = new Vector3(startPos + dist, y, transform.position.z);

        // Kamera bir kopyanin sinirini gecince katmani o yone kaydir (sonsuz tekrar)
        if (travelled > startPos + length) startPos += length;
        else if (travelled < startPos - length) startPos -= length;
    }
}
