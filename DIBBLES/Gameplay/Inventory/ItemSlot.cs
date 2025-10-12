using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Gameplay.Inventory;

using static InventorySystem;

public enum ItemSlotInteraction
{
    None,
    Hover,
    LeftClick,
    RightClick,
    LeftClickDrag,
    RightClickDrag,
    Drop
}

// Both ItemSlot logic, and UI logic/drawing
public class ItemSlot
{
    public int StackAmount;
    public BlockType Type;

    public RectangleF Rect = new RectangleF(0f, 0f, ItemSlotSize, ItemSlotSize);
    
    private Vector2 cursorPos = Vector2.Zero;

    public ItemSlot()
    {
        StackAmount = 0;
        Type = BlockType.Air;
        Rect = new RectangleF(0f, 0f, ItemSlotSize, ItemSlotSize);
    }
    
    public ItemSlot(int stackAmount, BlockType type)
    {
        StackAmount = stackAmount;
        Type = type;
    }

    public void Update()
    {
        // Right click place one
        cursorPos = Mouse.GetState().Position.ToVector2();
        var rectContains = Rect.Contains(cursorPos);

        if (rectContains && Input.StartedInteracting)
        {
            
        }
    }

    public void Draw()
    {
        var currentColor = Rect.Contains(cursorPos) ? UI.AccentColor : UI.MainColor;
        UIBatch.DrawRect(Rect, currentColor);
    }
    
    public bool IsInteracting(ItemSlotInteraction interaction)
    {
        switch (interaction)
        {
            case ItemSlotInteraction.Hover:
                return true;
            case ItemSlotInteraction.LeftClick:
                return true;
            case ItemSlotInteraction.RightClick:
                return true;
            case ItemSlotInteraction.LeftClickDrag:
                return true;
            case ItemSlotInteraction.RightClickDrag:
                return true;
            case ItemSlotInteraction.Drop:
                return true;
            default:
                return false;
        }
    }
}