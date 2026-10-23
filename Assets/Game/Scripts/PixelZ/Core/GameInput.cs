using UnityEngine;
using UnityEngine.InputSystem;

// Entrada común de teclado y mando. En 2 jugadores el J1 usa teclado y ratón y el J2 el mando;
// jugando solo, el J1 puede usar cualquiera de los dos.
// Mando: stick izq. mover · stick der. apuntar · X / RT atacar · A / LT dash · B / RB habilidad · Y interactuar
//        Start pausa · Select tienda · LB empezar la ronda ya.
public static class GameInput
{
    public static Gamepad Pad => Gamepad.all.Count > 0 ? Gamepad.all[0] : null;

    public static bool UsesKeyboard(int player) => player == 0;
    public static bool UsesPad(int player) => player == 1 || !GameSession.TwoPlayers;

    public static bool InteractPressed(int player)
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Pad;
        return (UsesKeyboard(player) && kb != null && kb.eKey.wasPressedThisFrame)
            || (UsesPad(player) && pad != null && pad.buttonNorth.wasPressedThisFrame);
    }

    public static bool AnyInteractPressed => InteractPressed(0) || InteractPressed(1);

    public static bool PausePressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Pad;
            return (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) || (pad != null && pad.startButton.wasPressedThisFrame);
        }
    }

    public static bool ShopPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Pad;
            return (kb != null && (kb.bKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)) || (pad != null && pad.selectButton.wasPressedThisFrame);
        }
    }

    public static bool SkipPressed
    {
        get
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Pad;
            return (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) || (pad != null && pad.leftShoulder.wasPressedThisFrame);
        }
    }

    // Texto de la tecla de interactuar para los avisos sobre puertas y cofres.
    public static string InteractLabel => GameSession.TwoPlayers ? "[E / Y]" : Pad != null ? "[E / Y]" : "[E]";
}
