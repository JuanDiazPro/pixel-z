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
// Cada pocos frames se calcula el coste de cada celda hasta el jugador (Dijkstra con diagonales que penaliza
// pegarse a los muros) y los enemigos bajan por él. Sobre ese campo, NextWaypoint "estira" el camino: apunta a la
// celda más lejana del camino que se ve sin chocar, en vez de ir de centro en centro rozando esquinas.
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
    private byte[] clear;                    // holgura: celdas hasta el obstáculo más cercano (1..3)
    private int[] dist;
    private ContactFilter2D[] filters;
    private readonly List<int>[] buckets = new List<int>[Buckets];
    private readonly List<int> path = new List<int>();
    private readonly Collider2D[] hits = new Collider2D[8];
    private readonly RaycastHit2D[] rayHits = new RaycastHit2D[8];
    private float timer;
    private int lastTarget = -1;

    private const int StepCost = 10, DiagCost = 14, Buckets = 32;   // Buckets > coste máximo de un paso
    private const int LookAhead = 10;

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
        clear = new byte[count];
        dist = new int[count];
        filters = new ContactFilter2D[levels];
        for (int l = 0; l < levels; l++) filters[l] = FilterFor(layers[l]);
        for (int b = 0; b < Buckets; b++) buckets[b] = buckets[b] ?? new List<int>();
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

        // 3) Holgura: lo cerca que está cada celda de un obstáculo, para que los caminos no vayan pegados a los muros
        //    (los enemigos grandes se quedaban enganchados en las esquinas y en los pasillos estrechos).
        for (int l = 0; l < levels; l++)
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int i = Index(l, x, y);
            if (walk[i]) clear[i] = Clearance(l, x, y);
        }

        // 4) Escaleras.
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

    private byte Clearance(int l, int x, int y)
    {
        int i = Index(l, x, y);
        // Un borde fino entre dos celdas transitables también cuenta como obstáculo pegado.
        if ((x + 1 < w && walk[i + 1] && (pass[i] & 1) == 0) || (x > 0 && walk[i - 1] && (pass[i] & 2) == 0) ||
            (y + 1 < h && walk[i + w] && (pass[i] & 4) == 0) || (y > 0 && walk[i - w] && (pass[i] & 8) == 0))
            return 1;
        for (int r = 1; r <= 2; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h || !walk[Index(l, nx, ny)]) return (byte)r;
        }
        return 3;
    }

    private int Penalty(int i) => clear[i] == 1 ? 8 : clear[i] == 2 ? 3 : 0;

    // Diagonal desde n en (sx, sy) sólo si los dos lados están libres (no cortar esquinas).
    private bool Diagonal(int n, int sx, int sy)
    {
        int bx = sx > 0 ? 1 : 2;
        int by = sy > 0 ? 4 : 8;
        int p = pass[n];
        return (p & bx) != 0 && (p & by) != 0 && (pass[n + sx] & by) != 0 && (pass[n + sy * w] & bx) != 0;
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

        // Dijkstra con cola de cubos (los costes son enteros pequeños): lineal en el número de celdas.
        foreach (List<int> b in buckets) b.Clear();
        dist[target] = 0;
        buckets[0].Add(target);
        int pending = 1;
        for (int d = 0; pending > 0; d++)
        {
            List<int> bucket = buckets[d % Buckets];
            for (int k = 0; k < bucket.Count; k++)
            {
                int c = bucket[k];
                pending--;
                if (dist[c] != d) continue;   // entrada vieja: ya se llegó por un camino más barato
                byte p = pass[c];
                if ((p & 1) != 0) Relax(c + 1, d + StepCost, ref pending);
                if ((p & 2) != 0) Relax(c - 1, d + StepCost, ref pending);
                if ((p & 4) != 0) Relax(c + w, d + StepCost, ref pending);
                if ((p & 8) != 0) Relax(c - w, d + StepCost, ref pending);
                if (Diagonal(c, 1, 1)) Relax(c + 1 + w, d + DiagCost, ref pending);
                if (Diagonal(c, -1, 1)) Relax(c - 1 + w, d + DiagCost, ref pending);
                if (Diagonal(c, 1, -1)) Relax(c + 1 - w, d + DiagCost, ref pending);
                if (Diagonal(c, -1, -1)) Relax(c - 1 - w, d + DiagCost, ref pending);
                if (links.TryGetValue(c, out List<int> list))
                    foreach (int n in list) Relax(n, d + StepCost, ref pending);
            }
            bucket.Clear();
        }
    }

    private void Relax(int n, int d, ref int pending)
    {
        d += Penalty(n);
        if (dist[n] <= d) return;
        dist[n] = d;
        buckets[d % Buckets].Add(n);
        pending++;
    }

    public bool IsReachable(NavPoint point)
    {
        int n = NearestWalkable(LevelOf(point.layer), point.position);
        return n >= 0 && dist[n] != int.MaxValue;
    }

    // Coste del camino hasta el jugador desde una posición (int.MaxValue si no hay camino).
    public int CostAt(Vector2 pos, int layer)
    {
        int n = NearestWalkable(LevelOf(layer), pos);
        return n >= 0 ? dist[n] : int.MaxValue;
    }

    // Siguiente punto al que debe ir un cuerpo de radio 'radius' que está en 'from' (capa física 'layer') para
    // llegar a 'target'. Devuelve false si no hay datos de navegación (entonces lo normal es ir en línea recta).
    public bool NextWaypoint(Vector2 from, int layer, float radius, Vector2 target, int targetLayer, out Vector2 waypoint)
    {
        waypoint = target;
        int level = LevelOf(layer);
        if (level < 0 || walk == null) return false;
        ContactFilter2D filter = filters[level];

        // Si ve al jugador en su mismo nivel y está cerca, va directo.
        if (layer == targetLayer && (target - from).sqrMagnitude < 64f && CircleClear(from, target, radius, filter))
            return true;

        int n = NearestWalkable(level, from);
        if (n < 0)
        {
            // Fuera de la rejilla (empujado contra un borde, etc.): primero vuelve a una celda transitable.
            n = NearestWalkable(level, from, 5);
            if (n < 0) return false;
            waypoint = PositionOf(n);
            return true;
        }
        if (dist[n] == int.MaxValue) return false;

        BuildPath(n, NeededClearance(radius));
        if (path.Count == 0) return true;

        // Estira el camino: la celda más lejana a la que se llega en línea recta sin chocar.
        for (int i = path.Count - 1; i >= 1; i--)
        {
            Vector2 p = PositionOf(path[i]);
            if (CircleClear(from, p, radius, filter))
            {
                waypoint = p;
                return true;
            }
        }
        waypoint = PositionOf(path[0]);
        return true;
    }

    // Punto unas celdas más adelante en el camino, en el mismo nivel (para recolocar a un enemigo atascado).
    public bool PointAhead(Vector2 from, int layer, int steps, out Vector2 point)
    {
        point = from;
        int level = LevelOf(layer);
        int n = NearestWalkable(level, from, 5);
        if (n < 0 || dist[n] == int.MaxValue) return false;
        BuildPath(n, 2);
        int last = n;
        for (int i = 0; i < path.Count && i < steps; i++)
        {
            if (path[i] / (w * h) != level) break;
            last = path[i];
        }
        point = PositionOf(last);
        return last != n;
    }

    // Celda despejada cercana (con holgura y camino al jugador) a la que apartarse para desatascarse.
    public bool SafeSpot(Vector2 from, int layer, out Vector2 spot)
    {
        spot = from;
        int level = LevelOf(layer);
        if (level < 0 || walk == null) return false;
        int cx = Mathf.FloorToInt((from.x - origin.x) / Cell);
        int cy = Mathf.FloorToInt((from.y - origin.y) / Cell);
        int best = -1;
        float bestScore = float.MinValue;
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            int x = cx + dx, y = cy + dy;
            if (x < 0 || y < 0 || x >= w || y >= h) continue;
            int i = Index(level, x, y);
            if (!walk[i] || dist[i] == int.MaxValue) continue;
            float score = clear[i] * 10f - dist[i] * 0.05f - (CenterOf(x, y) - from).magnitude;
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        if (best < 0) return false;
        spot = PositionOf(best);
        return true;
    }

    // ¿Hay línea de visión en ese nivel? (sin muros ni props en medio)
    public bool LineOfSight(Vector2 from, Vector2 to, int layer)
    {
        int level = LevelOf(layer);
        return level < 0 || filters == null || Clear(from, to, filters[level]);
    }

    private static int NeededClearance(float radius) => radius > 0.3f ? 2 : 1;

    // Baja por el campo de costes desde n, hasta LookAhead celdas o hasta cruzar una escalera.
    private void BuildPath(int n, int needClear)
    {
        path.Clear();
        int level = n / (w * h);
        int cur = n;
        for (int s = 0; s < LookAhead; s++)
        {
            int next = Downhill(cur, needClear);
            if (next < 0) break;
            path.Add(next);
            if (next / (w * h) != level) break;
            cur = next;
        }
    }

    // Vecino con menor coste; si lo hay, uno con holgura suficiente para el tamaño del enemigo.
    private int Downhill(int n, int needClear)
    {
        int best = -1, bestDist = dist[n];
        int roomy = -1, roomyDist = dist[n];
        byte p = pass[n];

        void Try(int c)
        {
            int d = dist[c];
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
            if (d < roomyDist && clear[c] >= needClear)
            {
                roomyDist = d;
                roomy = c;
            }
        }

        if ((p & 1) != 0) Try(n + 1);
        if ((p & 2) != 0) Try(n - 1);
        if ((p & 4) != 0) Try(n + w);
        if ((p & 8) != 0) Try(n - w);
        if (Diagonal(n, 1, 1)) Try(n + 1 + w);
        if (Diagonal(n, -1, 1)) Try(n - 1 + w);
        if (Diagonal(n, 1, -1)) Try(n + 1 - w);
        if (Diagonal(n, -1, -1)) Try(n - 1 - w);
        // Escaleras: caminar hacia la celda del otro nivel cruza la línea de la escalera y cambia de capa.
        if (links.TryGetValue(n, out List<int> list))
            foreach (int other in list) Try(other);

        return roomy >= 0 ? roomy : best;
    }

    // ¿Cabe un círculo de radio 'radius' yendo de a a b? (algo más estrecho para no contar los roces)
    private bool CircleClear(Vector2 a, Vector2 b, float radius, ContactFilter2D filter)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.001f) return true;
        int n = Physics2D.CircleCast(a, radius * 0.85f, d / len, filter, rayHits, len);
        for (int i = 0; i < n; i++)
            if (IsStatic(rayHits[i].collider)) return false;
        return true;
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

    private Vector2 PositionOf(int i) => CenterOf(i % w, (i / w) % h);

    // Celda transitable más cercana a una posición (la propia o una vecina hasta 'maxRing' celdas), o -1.
    private int NearestWalkable(int level, Vector2 pos, int maxRing = 2)
    {
        if (level < 0 || walk == null) return -1;
        int cx = Mathf.FloorToInt((pos.x - origin.x) / Cell);
        int cy = Mathf.FloorToInt((pos.y - origin.y) / Cell);
        int best = -1;
        float bestD = float.MaxValue;
        for (int r = 0; r <= maxRing && best < 0; r++)
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
