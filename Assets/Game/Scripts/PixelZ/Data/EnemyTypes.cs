using UnityEngine;

public enum EnemyArt { Orc, Demon, BloodMonster }

// Datos base de un tipo de enemigo. La vida y velocidad se escalan con la ronda en WaveDirector.
public class EnemyDef
{
    public string name;
    public EnemyArt art;
    public Color tint = Color.white;
    public float scale = 1f;
    public float hp;
    public float speed;
    public float attackRange = 0.75f;
    public float attackCooldown = 1.3f;
    public float attackAnimFps = 10f;
    public int damage = 1;
    public int coinMin = 1, coinMax = 1;
    public float gemChance = 0.08f;
    public int unlockWave = 1;      // ronda (dentro del mapa) en la que empieza a aparecer
    public float weight = 1f;       // probabilidad relativa de aparición
    public bool boss;
    public bool fireRing;           // ataque especial: anillo de bolas de fuego
    public bool ranged;
    public float projectileSpeed = 6f;
    public float preferredDistance = 4.5f;
}

public static class EnemyTypes
{
    // Mapa 1 – Ruinas: Orcos de Tiny RPG 01 (de más fácil a más difícil).
    public static readonly EnemyDef[] Ruins =
    {
        new EnemyDef { name = "Orco recluta", art = EnemyArt.Orc, hp = 4, speed = 1.3f, coinMin = 1, coinMax = 2, gemChance = 0.05f, unlockWave = 1, weight = 10 },
        new EnemyDef { name = "Orco explorador", art = EnemyArt.Orc, tint = new Color(1f, 1f, 0.55f), scale = 0.85f, hp = 3, speed = 2.3f,
                       attackCooldown = 1f, coinMin = 1, coinMax = 2, gemChance = 0.06f, unlockWave = 3, weight = 6 },
        new EnemyDef { name = "Orco guerrero", art = EnemyArt.Orc, tint = new Color(1f, 0.55f, 0.5f), scale = 1.1f, hp = 9, speed = 1.5f,
                       coinMin = 2, coinMax = 3, gemChance = 0.1f, unlockWave = 5, weight = 5 },
        new EnemyDef { name = "Orco lanzador", art = EnemyArt.Orc, tint = new Color(0.75f, 0.6f, 1f), hp = 6, speed = 1.4f, ranged = true,
                       attackCooldown = 2.2f, projectileSpeed = 6f, coinMin = 2, coinMax = 3, gemChance = 0.12f, unlockWave = 7, weight = 3 },
        new EnemyDef { name = "Orco bruto", art = EnemyArt.Orc, tint = new Color(0.5f, 0.62f, 0.5f), scale = 1.4f, hp = 22, speed = 1.05f,
                       attackRange = 0.95f, attackCooldown = 1.6f, attackAnimFps = 8f, coinMin = 3, coinMax = 5, gemChance = 0.25f, unlockWave = 9, weight = 3 },
    };

    public static readonly EnemyDef RuinsBoss = new EnemyDef
    {
        name = "Jefe Orco", art = EnemyArt.Orc, tint = new Color(0.9f, 0.4f, 1f), scale = 2f, hp = 45, speed = 1.35f,
        attackRange = 1.25f, attackCooldown = 1.4f, attackAnimFps = 9f, coinMin = 12, coinMax = 18, gemChance = 1f, boss = true,
    };

    // Mapa 2 – Castillo: Demon_A y Blood Monster_A de Tiny RPG 02.
    public static readonly EnemyDef[] Castle =
    {
        new EnemyDef { name = "Monstruo de sangre", art = EnemyArt.BloodMonster, hp = 9, speed = 1.55f, coinMin = 2, coinMax = 3, gemChance = 0.08f, unlockWave = 1, weight = 10 },
        new EnemyDef { name = "Demonio", art = EnemyArt.Demon, hp = 11, speed = 1.85f, coinMin = 2, coinMax = 3, gemChance = 0.1f, unlockWave = 1, weight = 7 },
        new EnemyDef { name = "Sangre rabiosa", art = EnemyArt.BloodMonster, tint = new Color(1f, 0.6f, 0.35f), scale = 0.85f, hp = 7, speed = 2.8f,
                       attackCooldown = 0.9f, coinMin = 2, coinMax = 3, gemChance = 0.1f, unlockWave = 3, weight = 6 },
        new EnemyDef { name = "Demonio infernal", art = EnemyArt.Demon, tint = new Color(1f, 0.75f, 0.3f), scale = 1.1f, hp = 13, speed = 1.6f, ranged = true,
                       attackCooldown = 2f, projectileSpeed = 7f, coinMin = 3, coinMax = 4, gemChance = 0.15f, unlockWave = 4, weight = 4 },
        new EnemyDef { name = "Demonio sombrío", art = EnemyArt.Demon, tint = new Color(0.45f, 0.4f, 0.65f), scale = 1.2f, hp = 20, speed = 2.3f,
                       coinMin = 3, coinMax = 5, gemChance = 0.2f, unlockWave = 6, weight = 3 },
        new EnemyDef { name = "Coloso de sangre", art = EnemyArt.BloodMonster, tint = new Color(0.55f, 0.3f, 0.45f), scale = 1.5f, hp = 40, speed = 1.15f,
                       attackRange = 1f, attackCooldown = 1.6f, attackAnimFps = 9f, coinMin = 4, coinMax = 7, gemChance = 0.35f, unlockWave = 8, weight = 3 },
    };

    public static readonly EnemyDef CastleBoss = new EnemyDef
    {
        name = "Archidemonio", art = EnemyArt.Demon, tint = new Color(1f, 0.35f, 0.35f), scale = 2.1f, hp = 120, speed = 1.6f,
        attackRange = 1.35f, attackCooldown = 1.3f, attackAnimFps = 10f, coinMin = 20, coinMax = 30, gemChance = 1f, boss = true, fireRing = true,
    };
}
