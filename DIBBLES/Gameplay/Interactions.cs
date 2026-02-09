using DIBBLES.Gameplay.Inventory;
using DIBBLES.Systems;

namespace DIBBLES.Gameplay;

// Master class for managing interactions
// Freezing player, preventing UI opening etc.
public class Interactions
{
    public static bool Frozen = false; // Freezes player, disables UI interactions, etc

    private static bool playerFrozen = false;
    public static bool PlayerFrozen
    {
        get
        {
            if (!Frozen)
                return playerFrozen;
            else
                return Frozen;
        }
        set
        {
            if (!Frozen)
                playerFrozen = value;
        }
    }

    private static bool uiFrozen = false;
    public static bool UIFrozen
    {
        get
        {
            if (!Frozen)
                return uiFrozen;
            else
                return Frozen;
        }
        set
        {
            if (!Frozen)
                uiFrozen = value;
        }
    }

    private static bool wasClosedAndFrozen = false;
    public static void CloseMenusAndFreeze()
    {
        if (!wasClosedAndFrozen)
        {
            CursorManager.ReleaseCursor();
            InventorySystem.StateMachine.CloseAll();
            Frozen = true;
            
            wasClosedAndFrozen = true;
        }
    }

    public static void Unfreeze()
    {
        if (!Frozen && !wasClosedAndFrozen)
            return;

        CursorManager.LockCursor();
        Frozen = false;
        wasClosedAndFrozen = false;
    }
}