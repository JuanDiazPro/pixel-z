using UnityEngine;

// Marca una zona o un punto reservado del mapa (sala, futuro NPC, enemigo, cofre...).
// No hace nada en el juego: sólo se dibuja en la vista Scene para organizar el nivel.
public class ZoneMarker : MonoBehaviour
{
    [TextArea] public string note;
    public Vector2 size = Vector2.zero;   // 0 = sólo un punto
    public Color color = new Color(1f, 0.8f, 0.2f, 0.8f);

    private void OnDrawGizmos()
    {
        Gizmos.color = color;
        if (size == Vector2.zero) Gizmos.DrawWireSphere(transform.position, 0.35f);
        else Gizmos.DrawWireCube(transform.position, size);

#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        Vector3 labelPos = transform.position + new Vector3(-size.x * 0.5f, size.y * 0.5f + 0.4f, 0f);
        UnityEditor.Handles.Label(size == Vector2.zero ? transform.position + Vector3.up * 0.5f : labelPos, name);
#endif
    }
}
