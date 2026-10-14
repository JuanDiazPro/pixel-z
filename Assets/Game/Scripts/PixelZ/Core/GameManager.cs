using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Monta la partida en las escenas de juego: sustituye al Knight por el soldado elegido,
// crea HUD, menús, director de hordas, cofre/puerta (mapa 1) y navegación de enemigos (mapa 2).
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public int MapIndex { get; private set; }
    public bool IsOver { get; private set; }
    public LayerMask ProjectileWallMask { get; private set; }
    public int Combo { get; private set; }

    private float comboTimer;
    private GameMenus menus;
    private ChestShop chest;
    private SceneDoor door;

    // Objetivo actual que muestra el HUD (texto + flecha hacia el punto).
    public string ObjectiveText { get; private set; }
    public Vector2? ObjectiveTarget { get; private set; }

    public static void SetupScene(Scene scene)
    {
        int map = scene.name == "Game" ? 1 : scene.name == "Castle" ? 2 : 0;
        if (map == 0 || Instance != null) return;
        new GameObject("PIXEL Z - Game Manager").AddComponent<GameManager>().Init(map);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Init(int map)
    {
        Instance = this;
        MapIndex = map;
        Time.timeScale = 1f;

        if (!GameSession.RunActive) GameSession.NewRun(GameSession.SelectedClass);
        GameSession.MapReached = Mathf.Max(GameSession.MapReached, map);
        Loot.ResetPity();
        Enemy.All.Clear();
        Pickup.All.Clear();

        UIFactory.EnsureEventSystem();
        SetupPhysicsLayers();

        PlayerController player = SpawnPlayer();
        SetupCamera(player);

        List<Vector2> spawnPoints = map == 1 ? SetupRuins() : SetupCastle();

        GameHUD.Create();
        menus = GameMenus.Create();

        var director = gameObject.AddComponent<WaveDirector>();
        if (map == 1) director.Init(1, EnemyTypes.Ruins, EnemyTypes.RuinsBoss, spawnPoints, GameSession.Wave == 0 ? 5f : 6f, 10);
        else director.Init(2, EnemyTypes.Castle, EnemyTypes.CastleBoss, spawnPoints, GameSession.CastleBossDefeated ? 6f : 4f, 5, true, FindBossArena());

        if (map == 1)
            GameHUD.Banner("LAS RUINAS", GameSession.Wave == 0 ? "Sobrevive todo lo que puedas · Junta " + GameSession.ChestPrice + " monedas para abrir el cofre"
                                       : GameSession.HasKey ? "De vuelta en las Ruinas · Puedes volver al castillo gratis cuando quieras" : "");
        else
            GameHUD.Banner("EL CASTILLO", GameSession.CastleBossDefeated ? "Las hordas te esperan · Volver a las Ruinas cuesta " + GameSession.ReturnPrice + " monedas"
                                                                        : "Su guardián despierta...");

        if (map == 1 && GameSession.Wave == 0)
        {
            GameHUD.Toast("Click izquierdo: atacar hacia el ratón  ·  Espacio: dash", UIFactory.Cream);
            GameHUD.Toast("Click derecho / Q: habilidad de " + GameSession.Class.name, UIFactory.Cream);
        }
    }

    // El jefe del castillo aparece en el salón del trono (marcador "Trono" de la escena).
    private static Vector2? FindBossArena()
    {
        foreach (ZoneMarker z in FindObjectsByType<ZoneMarker>())
            if (z.name.Contains("Trono")) return (Vector2)z.transform.position + Vector2.down * 3f;
        return null;
    }

    // ---------- Montaje ----------

    private void SetupPhysicsLayers()
    {
        int enemy = LayerMask.NameToLayer("Enemy");
        if (enemy < 0)
        {
            Debug.LogWarning("[Pixel Z] Falta la capa 'Enemy'. Ejecuta  Pixel Z > Configurar juego.");
            return;
        }

        // Los enemigos sólo chocan entre sí (para no amontonarse) y, en el castillo, con muros y muebles.
        for (int i = 0; i < 32; i++)
        {
            bool collide = i == enemy || (MapIndex == 2 && i == 0);
            Physics2D.IgnoreLayerCollision(enemy, i, !collide);
        }
        ProjectileWallMask = MapIndex == 2 ? 1 << 0 : 0;
    }

    private PlayerController SpawnPlayer()
    {
        Vector3 pos = Vector3.zero;
        int layer = LayerMask.NameToLayer("Layer 1");
        string sorting = "Layer 1";
        int order = 2;

        // El Knight colocado en la escena marca el punto de aparición (SpawnPoint ya lo movió si venimos de otra puerta).
        KnightController knight = FindAnyObjectByType<KnightController>();
        if (knight != null)
        {
            pos = knight.transform.position;
            layer = knight.gameObject.layer;
            var sr = knight.GetComponent<SpriteRenderer>();
            if (sr != null) sorting = sr.sortingLayerName;
            var group = knight.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group != null) order = group.sortingOrder;
            knight.gameObject.tag = "Untagged";
            knight.gameObject.SetActive(false);
            Destroy(knight.gameObject);
        }

        return PlayerController.Create(pos, layer, sorting, order, GameSession.Class);
    }

    private static void SetupCamera(PlayerController player)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
        {
            follow.target = player.transform;
            follow.lerpSpeed = Mathf.Max(follow.lerpSpeed, 4f);
        }
        if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
    }

    // Mapa 1: cofre que vende la llave y puerta del sur que la exige.
    private List<Vector2> SetupRuins()
    {
        ProjectileWallMask = 0;

        GameObject chestGo = null;
        foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>())
            if (sr.name.Contains("Chest")) { chestGo = sr.gameObject; break; }
        if (chestGo == null)
        {
            chestGo = new GameObject("Chest");
            chestGo.transform.position = new Vector3(2f, 3f, 0f);
            var sr = chestGo.AddComponent<SpriteRenderer>();
            sr.sprite = PixelZDatabase.I.chest;
            sr.sortingLayerName = "Layer 1";
            sr.sortingOrder = 2;
        }
        if (chestGo.GetComponent<Collider2D>() == null)
        {
            var box = chestGo.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.9f, 0.5f);
            box.offset = new Vector2(0f, 0.25f);
        }
        chest = chestGo.AddComponent<ChestShop>();
        chest.openSprite = PixelZDatabase.I.chestOpen;

        foreach (SceneDoor d in FindObjectsByType<SceneDoor>())
        {
            d.requiresKey = true;
            d.label = "Entrar al castillo";
            door = d;
        }

        Breakable.SetupAll();
        BuildEnemyObstacles();
        return BuildRuinsFlowField();
    }

    // Los enemigos sólo chocan con la capa Enemy: se crea una copia de cada colisión de props
    // (árboles, cofre, estatuas, pilares, pozo...) en esa capa para que no los atraviesen.
    private void BuildEnemyObstacles()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) return;

        var root = new GameObject("Enemy Obstacles").transform;
        foreach (Collider2D c in FindObjectsByType<Collider2D>())
        {
            if (c.isTrigger || c is CompositeCollider2D || c is TilemapCollider2D || c.gameObject.layer == enemyLayer) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
            if (!IsProp(c.transform)) continue;   // los bordes de escaleras/plataformas no: los enemigos cruzan niveles

            var go = new GameObject(c.name);
            go.layer = enemyLayer;
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(c.transform.position, c.transform.rotation);
            go.transform.localScale = c.transform.lossyScale;

            switch (c)
            {
                case BoxCollider2D b:
                    var nb = go.AddComponent<BoxCollider2D>();
                    nb.size = b.size;
                    nb.offset = b.offset;
                    break;
                case CircleCollider2D ci:
                    var nc = go.AddComponent<CircleCollider2D>();
                    nc.radius = ci.radius;
                    nc.offset = ci.offset;
                    break;
                case CapsuleCollider2D ca:
                    var ncap = go.AddComponent<CapsuleCollider2D>();
                    ncap.size = ca.size;
                    ncap.offset = ca.offset;
                    ncap.direction = ca.direction;
                    break;
                case PolygonCollider2D po:
                    var np = go.AddComponent<PolygonCollider2D>();
                    np.offset = po.offset;
                    np.pathCount = po.pathCount;
                    for (int i = 0; i < po.pathCount; i++) np.SetPath(i, po.GetPath(i));
                    break;
                default:
                    Destroy(go);
                    continue;
            }

            // Si el prop se puede romper, su copia desaparece con él.
            Breakable breakable = c.GetComponentInParent<Breakable>();
            if (breakable != null) breakable.AddObstacleCopy(go);
        }
    }

    private static bool IsProp(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name.StartsWith("PF Struct")) return false;
            if (t.name.StartsWith("PF Props") || t.name.StartsWith("PF Plant")) return true;
        }
        return false;
    }

    // Celdas con suelo (hierba o piedra) que no son muro ni están ocupadas por props, más las escaleras.
    // Sirve para que los enemigos rodeen obstáculos y para elegir dónde aparecen.
    private List<Vector2> BuildRuinsFlowField()
    {
        var grounds = new List<Tilemap>();
        var walls = new List<Tilemap>();
        foreach (Tilemap tm in FindObjectsByType<Tilemap>())
        {
            if (tm.name.Contains("Shadow")) continue;
            if (tm.name.Contains("Wall")) walls.Add(tm);
            else if (tm.name.Contains("Grass") || tm.name.Contains("Ground")) grounds.Add(tm);
        }
        if (grounds.Count == 0) return new List<Vector2>();

        Tilemap reference = grounds[0];
        int enemyMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Enemy"));
        var cells = new HashSet<Vector3Int>();
        var spawn = new List<Vector2>();

        foreach (Tilemap g in grounds)
        {
            g.CompressBounds();
            foreach (Vector3Int cell in g.cellBounds.allPositionsWithin)
            {
                if (!g.HasTile(cell)) continue;
                Vector3 world = g.GetCellCenterWorld(cell);
                Vector3Int refCell = reference.WorldToCell(world);
                if (cells.Contains(refCell)) continue;

                bool isWall = false;
                foreach (Tilemap w in walls)
                    if (w.HasTile(w.WorldToCell(world))) { isWall = true; break; }
                if (isWall || FlowField.IsBlocked(world, enemyMask)) continue;

                cells.Add(refCell);
                spawn.Add(world);
            }
        }

        // Las escaleras unen los niveles: también son transitables (pero no se aparece en ellas).
        foreach (Transform t in FindObjectsByType<Transform>())
        {
            if (!t.name.Contains("Stairs") || t.GetComponent<StairsLayerTrigger>() != null) continue;
            foreach (SpriteRenderer sr in t.GetComponentsInChildren<SpriteRenderer>())
            {
                Bounds b = sr.bounds;
                for (float x = b.min.x + 0.5f; x < b.max.x; x += 1f)
                for (float y = b.min.y + 0.5f; y < b.max.y; y += 1f)
                    cells.Add(reference.WorldToCell(new Vector3(x, y, 0f)));
            }
        }

        gameObject.AddComponent<FlowField>().Build(reference, cells);
        return spawn;
    }

    // Mapa 2: los enemigos chocan con los muros y se orientan con un flow field.
    // La puerta de salida lleva de vuelta a las Ruinas pagando monedas.
    private List<Vector2> SetupCastle()
    {
        foreach (SceneDoor d in FindObjectsByType<SceneDoor>())
        {
            d.requiresKey = false;
            d.price = GameSession.ReturnPrice;
            d.label = "Volver a las Ruinas";
        }

        CastleMap map = FindAnyObjectByType<CastleMap>();
        if (map == null) return new List<Vector2>();

        var field = gameObject.AddComponent<FlowField>();
        field.Build(map.floor, map.walls, 1 << 0);

        // No aparecer justo en la entrada.
        var points = new List<Vector2>();
        foreach (Vector2 p in field.WalkableCenters)
            if (p.y > -4f || Mathf.Abs(p.x) > 4f) points.Add(p);
        return points;
    }

    // ---------- Bucle ----------

    private void Update()
    {
        if (IsOver) return;
        UpdateObjective();

        if (Time.timeScale > 0f) GameSession.TimeAlive += Time.deltaTime;

        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f) Combo = 0;

        Keyboard kb = Keyboard.current;
        if (kb == null || LoadingScreen.IsLoading || SceneTransition.IsBusy) return;

        if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
        {
            if (menus.ShopOpen) menus.SetShop(false);
            else menus.SetPause(!menus.PauseOpen);
        }
        else if (kb.bKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
        {
            if (menus.ShopOpen) menus.SetShop(false);
            else if (!menus.PauseOpen) menus.TryOpenShop();
        }
        else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !menus.AnyOpen)
        {
            WaveDirector.Instance?.SkipBreak();
        }
    }

    private void UpdateObjective()
    {
        ObjectiveTarget = null;
        if (MapIndex == 1)
        {
            Pickup key = Pickup.All.Find(p => p != null && p.Type == PickupType.Key);
            if (!GameSession.ChestOpened && chest != null)
            {
                bool rich = GameSession.Coins >= GameSession.ChestPrice;
                ObjectiveText = rich ? "¡Ya puedes comprar el cofre! Ve al centro y pulsa E"
                                     : "Junta monedas para el cofre: " + GameSession.Coins + " / " + GameSession.ChestPrice;
                if (rich) ObjectiveTarget = chest.transform.position;
            }
            else if (!GameSession.HasKey && key != null)
            {
                ObjectiveText = "Recoge la llave";
                ObjectiveTarget = key.transform.position;
            }
            else if (GameSession.HasKey && door != null)
            {
                ObjectiveText = "Entra al castillo por la puerta del sur";
                ObjectiveTarget = door.transform.position;
            }
            else ObjectiveText = "Sobrevive";
        }
        else
        {
            Enemy boss = WaveDirector.Instance != null ? WaveDirector.Instance.Boss : null;
            if (boss != null && boss.Alive)
            {
                ObjectiveText = "Derrota a " + boss.Def.name;
                ObjectiveTarget = boss.transform.position;
            }
            else ObjectiveText = "Sobrevive en el castillo todo lo que puedas";
        }
    }

    public void OnEnemyKilled(Enemy enemy)
    {
        GameSession.Kills++;
        Combo++;
        comboTimer = 2.5f;

        if (Combo > 0 && Combo % 10 == 0)
        {
            int bonus = Combo / 2;
            GameSession.AddCoins(bonus);
            GameHUD.Toast("¡COMBO x" + Combo + "!  +" + bonus + " monedas", new Color(1f, 0.6f, 0.25f));
        }
        if (enemy.Def.boss)
            GameHUD.Banner("¡" + enemy.Def.name.ToUpper() + " DERROTADO!", "Ha soltado un gran botín");
    }

    public void OnPlayerDied()
    {
        if (IsOver) return;
        IsOver = true;
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        Time.timeScale = 0.4f;
        yield return new WaitForSecondsRealtime(1.6f);
        Time.timeScale = 1f;
        menus.ShowGameOver();
    }
}
