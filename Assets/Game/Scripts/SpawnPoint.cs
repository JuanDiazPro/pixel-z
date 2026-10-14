using UnityEngine;

// Punto donde aparece el jugador al llegar desde una SceneDoor con el mismo id.
// Mueve al jugador y a la cámara juntos para que CameraFollow conserve su offset.
public class SpawnPoint : MonoBehaviour
{
    public string spawnId;

    private void Awake()
    {
        if (string.IsNullOrEmpty(spawnId) || SceneTransition.PendingSpawnId != spawnId) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) return;

        Vector3 delta = transform.position - player.transform.position;
        delta.z = 0f;
        player.transform.position += delta;

        Camera cam = Camera.main;
        if (cam != null) cam.transform.position += delta;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}
