using UnityEngine;

// Sprites de un personaje de Tiny RPG ya cortados en frames de 100x100.
[System.Serializable]
public class CharacterArt
{
    public Sprite[] idle;
    public Sprite[] walk;
    public Sprite[] attack1;
    public Sprite[] attack2;
    public Sprite[] attack3;
    public Sprite[] hurt;
    public Sprite[] death;
}

// Referencias a todos los sprites que el juego usa en tiempo de ejecución.
// Se genera con  Pixel Z > Configurar juego  y vive en Resources para cargarse sin escenas.
public class PixelZDatabase : ScriptableObject
{
    [Header("Personajes")]
    public CharacterArt soldier;
    public CharacterArt orc;
    public CharacterArt demon;
    public CharacterArt bloodMonster;

    [Header("Iconos")]
    public Sprite heart;
    public Sprite heartEmpty;
    public Sprite coin;
    public Sprite gem;
    public Sprite key;
    public Sprite potion;
    public Sprite chest;
    public Sprite star;
    public Sprite sword;
    public Sprite bow;
    public Sprite shield;
    public Sprite helmet;
    public Sprite mana;
    public Sprite gear;
    public Sprite apple;
    public Sprite map;

    [Header("Pantallas")]
    public Sprite menuBackground;
    public Sprite loadingBackground;
    public Sprite pauseBackground;

    [Header("FX")]
    public Sprite white;
    public Sprite circle;
    public Sprite ring;
    public Sprite glow;
    public Sprite arrow;
    public Sprite keyPrompt;
    public Sprite chestOpen;
    public Sprite[] fireball;
    public Sprite[] explosion;
    public Material unlitMaterial;

    private static PixelZDatabase instance;

    public static PixelZDatabase I
    {
        get
        {
            if (instance == null) instance = Resources.Load<PixelZDatabase>("PixelZDatabase");
            if (instance == null) Debug.LogError("[Pixel Z] Falta Resources/PixelZDatabase. Ejecuta el menú  Pixel Z > Configurar juego.");
            return instance;
        }
    }

    public CharacterArt Art(EnemyArt art)
    {
        switch (art)
        {
            case EnemyArt.Demon: return demon;
            case EnemyArt.BloodMonster: return bloodMonster;
            default: return orc;
        }
    }
}
