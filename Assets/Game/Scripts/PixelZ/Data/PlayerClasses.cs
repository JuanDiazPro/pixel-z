using UnityEngine;

public enum AbilityType { Whirlwind, ArrowRain, Shield, Fury }

// Datos de una clase de soldado jugable.
public class PlayerClassDef
{
    public string name;
    public string description;
    public Color tint = Color.white;
    public float speed;
    public int damage;
    public float attackCooldown;
    public float attackRadius;
    public float knockback;
    public bool ranged;
    public AbilityType ability;
    public string abilityName;
    public string abilityDescription;
    public float abilityCooldown;

    // Valores 1..5 para las barras del menú de selección.
    public int statSpeed, statDamage, statRate, statRange;
}

// Todas las clases usan el sprite Soldier de Tiny RPG 01: cambian estadísticas, color, ataque y habilidad.
public static class PlayerClasses
{
    public static readonly PlayerClassDef[] All =
    {
        new PlayerClassDef
        {
            name = "Soldado",
            description = "Equilibrado. Espada con combo de dos golpes.",
            tint = Color.white,
            speed = 3.2f, damage = 2, attackCooldown = 0.42f, attackRadius = 0.8f, knockback = 3.5f,
            ability = AbilityType.Whirlwind, abilityName = "Torbellino",
            abilityDescription = "Gira la espada y golpea a todos a tu alrededor (x2 daño).",
            abilityCooldown = 6f,
            statSpeed = 3, statDamage = 3, statRate = 3, statRange = 2,
        },
        new PlayerClassDef
        {
            name = "Arquero",
            description = "Ataca a distancia con flechas que atraviesan enemigos.",
            tint = new Color(0.72f, 1f, 0.68f),
            speed = 3.4f, damage = 2, attackCooldown = 0.5f, attackRadius = 0f, knockback = 1.5f, ranged = true,
            ability = AbilityType.ArrowRain, abilityName = "Lluvia de flechas",
            abilityDescription = "Dispara 16 flechas en todas direcciones.",
            abilityCooldown = 7f,
            statSpeed = 4, statDamage = 2, statRate = 3, statRange = 5,
        },
        new PlayerClassDef
        {
            name = "Guardián",
            description = "Lento pero golpea muy fuerte y empuja lejos.",
            tint = new Color(0.62f, 0.75f, 1f),
            speed = 2.75f, damage = 4, attackCooldown = 0.62f, attackRadius = 1f, knockback = 6f,
            ability = AbilityType.Shield, abilityName = "Escudo divino",
            abilityDescription = "3 s invulnerable y una onda que empuja y daña.",
            abilityCooldown = 11f,
            statSpeed = 2, statDamage = 5, statRate = 2, statRange = 3,
        },
        new PlayerClassDef
        {
            name = "Berserker",
            description = "Muy rápido, golpes veloces. Ideal para jugadores agresivos.",
            tint = new Color(1f, 0.6f, 0.55f),
            speed = 3.7f, damage = 2, attackCooldown = 0.28f, attackRadius = 0.72f, knockback = 2.5f,
            ability = AbilityType.Fury, abilityName = "Furia",
            abilityDescription = "5 s: ataques el doble de rápidos y +25% velocidad.",
            abilityCooldown = 12f,
            statSpeed = 5, statDamage = 2, statRate = 5, statRange = 2,
        },
    };
}
