using DIBBLES.Scenes;
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
        // Left click grab/place
        cursorPos = Mouse.GetState().Position.ToVector2();
        var rectContains = Rect.Contains(cursorPos);
        
        // Placing/Grabbing
        if (rectContains && Input.StartedBreaking && HeldItem != null && HeldItem != this)
            InvokeItemPlaced(this);
        else if (rectContains && Input.StartedBreaking && HeldItem == null && StackAmount > 0)
            InvokeItemGrabbed(this);
        
        // Right click place one
        /*cursorPos = Mouse.GetState().Position.ToVector2();
        var rectContains = Rect.Contains(cursorPos);

        if (rectContains && Input.StartedInteracting)
        {
            
        }*/
    }

    public void Draw()
    {
        // Main rect (Always draw)
        var currentColor = Rect.Contains(cursorPos) ? UI.AccentColor : UI.MainColor;
        UIBatch.DrawRect(Rect, currentColor);

        if (!IsItemHeld)
        {
            // Icons
            if (Type != BlockType.Air && StackAmount > 0)
            {
                if (GameScene.Inventory.BlockIcons.TryGetValue(Type, out var iconTex))
                {
                    var itemOrigRect = new RectangleF(0f, 0f, iconTex.Width, iconTex.Height);
                        
                    var flippedDestRect = new RectangleF(
                        Rect.X,
                        Rect.Y + Rect.Height, // move Y down by height
                        Rect.Width,
                        -Rect.Height // negative height to flip
                    );
                        
                    UIBatch.DrawTexturePro(iconTex, itemOrigRect, flippedDestRect, Vector2.Zero, 0.0f, Color.White);
                }
            }

            // Stack amount
            if (Type != BlockType.Air && StackAmount > 0)
            {
                var padding = 8f;
                var text = $"{StackAmount}";
                var textSize = Engine.MainFont.MeasureString(text) * 0.93f; 
                var pos = new Vector2((Rect.X + ItemSlotSize) - textSize.X, (Rect.Y + ItemSlotSize) - textSize.Y);
            
                UIBatch.DrawString(text, pos, Color.White);
            }
        }
    }

    public void Set(BlockType type, int stackAmount)
    {
        Type = type;
        StackAmount = stackAmount;
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