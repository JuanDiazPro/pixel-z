using UnityEngine;
using UnityEngine.Tilemaps;

// Datos del mapa del castillo.
// Pinta el suelo en el tilemap "Floor" y usa el menú  Pixel Z > Castillo > Regenerar muros
// para que los muros y sus colisiones se generen solos alrededor del suelo.
public class CastleMap : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap floor;
    public Tilemap walls;

    [Header("Tiles de muro")]
    public TileBase wallFrontBottom;   // fila inferior del muro frontal (pegada al suelo)
    public TileBase wallFrontTop;      // fila superior del muro frontal
    public TileBase wallTop;           // parte superior oscura del muro
    public TileBase wallEdgeLeft;      // borde con el suelo a la izquierda
    public TileBase wallEdgeRight;     // borde con el suelo a la derecha
    public TileBase wallEdgeTop;       // borde con el suelo arriba (muro sur)
}
