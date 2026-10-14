using UnityEngine;
using UnityEngine.Tilemaps;

// Mantiene la cámara ortográfica dentro de los límites de un tilemap.
// Va en la cámara junto a CameraFollow; corrige la posición después de que ésta se mueva.
[RequireComponent(typeof(Camera))]
public class CameraBounds : MonoBehaviour
{
    public Tilemap bounds;
    public float padding = 1f;

    private Camera cam;
    private Rect area;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (bounds == null) return;

        bounds.CompressBounds();
        Bounds b = bounds.localBounds;
        Vector3 min = bounds.transform.TransformPoint(b.min);
        Vector3 max = bounds.transform.TransformPoint(b.max);
        area = Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + padding);
    }

    private void LateUpdate()
    {
        if (bounds == null) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 p = transform.position;
        p.x = ClampAxis(p.x, area.xMin + halfW, area.xMax - halfW);
        p.y = ClampAxis(p.y, area.yMin + halfH, area.yMax - halfH);
        transform.position = p;
    }

    // Si el área es más pequeña que la vista, centra la cámara en ese eje.
    private static float ClampAxis(float v, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(v, min, max);
    }
}
