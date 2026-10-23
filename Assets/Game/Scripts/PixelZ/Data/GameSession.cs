using UnityEngine;

// Estado de la partida en curso. Es estático para sobrevivir al cambio de escena (Ruinas -> Castillo).
public static class GameSession
{
    public const int StartHearts = 3;
    public const int HeartsCap = 6;
    public const int ChestPrice = 150;
    public const int GemValue = 5;
    public const int ReturnPrice = 50;   // volver del castillo a las Ruinas

    public static int SelectedClass
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("pz_class", 0), 0, PlayerClasses.All.Length - 1);
        set { PlayerPrefs.SetInt("pz_class", value); PlayerPrefs.Save(); }
    }

    public static int SelectedClass2
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("pz_class2", 1), 0, PlayerClasses.All.Length - 1);
        set { PlayerPrefs.SetInt("pz_class2", value); PlayerPrefs.Save(); }
    }

    public static bool RunActive;
    public static int ClassIndex;
    public static bool TwoPlayers;      // J1 teclado y ratón, J2 mando
    public static int ClassIndex2;
    public static int Hearts2;          // corazones del J2 (el máximo y las mejoras son compartidos)
    public static int Wave;             // última ronda iniciada (global, continúa entre mapas)
    public static int Coins;            // monedas disponibles para gastar
    public static int CoinsCollected;   // valor total recogido en la partida (gemas incluidas)
    public static int Kills;
    public static float TimeAlive;
    public static int Hearts;
    public static int MaxHearts;
    public static bool HasKey;
    public static bool ChestOpened;
    public static int MapReached;

    // Progreso de cada mapa, para continuar donde se dejó al ir y volver entre Ruinas y Castillo.
    public static int RuinsWave;
    public static int CastleWave;
    public static bool CastleBossDefeated;

    // Mejoras de la tienda
    public static int DamageLevel;
    public static int SpeedLevel;
    public static int AttackSpeedLevel;
    public static int MagnetLevel;
    public static int CooldownLevel;
    public static int ExtraHeartLevel;

    public static PlayerClassDef Class => PlayerClasses.All[ClassIndex];
    public static PlayerClassDef Class2 => PlayerClasses.All[ClassIndex2];
    public static PlayerClassDef ClassOf(int player) => player == 0 ? Class : Class2;

    public static int GetHearts(int player) => player == 0 ? Hearts : Hearts2;

    public static void SetHearts(int player, int value)
    {
        value = Mathf.Clamp(value, 0, MaxHearts);
        if (player == 0) Hearts = value;
        else Hearts2 = value;
    }

    // Corazones del jugador vivo que peor va (para el "seguro de pena" del botín).
    public static int LowestHearts
    {
        get
        {
            int low = int.MaxValue;
            foreach (PlayerController p in PlayerController.All)
                if (!p.Dead) low = Mathf.Min(low, p.Hearts);
            return low == int.MaxValue ? Hearts : low;
        }
    }

    // class2 < 0: partida de un jugador.
    public static void NewRun(int classIndex, int class2 = -1)
    {
        RunActive = true;
        ClassIndex = Mathf.Clamp(classIndex, 0, PlayerClasses.All.Length - 1);
        TwoPlayers = class2 >= 0;
        ClassIndex2 = Mathf.Clamp(class2, 0, PlayerClasses.All.Length - 1);
        Wave = 0;
        Coins = 0;
        CoinsCollected = 0;
        Kills = 0;
        TimeAlive = 0f;
        MaxHearts = StartHearts;
        Hearts = StartHearts;
        Hearts2 = StartHearts;
        HasKey = false;
        ChestOpened = false;
        MapReached = 1;
        RuinsWave = 0;
        CastleWave = 0;
        CastleBossDefeated = false;
        DamageLevel = SpeedLevel = AttackSpeedLevel = MagnetLevel = CooldownLevel = ExtraHeartLevel = 0;
    }

    public static void AddCoins(int value)
    {
        Coins += value;
        CoinsCollected += value;
    }

    public static int BestWave => PlayerPrefs.GetInt("pz_best_wave", 0);
    public static int BestCoins => PlayerPrefs.GetInt("pz_best_coins", 0);

    // Guarda récords; devuelve true si se superó la mejor ronda.
    public static bool SaveRecords()
    {
        bool newRecord = Wave > BestWave;
        if (newRecord) PlayerPrefs.SetInt("pz_best_wave", Wave);
        if (CoinsCollected > BestCoins) PlayerPrefs.SetInt("pz_best_coins", CoinsCollected);
        PlayerPrefs.Save();
        return newRecord;
    }
}
