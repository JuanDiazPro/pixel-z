using System;
using System.Collections.Generic;
using UnityEngine;

// Punto del mapa en un nivel concreto (capa física "Layer 1/2/3").
public struct NavPoint
{
    public Vector2 position;
    public int layer;

    public NavPoint(Vector2 position, int layer)
    {
        this.position = position;
        this.layer = layer;
    }
}

// Navegación por niveles para los enemigos.
// Divide el mapa en celdas de medio tile, una rejilla por nivel (capa física). Una celda es transitable si hay
// suelo y no choca con nada de ese nivel, igual que el jugador. Las escaleras unen niveles: cruzar su línea
// cambia la capa del enemigo (StairsLayerTrigger de Cainos), así que el camino las usa como "puentes".
// Cada pocos frames se calcula la distancia de cada celda al jugador (BFS) y los enemigos bajan por ella.
public class FlowField : MonoBehaviour
{
    public static FlowField Instance { get; private set; }

    public const float Cell = 0.5f;

    public List<NavPoint> SpawnPoints { get; } = new List<NavPoint>();
    public int DefaultLayer => layers != null && layers.Length > 0 ? layers[0] : 0;

    private int[] layers;
    private int levels, w, h;
    private Vector2 origin;
    private bool[] walk;
    private byte[] pass;                     // bits: 1 = +x, 2 = -x, 4 = +y, 8 = -y
    private readonly Dictionary<int, List<int>> links = new Dictionary<int, List<int>>();
    private int[] dist;
    private readonly Queue<int> queue = new Queue<int>();
    private readonly Collider2D[] hits = new Collider2D[8];
    private readonly RaycastHit2D[] rayHits = new RaycastHit2D[8];
    private float timer;
    private int lastTarget = -1;

    // Enlace entre dos niveles a través de una escalera (punto abajo ↔ punto arriba).
    public struct StairLink
    {
        public Vector2 lowerPoint, upperPoint;
        public int lowerLayer, upperLayer;
    }

    public void Build(Rect area, int[] physicsLayers, Func<int, Vector2, bool> isGround, Func<int, Vector2, bool> canSpawn, List<StairLink> stairLinks)
    {
        Instance = this;
        layers = physicsLayers;
        levels = layers.Length;
        origin = area.min;
        w = Mathf.CeilToInt(area.width / Cell);
        h = Mathf.CeilToInt(area.height / Cell);
        int count = levels * w * h;
        walk = new bool[count];
        pass = new byte[count];
        dist = new int[count];
        links.Clear();
        SpawnPoints.Clear();

        // 1) Celdas transitables por nivel.
        for (int l = 0; l < levels; l++)
        {
            ContactFilter2D filter = FilterFor(layers[l]);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = CenterOf(x, y);
                if (!isGround(l, p) || Blocked(p, filter)) continue;
                walk[Index(l, x, y)] = true;
                if (canSpawn(l, p)) SpawnPoints.Add(new NavPoint(p, layers[l]));
            }
        }

        // 2) Conexiones entre celdas vecinas sin nada en medio (bordes finos, barandillas de escaleras...).
        for (int l = 0; l < levels; l++)
        {
            ContactFilter2D filter = FilterFor(layers[l]);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = Index(l, x, y);
                if (!walk[i]) continue;
                Vector2 p = CenterOf(x, y);
                if (x + 1 < w && walk[i + 1] && Clear(p, CenterOf(x + 1, y), filter))
                {
                    pass[i] |= 1;
                    pass[i + 1] |= 2;
                }
                if (y + 1 < h && walk[i + w] && Clear(p, CenterOf(x, y + 1), filter))
                {
                    pass[i] |= 4;
                    pass[i + w] |= 8;
                }
            }
        }

        // 3) Escaleras.
        if (stairLinks != null)
        {
            foreach (StairLink s in stairLinks)
            {
                int a = NearestWalkable(LevelOf(s.lowerLayer), s.lowerPoint);
                int b = NearestWalkable(LevelOf(s.upperLayer), s.upperPoint);
                if (a < 0 || b < 0) continue;
                AddLink(a, b);
                AddLink(b, a);
            }
        }

        for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
    }

    private void AddLink(int from, int to)
    {
        if (!links.TryGetValue(from, out List<int> list)) links[from] = list = new List<int>();
        if (!list.Contains(to)) list.Add(to);
    }

    private static ContactFilter2D FilterFor(int layer)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(layer));
        filter.useTriggers = false;
        return filter;
    }

    private bool Blocked(Vector2 p, ContactFilter2D filter)
    {
        int n = Physics2D.OverlapBox(p, new Vector2(0.36f, 0.36f), 0f, filter, hits);
        for (int i = 0; i < n; i++)
            if (IsStatic(hits[i])) return true;
        return false;
    }

    private bool Clear(Vector2 a, Vector2 b, ContactFilter2D filter)
    {
        int n = Physics2D.Linecast(a, b, filter, rayHits);
        for (int i = 0; i < n; i++)
            if (IsStatic(rayHits[i].collider)) return false;
        return true;
    }

    // Personajes y enemigos (cuerpos dinámicos) no cuentan como obstáculos.
    private static bool IsStatic(Collider2D c) =>
        c != null && !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody.bodyType != RigidbodyType2D.Dynamic);

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Distancias al jugador ----------

    private void Update()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null || walk == null) return;

        timer -= Time.deltaTime;
        int target = NearestWalkable(LevelOf(player.gameObject.layer), player.transform.position);
        if (timer > 0f && target == lastTarget) return;
        timer = 0.2f;
        lastTarget = target;
        Recompute(target);
    }

    private void Recompute(int target)
    {
        for (int i = 0; i < dist.Length; i++) dist[i] = int.MaxValue;
        if (target < 0) return;

        queue.Clear();
        dist[target] = 0;
        queue.Enqueue(target);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int d = dist[c] + 1;
            byte p = pass[c];
            if ((p & 1) != 0) Visit(c + 1, d);
            if ((p & 2) != 0) Visit(c - 1, d);
            if ((p & 4) != 0) Visit(c + w, d);
            if ((p & 8) != 0) Visit(c - w, d);
            if (links.TryGetValue(c, out List<int> list))
                foreach (int n in list) Visit(n, d);
        }
    }

    private void Visit(int n, int d)
    {
        if (dist[n] <= d) return;
        dist[n] = d;
        queue.Enqueue(n);
    }

    public bool IsReachable(NavPoint point)
    {
        int n = NearestWalkable(LevelOf(point.layer), point.position);
        return n >= 0 && dist[n] != int.MaxValue;
    }

    // Dirección a seguir desde 'from' (en la capa física 'layer') para acercarse al jugador.
    public Vector2 DirectionTo(Vector2 from, int layer, Vector2 target)
    {
        Vector2 straight = (target - from).normalized;
        int level = LevelOf(layer);
        int n = NearestWalkable(level, from);
        if (n < 0 || dist[n] == int.MaxValue || dist[n] <= 2) return straight;

        int best = n;
        int bestDist = dist[n];
        int x = n % w;
        int y = (n / w) % h;
        byte p = pass[n];

        void Try(int candidate)
        {
            if (dist[candidate] < bestDist)
            {
                bestDist = dist[candidate];
                best = candidate;
            }
        }

        if ((p & 1) != 0) Try(n + 1);
        if ((p & 2) != 0) Try(n - 1);
        if ((p & 4) != 0) Try(n + w);
        if ((p & 8) != 0) Try(n - w);
        // Diagonales sólo si ambos lados están libres (no cortar esquinas).
        if ((p & 1) != 0 && (p & 4) != 0 && (pass[n + 1] & 4) != 0 && (pass[n + w] & 1) != 0) Try(n + 1 + w);
        if ((p & 2) != 0 && (p & 4) != 0 && (pass[n - 1] & 4) != 0 && (pass[n + w] & 2) != 0) Try(n - 1 + w);
        if ((p & 1) != 0 && (p & 8) != 0 && (pass[n + 1] & 8) != 0 && (pass[n - w] & 1) != 0) Try(n + 1 - w);
        if ((p & 2) != 0 && (p & 8) != 0 && (pass[n - 1] & 8) != 0 && (pass[n - w] & 2) != 0) Try(n - 1 - w);
        // Escaleras: caminar hacia la celda del otro nivel cruza la línea de la escalera y cambia de capa.
        if (links.TryGetValue(n, out List<int> list))
            foreach (int other in list) Try(other);

        if (best == n) return straight;
        int bx = best % w;
        int by = (best / w) % h;
        Vector2 dir = CenterOf(bx, by) - from;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : straight;
    }

    // ---------- Utilidades de rejilla ----------

    private int LevelOf(int layer)
    {
        if (layers == null) return -1;
        for (int i = 0; i < layers.Length; i++)
            if (layers[i] == layer) return i;
        return layers.Length == 1 ? 0 : -1;
    }

    private Vector2 CenterOf(int x, int y) => origin + new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell);

    private int Index(int l, int x, int y) => (l * h + y) * w + x;

    // Celda transitable más cercana a una posición (la propia o una vecina), o -1.
    private int NearestWalkable(int level, Vector2 pos)
    {
        if (level < 0 || walk == null) return -1;
        int cx = Mathf.FloorToInt((pos.x - origin.x) / Cell);
        int cy = Mathf.FloorToInt((pos.y - origin.y) / Cell);
        int best = -1;
        float bestD = float.MaxValue;
        for (int r = 0; r <= 2 && best < 0; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            int x = cx + dx, y = cy + dy;
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            int i = Index(level, x, y);
            if (!walk[i]) continue;
            float d = (CenterOf(x, y) - pos).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        return best;
    }
}
