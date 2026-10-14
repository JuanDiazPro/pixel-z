using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Mapa de distancias hacia el jugador sobre las celdas transitables (BFS).
// Los enemigos lo siguen para rodear muros, árboles, cofres y muebles en lugar de quedarse atascados.
public class FlowField : MonoBehaviour
{
    public static FlowField Instance { get; private set; }

    public List<Vector2> WalkableCenters { get; } = new List<Vector2>();

    private GridLayout grid;
    private BoundsInt bounds;
    private bool[,] walkable;
    private int[,] dist;
    private float timer;
    private Vector2Int lastTarget = new Vector2Int(int.MinValue, 0);
    private readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();

    private static readonly Vector2Int[] Neighbours =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1),
    };

    // Castillo: suelo sin muro y sin muebles con colisión.
    public void Build(Tilemap floorMap, Tilemap wallMap, LayerMask obstacleMask)
    {
        floorMap.CompressBounds();
        var cells = new HashSet<Vector3Int>();
        foreach (Vector3Int cell in floorMap.cellBounds.allPositionsWithin)
        {
            if (!floorMap.HasTile(cell) || (wallMap != null && wallMap.HasTile(cell))) continue;
            if (IsBlocked(floorMap.GetCellCenterWorld(cell), obstacleMask)) continue;
            cells.Add(cell);
        }
        Build(floorMap, cells);
    }

    // Construcción genérica a partir de un conjunto de celdas de la rejilla indicada.
    public void Build(GridLayout layout, HashSet<Vector3Int> cells)
    {
        Instance = this;
        grid = layout;

        if (cells.Count == 0)
        {
            bounds = new BoundsInt(0, 0, 0, 1, 1, 1);
            walkable = new bool[1, 1];
            dist = new int[1, 1];
            return;
        }

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector3Int c in cells)
        {
            minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
            maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
        }
        bounds = new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        walkable = new bool[bounds.size.x, bounds.size.y];
        dist = new int[bounds.size.x, bounds.size.y];

        WalkableCenters.Clear();
        foreach (Vector3Int c in cells)
        {
            walkable[c.x - minX, c.y - minY] = true;
            WalkableCenters.Add(grid.CellToWorld(c) + grid.cellSize * 0.5f);
        }
    }

    public static bool IsBlocked(Vector2 center, LayerMask mask)
    {
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(center, new Vector2(0.7f, 0.7f), 0f, mask))
        {
            if (hit.isTrigger || hit is CompositeCollider2D || hit is TilemapCollider2D) continue;
            if (hit.attachedRigidbody != null && hit.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || walkable == null) return;

        timer -= Time.deltaTime;
        Vector2Int target = ToIndex(player.transform.position);
        if (timer > 0f && target == lastTarget) return;
        timer = 0.25f;
        lastTarget = target;
        Recompute(target);
    }

    private void Recompute(Vector2Int target)
    {
        int w = bounds.size.x, h = bounds.size.y;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            dist[x, y] = int.MaxValue;

        // Si el jugador está sobre una celda no transitable (p. ej. pegado a un mueble), parte de la vecina más cercana.
        if (!InGrid(target) || !walkable[target.x, target.y])
        {
            bool found = false;
            foreach (Vector2Int off in Neighbours)
            {
                Vector2Int n = target + off;
                if (InGrid(n) && walkable[n.x, n.y]) { target = n; found = true; break; }
            }
            if (!found) return;
        }

        queue.Clear();
        dist[target.x, target.y] = 0;
        queue.Enqueue(target);

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            int d = dist[c.x, c.y] + 1;
            for (int i = 0; i < 4; i++)
            {
                Vector2Int n = c + Neighbours[i];
                if (!InGrid(n) || !walkable[n.x, n.y] || dist[n.x, n.y] <= d) continue;
                dist[n.x, n.y] = d;
                queue.Enqueue(n);
            }
        }
    }

    // Dirección a seguir desde 'from' para acercarse al jugador. Si no hay camino, va en línea recta.
    public Vector2 DirectionTo(Vector2 from, Vector2 target)
    {
        Vector2 straight = (target - from).normalized;
        Vector2Int c = ToIndex(from);
        if (!InGrid(c) || dist[c.x, c.y] == int.MaxValue || dist[c.x, c.y] <= 1) return straight;

        int best = dist[c.x, c.y];
        Vector2Int bestCell = c;
        foreach (Vector2Int off in Neighbours)
        {
            Vector2Int n = c + off;
            if (!InGrid(n) || !walkable[n.x, n.y]) continue;
            // No cortar esquinas en diagonal.
            if (off.x != 0 && off.y != 0 && (!walkable[c.x + off.x, c.y] || !walkable[c.x, c.y + off.y])) continue;
            if (dist[n.x, n.y] < best)
            {
                best = dist[n.x, n.y];
                bestCell = n;
            }
        }
        if (bestCell == c) return straight;

        Vector2 next = grid.CellToWorld(new Vector3Int(bounds.xMin + bestCell.x, bounds.yMin + bestCell.y, 0)) + grid.cellSize * 0.5f;
        return (next - from).normalized;
    }

    private Vector2Int ToIndex(Vector2 world)
    {
        Vector3Int cell = grid.WorldToCell(world);
        return new Vector2Int(cell.x - bounds.xMin, cell.y - bounds.yMin);
    }

    private bool InGrid(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < bounds.size.x && c.y < bounds.size.y;
}
