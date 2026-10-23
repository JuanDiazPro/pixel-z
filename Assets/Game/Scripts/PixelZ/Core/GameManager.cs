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

        List<NavPoint> spawnPoints = map == 1 ? SetupRuins() : SetupCastle();

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

    // Los enemigos usan las mismas capas físicas que el jugador (Layer 1/2/3), así que chocan con
    // muros, props y bordes igual que él. En el castillo las flechas y bolas de fuego paran en los muros.
    private void SetupPhysicsLayers()
    {
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
    private List<NavPoint> SetupRuins()
    {
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
        return BuildRuinsNavigation();
    }

    // Navegación de las Ruinas: 3 niveles (Layer 1/2/3) unidos por las escaleras de Cainos.
    private List<NavPoint> BuildRuinsNavigation()
    {
        var levelLayers = new List<int>();
        var grounds = new Dictionary<int, List<Tilemap>>();
        var walls = new Dictionary<int, List<Tilemap>>();
        foreach (Tilemap tm in FindObjectsByType<Tilemap>())
        {
            if (tm.name.Contains("Shadow") || !tm.name.StartsWith("Layer ")) continue;
            int layer = LayerMask.NameToLayer(tm.name.Split('-')[0].Trim());   // "Layer 2 - Grass" -> capa "Layer 2"
            if (layer < 0) continue;
            if (!levelLayers.Contains(layer)) levelLayers.Add(layer);
            var dict = tm.name.Contains("Wall") ? walls : tm.name.Contains("Grass") || tm.name.Contains("Ground") ? grounds : null;
            if (dict == null) continue;
            if (!dict.TryGetValue(layer, out List<Tilemap> list)) dict[layer] = list = new List<Tilemap>();
            list.Add(tm);
        }
        levelLayers.Sort();
        if (levelLayers.Count == 0) return new List<NavPoint>();

        // Zona que cubren los suelos.
        Rect area = Rect.zero;
        bool first = true;
        foreach (List<Tilemap> list in grounds.Values)
            foreach (Tilemap tm in list)
            {
                tm.CompressBounds();
                Vector3 min = tm.CellToWorld(tm.cellBounds.min);
                Vector3 max = tm.CellToWorld(tm.cellBounds.max);
                Rect r = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                area = first ? r : Rect.MinMaxRect(Mathf.Min(area.xMin, r.xMin), Mathf.Min(area.yMin, r.yMin), Mathf.Max(area.xMax, r.xMax), Mathf.Max(area.yMax, r.yMax));
                first = false;
            }

        // Escaleras: el cuerpo pertenece al nivel superior y la línea del trigger une los dos niveles.
        var stairBodies = new List<(Bounds bounds, int upper)>();
        var stairLinks = new List<FlowField.StairLink>();
        foreach (StairsLayerTrigger t in FindObjectsByType<StairsLayerTrigger>())
        {
            int upper = LayerMask.NameToLayer(t.layerUpper);
            int lower = LayerMask.NameToLayer(t.layerLower);
            Collider2D line = t.GetComponent<Collider2D>();
            if (upper < 0 || lower < 0 || line == null) continue;

            Transform body = t.transform.parent != null ? t.transform.parent : t.transform;
            foreach (SpriteRenderer sr in body.GetComponentsInChildren<SpriteRenderer>())
                stairBodies.Add((sr.bounds, upper));

            // Lado "de abajo" de la línea según la orientación de la escalera.
            Vector2 outward = t.direction == StairsLayerTrigger.Direction.South ? Vector2.down
                            : t.direction == StairsLayerTrigger.Direction.North ? Vector2.up
                            : t.direction == StairsLayerTrigger.Direction.West ? Vector2.left : Vector2.right;
            Bounds b = line.bounds;
            bool horizontal = b.size.x >= b.size.y;
            float length = horizontal ? b.size.x : b.size.y;
            for (float k = 0.25f; k < length; k += 0.25f)
            {
                Vector2 point = horizontal ? new Vector2(b.min.x + k, b.center.y) : new Vector2(b.center.x, b.min.y + k);
                // Se conectan las celdas cercanas a ambos lados de la línea (no sólo una) para que no quede
                // ninguna celda al pie de la escalera desde la que el camino "rebote".
                foreach (float lowOffset in new[] { 0.15f, 0.6f })
                foreach (float upOffset in new[] { 0.15f, 0.6f })
                    stairLinks.Add(new FlowField.StairLink
                    {
                        lowerPoint = point + outward * lowOffset,
                        upperPoint = point - outward * upOffset,
                        lowerLayer = lower,
                        upperLayer = upper,
                    });
            }
        }

        int[] layers = levelLayers.ToArray();

        bool InStairs(int layer, Vector2 p)
        {
            foreach (var s in stairBodies)
                if (s.upper == layer && s.bounds.Contains(new Vector3(p.x, p.y, s.bounds.center.z))) return true;
            return false;
        }

        bool HasTile(Dictionary<int, List<Tilemap>> dict, int layer, Vector2 p)
        {
            if (!dict.TryGetValue(layer, out List<Tilemap> list)) return false;
            foreach (Tilemap tm in list)
                if (tm.HasTile(tm.WorldToCell(p))) return true;
            return false;
        }

        bool IsGround(int level, Vector2 p)
        {
            int layer = layers[level];
            if (InStairs(layer, p)) return true;
            return HasTile(grounds, layer, p) && !HasTile(walls, layer, p);
        }

        bool CanSpawn(int level, Vector2 p)
        {
            int layer = layers[level];
            if (InStairs(layer, p)) return false;
            foreach (var s in stairBodies)
                if (s.bounds.Contains(new Vector3(p.x, p.y, s.bounds.center.z))) return false;
            return true;
        }

        var field = gameObject.AddComponent<FlowField>();
        field.Build(area, layers, IsGround, CanSpawn, stairLinks);
        return field.SpawnPoints;
    }

    // Mapa 2: un solo nivel. La puerta de salida lleva de vuelta a las Ruinas pagando monedas.
    private List<NavPoint> SetupCastle()
    {
        foreach (SceneDoor d in FindObjectsByType<SceneDoor>())
        {
            d.requiresKey = false;
            d.price = GameSession.ReturnPrice;
            d.label = "Volver a las Ruinas";
        }

        CastleMap map = FindAnyObjectByType<CastleMap>();
        if (map == null) return new List<NavPoint>();

        map.floor.CompressBounds();
        Vector3 min = map.floor.CellToWorld(map.floor.cellBounds.min);
        Vector3 max = map.floor.CellToWorld(map.floor.cellBounds.max);
        Rect area = Rect.MinMaxRect(min.x, min.y, max.x, max.y);

        int layer = PlayerController.Instance != null ? PlayerController.Instance.gameObject.layer : LayerMask.NameToLayer("Layer 1");
        bool IsGround(int level, Vector2 p)
        {
            Vector3Int c = map.floor.WorldToCell(p);
            return map.floor.HasTile(c) && (map.walls == null || !map.walls.HasTile(map.walls.WorldToCell(p)));
        }
        bool CanSpawn(int level, Vector2 p) => p.y > -4f || Mathf.Abs(p.x) > 4f;   // no aparecer justo en la entrada

        var field = gameObject.AddComponent<FlowField>();
        field.Build(area, new[] { layer }, IsGround, CanSpawn, null);
        return field.SpawnPoints;
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
        if (kb == null || LoadingScreen.IsLoading || SceneTransition.IsBusy || CoinInput.AnyFocused) return;

        if (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)
        {
            if (menus.CasinoOpen) menus.CasinoBack();
            else if (menus.ShopOpen) menus.SetShop(false);
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
        Sfx.Play(Sfx.Clip.EnemyDeath, 0.7f);
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
        Sfx.StopHordeMusic();
        Sfx.Play(Sfx.Clip.GameOver);
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
