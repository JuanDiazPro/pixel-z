using System.Collections.Generic;
using UnityEngine;

// Sistema de hordas infinitas.
// La dificultad depende de la ronda dentro del mapa (LocalWave): la ronda 1 es casi un tutorial
// (3 orcos lentos que mueren de un golpe) y a partir de la 15-20 es una auténtica locura.
// Cada 5 rondas aparece un jefe. En el castillo, al llegar, primero se pelea sólo contra el Archidemonio
// y después las hordas vuelven a empezar suaves.
public class WaveDirector : MonoBehaviour
{
    public static WaveDirector Instance { get; private set; }

    public enum Phase { Intro, Break, Fighting }

    public Phase CurrentPhase { get; private set; } = Phase.Intro;
    public float BreakRemaining { get; private set; }
    public int LocalWave { get; private set; }
    public int EnemiesLeft => toSpawn + Enemy.All.Count;
    public bool CanShop => CurrentPhase != Phase.Fighting;
    public bool BossOnlyRound { get; private set; }
    public Enemy Boss { get; private set; }

    private EnemyDef[] pool;
    private EnemyDef bossDef;
    private List<NavPoint> spawnPoints;
    private Vector2? bossArena;
    private bool introBossPending;
    private int mapIndex;
    private int bossEvery = 5;
    private int toSpawn;
    private float spawnTimer;

    private const float BreakTime = 10f;

    public void Init(int map, EnemyDef[] enemyPool, EnemyDef boss, List<NavPoint> points, float introTime, int bossEveryWaves, bool startWithBoss = false, Vector2? arena = null)
    {
        mapIndex = map;
        LocalWave = map == 1 ? GameSession.RuinsWave : GameSession.CastleWave;   // continúa donde se quedó
        bossEvery = Mathf.Max(1, bossEveryWaves);
        Instance = this;
        pool = enemyPool;
        bossDef = boss;
        spawnPoints = points;
        BreakRemaining = introTime;
        CurrentPhase = Phase.Intro;
        introBossPending = startWithBoss && !GameSession.CastleBossDefeated;
        bossArena = arena;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Curva de dificultad (w = ronda dentro del mapa) ----------

    public static int EnemyCount(int w) => 2 + w + w * w / 8;                                   // 3, 4, 6, 8, 10 ... 22 (r10) ... 72 (r20)
    public static float HpMult(int w) => 0.5f + 0.12f * (w - 1) + 0.012f * (w - 1) * (w - 1);  // 0.5 ... 2.5 (r10) ... 7.1 (r20)
    public static float SpeedMult(int w) => Mathf.Min(0.7f + 0.045f * (w - 1), 1.9f);          // 0.7 ... 1.1 (r10) ... 1.55 (r20)
    public static float AttackMult(int w) => Mathf.Min(0.55f + 0.05f * (w - 1), 1.7f);         // golpes lentos y fáciles de esquivar al principio
    public static int MaxAlive(int w) => Mathf.Min(3 + w * 2, 50);
    public static float SpawnInterval(int w) => Mathf.Max(0.15f, 2.2f - 0.1f * w);

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsOver) return;
        PlayerController player = PlayerController.Instance;
        if (player == null || player.Dead) return;

        switch (CurrentPhase)
        {
            case Phase.Intro:
            case Phase.Break:
                BreakRemaining -= Time.deltaTime;
                if (BreakRemaining <= 0f) StartWave();
                break;

            case Phase.Fighting:
                UpdateSpawning(player);
                if (toSpawn == 0 && Enemy.All.Count == 0) CompleteWave();
                break;
        }
    }

    public void SkipBreak()
    {
        if (CurrentPhase != Phase.Fighting) BreakRemaining = Mathf.Min(BreakRemaining, 0.01f);
    }

    private void StartWave()
    {
        GameSession.Wave++;
        CurrentPhase = Phase.Fighting;
        spawnTimer = 1f;
        Sfx.StartHordeMusic();

        // Llegada al castillo: sólo el jefe.
        if (introBossPending)
        {
            introBossPending = false;
            BossOnlyRound = true;
            toSpawn = 0;
            GameHUD.Banner("¡" + bossDef.name.ToUpper() + "!", "El guardián del castillo te espera en el salón del trono");
            float hp = 0.55f + 0.05f * GameSession.Wave;
            SpawnBoss(hp, 1f, bossArena);
            return;
        }

        BossOnlyRound = false;
        LocalWave++;
        if (mapIndex == 1) GameSession.RuinsWave = LocalWave;
        else GameSession.CastleWave = LocalWave;
        int w = LocalWave;
        toSpawn = EnemyCount(w);

        bool bossWave = w % bossEvery == 0;
        string sub = w == 1 ? "¡Fácil! Calienta motores" : bossWave ? "¡Se acerca un JEFE!" : w >= 15 ? "¡LOCURA TOTAL!" : w >= 8 ? "Esto se pone serio..." : "Sobrevive a la horda";
        GameHUD.Banner("RONDA " + GameSession.Wave, sub);
        if (bossWave) SpawnBoss(0.6f + 0.1f * w + 0.004f * w * w, Mathf.Min(SpeedMult(w) + 0.2f, 1.6f), null);
    }

    private void CompleteWave()
    {
        CurrentPhase = Phase.Break;
        BreakRemaining = BreakTime;
        Boss = null;
        Sfx.StopHordeMusic();

        if (BossOnlyRound)
        {
            int reward = 40;
            GameSession.AddCoins(reward);
            BossOnlyRound = false;
            GameSession.CastleBossDefeated = true;
            GameHUD.Banner("¡GUARDIÁN DERROTADO!", "+" + reward + " monedas · Ahora el castillo despierta... prepárate   ·   [B] Tienda");
            return;
        }

        int bonus = 3 + LocalWave * 2;
        GameSession.AddCoins(bonus);
        GameHUD.Banner("¡RONDA " + GameSession.Wave + " SUPERADA!", "+" + bonus + " monedas de recompensa   ·   [B] Tienda   ·   [Enter] Siguiente ronda");
    }

    private void UpdateSpawning(PlayerController player)
    {
        if (toSpawn <= 0) return;

        int w = LocalWave;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f || Enemy.All.Count >= MaxAlive(w)) return;

        spawnTimer = SpawnInterval(w);
        int burst = Mathf.Min(toSpawn, w < 4 ? 1 : Random.Range(1, 2 + w / 4));
        for (int i = 0; i < burst; i++)
        {
            SpawnEnemy(PickType(), player);
            toSpawn--;
        }
    }

    private EnemyDef PickType()
    {
        float total = 0f;
        var weights = new float[pool.Length];
        for (int i = 0; i < pool.Length; i++)
        {
            EnemyDef d = pool[i];
            if (LocalWave < d.unlockWave) continue;
            // Los tipos difíciles ganan peso con cada ronda que pasa desde que se desbloquean.
            float growth = 1f + (LocalWave - d.unlockWave) * 0.15f * (d.unlockWave - 1) / 3f;
            weights[i] = d.weight * growth;
            total += weights[i];
        }

        float r = Random.value * total;
        for (int i = 0; i < pool.Length; i++)
        {
            r -= weights[i];
            if (r <= 0f && weights[i] > 0f) return pool[i];
        }
        return pool[0];
    }

    private void SpawnEnemy(EnemyDef def, PlayerController player)
    {
        int w = LocalWave;
        // Al principio aparecen cerca para no tener que buscarlos; luego desde más lejos y de todas partes.
        float maxDist = Mathf.Min(8f + w, 16f);
        Enemy.Spawn(def, FindSpawnPoint(player, 5.5f, maxDist), HpMult(w), SpeedMult(w), AttackMult(w));
    }

    private void SpawnBoss(float hpMult, float speedMult, Vector2? at)
    {
        PlayerController player = PlayerController.Instance;
        NavPoint point = at.HasValue && FlowField.Instance != null ? new NavPoint(at.Value, FlowField.Instance.DefaultLayer) : FindSpawnPoint(player, 7f, 12f);
        Boss = Enemy.Spawn(bossDef, point, hpMult, speedMult, 1f);
        FX.Shake(0.3f, 0.6f);
    }

    // Punto de aparición a la distancia pedida y, si se puede, desde el que el enemigo llegue andando hasta el jugador.
    private NavPoint FindSpawnPoint(PlayerController player, float minDist, float maxDist)
    {
        Vector2 playerPos = player.transform.position;
        if (spawnPoints == null || spawnPoints.Count == 0)
            return new NavPoint(playerPos + Random.insideUnitCircle.normalized * minDist, player.gameObject.layer);

        FlowField field = FlowField.Instance;
        bool Reachable(NavPoint p) => field == null || field.IsReachable(p);

        for (int attempt = 0; attempt < 40; attempt++)
        {
            NavPoint p = spawnPoints[Random.Range(0, spawnPoints.Count)];
            float d = Vector2.Distance(p.position, playerPos);
            if (d >= minDist && d <= maxDist && Reachable(p)) return p;
        }
        for (int attempt = 0; attempt < 40; attempt++)
        {
            NavPoint p = spawnPoints[Random.Range(0, spawnPoints.Count)];
            if (Vector2.Distance(p.position, playerPos) >= 4f && Reachable(p)) return p;
        }
        return spawnPoints[Random.Range(0, spawnPoints.Count)];
    }
}
