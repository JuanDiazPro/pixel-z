using UnityEngine;

// Punto que sigue la cámara: el centro entre los jugadores vivos. En cooperativo, si se separan,
// la cámara se aleja un poco (hasta un límite) para que los dos sigan en pantalla.
[DefaultExecutionOrder(-100)]
public class CoopCameraTarget : MonoBehaviour
{
    private const float MaxZoomOut = 1.55f;
    private const float Margin = 2.2f;

    private Camera cam;
    private float baseSize;

    public void Init(Camera camera)
    {
        cam = camera;
        baseSize = camera.orthographicSize;
    }

    private void Update()
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
        int n = 0;
        foreach (PlayerController p in PlayerController.All)
        {
            if (p.Dead && PlayerController.AnyAlive) continue;
            Vector2 pos = p.Center;
            min = Vector2.Min(min, pos);
            max = Vector2.Max(max, pos);
            n++;
        }
        if (n == 0) return;
        Vector2 center = (min + max) * 0.5f;
        transform.position = new Vector3(center.x, center.y - 0.45f, transform.position.z);

        if (cam == null || !cam.orthographic) return;
        Vector2 spread = max - min;
        float needed = Mathf.Max(spread.y * 0.5f + Margin, (spread.x * 0.5f + Margin) / Mathf.Max(cam.aspect, 0.1f));
        float size = Mathf.Clamp(needed, baseSize, baseSize * MaxZoomOut);
        cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, size, Time.unscaledDeltaTime * 3f);
    }
}
