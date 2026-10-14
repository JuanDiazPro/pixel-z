using UnityEngine;
using UnityEngine.Tilemaps;

// Tile que repite un patrón de varios sprites según la posición de la celda.
// Ej.: un suelo de 2x2 tiles se pinta con un solo tile y siempre queda alineado.
// sprites se ordena por filas empezando abajo a la izquierda: índice = x + y * width.
[CreateAssetMenu(menuName = "Pixel Z/Pattern Tile")]
public class PatternTile : TileBase
{
    public int width = 1;
    public int height = 1;
    public Sprite[] sprites;
    public Tile.ColliderType colliderType = Tile.ColliderType.None;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = GetSprite(position);
        tileData.color = Color.white;
        tileData.flags = TileFlags.LockTransform;
        tileData.colliderType = colliderType;
    }

    private Sprite GetSprite(Vector3Int p)
    {
        if (sprites == null || sprites.Length == 0) return null;
        int x = Mod(p.x, Mathf.Max(width, 1));
        int y = Mod(p.y, Mathf.Max(height, 1));
        int i = x + y * width;
        return i < sprites.Length ? sprites[i] : sprites[0];
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;
}
