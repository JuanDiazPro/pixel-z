using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Genera los muros del castillo alrededor del suelo pintado en el tilemap Floor.
//  - Celda vacía con suelo justo debajo           -> muro frontal (2 filas de alto)
//  - Celda vacía junto al suelo o al muro frontal  -> borde / parte superior del muro
// Todas las celdas de muro tienen colisión (TilemapCollider2D + CompositeCollider2D).
public static class CastleWallBuilder
{
    [MenuItem("Pixel Z/Castillo/Regenerar muros")]
    public static void RebuildFromMenu()
    {
        CastleMap map = Object.FindFirstObjectByType<CastleMap>();
        if (map == null)
        {
            EditorUtility.DisplayDialog("Castillo", "No hay ningún CastleMap en la escena abierta.", "OK");
            return;
        }
        Rebuild(map);
    }

    public static void Rebuild(CastleMap map)
    {
        Tilemap floor = map.floor;
        Tilemap walls = map.walls;
        Undo.RegisterCompleteObjectUndo(walls, "Regenerar muros");
        walls.ClearAllTiles();

        floor.CompressBounds();
        var floorCells = new HashSet<Vector2Int>();
        foreach (Vector3Int p in floor.cellBounds.allPositionsWithin)
            if (floor.HasTile(p)) floorCells.Add(new Vector2Int(p.x, p.y));

        bool IsFloor(int x, int y) => floorCells.Contains(new Vector2Int(x, y));

        // 1) Muros frontales
        var front = new Dictionary<Vector2Int, TileBase>();
        foreach (Vector2Int f in floorCells)
        {
            if (IsFloor(f.x, f.y + 1)) continue;
            front[new Vector2Int(f.x, f.y + 1)] = map.wallFrontBottom;
            if (!IsFloor(f.x, f.y + 2)) front[new Vector2Int(f.x, f.y + 2)] = map.wallFrontTop;
        }

        // 2) Bordes y parte superior alrededor del suelo y de los muros frontales
        var solid = new Dictionary<Vector2Int, TileBase>(front);
        var sources = new List<Vector2Int>(floorCells);
        sources.AddRange(front.Keys);
        foreach (Vector2Int s in sources)
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var c = new Vector2Int(s.x + dx, s.y + dy);
                if (IsFloor(c.x, c.y) || solid.ContainsKey(c)) continue;
                solid[c] = PickBorder(map, c, IsFloor);
            }
        }

        foreach (var kv in solid)
            walls.SetTile(new Vector3Int(kv.Key.x, kv.Key.y, 0), kv.Value);

        walls.CompressBounds();
        CompositeCollider2D composite = walls.GetComponent<CompositeCollider2D>();
        if (composite != null) composite.GenerateGeometry();

        EditorUtility.SetDirty(walls);
        EditorSceneManager.MarkSceneDirty(walls.gameObject.scene);
        Debug.Log($"[Castillo] Muros regenerados: {solid.Count} celdas alrededor de {floorCells.Count} celdas de suelo.");
    }

    private static TileBase PickBorder(CastleMap map, Vector2Int c, System.Func<int, int, bool> isFloor)
    {
        if (isFloor(c.x - 1, c.y)) return map.wallEdgeLeft;
        if (isFloor(c.x + 1, c.y)) return map.wallEdgeRight;
        if (isFloor(c.x, c.y + 1)) return map.wallEdgeTop;
        return map.wallTop;
    }
}
