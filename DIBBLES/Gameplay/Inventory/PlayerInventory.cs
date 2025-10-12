using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Gameplay.Inventory;

using static InventorySystem;

public class PlayerInventory : InventoryBase
{
    private RectangleF inventoryRect = new RectangleF(0f, 0f, 550f, 650f);
    private bool isOpen = false;

    // Main item slots
    private ItemSlot[,] itemSlots = new ItemSlot[9, 3];
    private RectangleF[,] itemSlotRects = new RectangleF[9, 3];
    
    // Hotbar item slots
    private ItemSlot[] hotBarSlots = new ItemSlot[9];
    private RectangleF[] hotbarItemSlotRects = new RectangleF[9];
    
    public override void Start()
    {
        inventoryRect.X = (UI.CenterPivot.X - inventoryRect.Width * 0.5f);
        inventoryRect.Y = (UI.CenterPivot.Y - inventoryRect.Height * 0.5f) - 50f;

        // Set main slot positions
        for (var x = 0; x < itemSlots.GetLength(0); x++)
        {
            for (var y = 0; y < itemSlots.GetLength(1); y++)
            {
                var invRectY = inventoryRect.Y + (inventoryRect.Height * 0.5f) + 32f;
                var pos = new Vector2(inventoryRect.X + (x * ItemSlotSize) * ItemSlotPadding,
                    invRectY + (y * ItemSlotSize) * ItemSlotPadding);
                
                itemSlotRects[x, y] = new RectangleF(pos, ItemSlotSize, ItemSlotSize);
            }
        }

        // Set hotbar slot positions
        for (var i = 0; i < hotBarSlots.Length; i++)
        {
            var hotRectY = inventoryRect.Y + (inventoryRect.Height - ItemSlotSize) - 32f;
            var pos = new Vector2(inventoryRect.X + (i * ItemSlotSize) * ItemSlotPadding, hotRectY);

            hotbarItemSlotRects[i] = new RectangleF(pos, ItemSlotSize, ItemSlotSize);
        }
    }

    public override void Update()
    {
        // Opening/Closing
        if (Input.IsKeyPressed(Keys.E))
        {
            if (isOpen)
                close();
            else
                open();
        }
        
        if (Input.IsKeyPressed(Keys.Escape))
            close();
    }

    public override void Draw()
    {
        if (isOpen)
        {
            UIBatch.DrawRect(inventoryRect, UI.MainColor);
            
            // Main slots
            for (var x = 0; x < itemSlotRects.GetLength(0); x++)
            {
                for (var y = 0; y < itemSlotRects.GetLength(1); y++)
                {
                    var rect = itemSlotRects[x, y];
                    var mousePos = Mouse.GetState().Position.ToVector2();
                    var currentColor = rect.Contains(mousePos) ? UI.AccentColor : UI.MainColor;
                    
                    UIBatch.DrawRect(itemSlotRects[x, y], currentColor);
                }
            }
            
            // Hotbar slots
            for (var i = 0; i < hotbarItemSlotRects.Length; i++)
            {
                var rect = hotbarItemSlotRects[i];
                var mousePos = Mouse.GetState().Position.ToVector2();
                var currentColor = rect.Contains(mousePos) ? UI.AccentColor : UI.MainColor;
                
                UIBatch.DrawRect(hotbarItemSlotRects[i], currentColor);
            }
        }
    }

    private void open()
    {
        if (StateMachine.Open(UIState.PlayerInventory))
            isOpen = true;
    }
    
    private void close()
    {
        StateMachine.Close(UIState.PlayerInventory);
        isOpen = false;
    }
}