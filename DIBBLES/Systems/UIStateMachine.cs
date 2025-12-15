using System;

namespace DIBBLES.Systems;

/// <summary>
/// Centralized state machine for managing mutually-exclusive UI overlays and their gameplay effects.
/// </summary>
public enum UIState
{
    None,
    Chat,
    PlayerInventory,
    Furnace,
    CraftingTable,
    // Add more states as needed
}

public class UIStateMachine
{
    public UIState CurrentState { get; private set; } = UIState.None;

    // --- State checks for convenience ---
    public bool CanOpenAnyUI = true;
    public bool IsChatOpen => CurrentState == UIState.Chat;
    public bool IsInventoryOpen => CurrentState == UIState.PlayerInventory;
    public bool IsAnyInventoryOpen => CurrentState != UIState.None;
    
    /// <summary>
    /// Raised whenever the UI state changes.
    /// </summary>
    public event Action<UIState> OnUIStateChanged;

    // --- Core logic ---
    public bool IsAnyOtherInventoryOpen(UIState state)
    {
        return CurrentState != state && CurrentState != UIState.None;
    }
    
    /// <summary>
    /// Attempt to open the specified UI. Will close any conflicting ones.
    /// Returns true if opened, false if suppressed by exclusivity.
    /// </summary>
    public bool Open(UIState state)
    {
        // If already open, do nothing
        if (CurrentState == state || !CanOpenAnyUI)
            return false;

        // If trying to open Chat while PlayerInventory is open, suppress
        if (state == UIState.Chat && CurrentState == UIState.PlayerInventory)
            return false;

        // If trying to open PlayerInventory while Chat is open, suppress
        if (state == UIState.PlayerInventory && CurrentState == UIState.Chat)
            return false;

        // Add more exclusivity rules as needed

        // Unlock cursor
        CursorManager.ReleaseCursor();
        
        CurrentState = state;
        OnUIStateChanged?.Invoke(CurrentState);
        return true;
    }

    /// <summary>
    /// Closes the specified UI if it's currently open.
    /// If any other UI should be enabled after closing (e.g., None), set it here.
    /// </summary>
    public void Close(UIState state)
    {
        if (CurrentState == state)
        {
            CursorManager.LockCursor();
            
            CurrentState = UIState.None;
            OnUIStateChanged?.Invoke(CurrentState);
        }
    }

    /// <summary>
    /// Closes any open UI, setting state to None.
    /// </summary>
    public void CloseAll()
    {
        if (CurrentState != UIState.None)
        {
            CurrentState = UIState.None;
            OnUIStateChanged?.Invoke(CurrentState);
        }
    }
}