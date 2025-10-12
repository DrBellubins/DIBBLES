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
    
    // Hotbar item slots
    private ItemSlot[] hotBarSlots = new ItemSlot[9];
    
    public override void Start()
    {
        inventoryRect.X = (UI.CenterPivot.X - inventoryRect.Width * 0.5f);
        inventoryRect.Y = (UI.CenterPivot.Y - inventoryRect.Height * 0.5f) - 50f;

        // Set main slot positions
        for (var x = 0; x < itemSlots.GetLength(0); x++)
        {
            for (var y = 0; y < itemSlots.GetLength(1); y++)
            {
                itemSlots[x, y] = new ItemSlot();
                
                var invRectY = inventoryRect.Y + (inventoryRect.Height * 0.5f) + 32f;
                var pos = new Vector2(inventoryRect.X + (x * ItemSlotSize) * ItemSlotPadding,
                    invRectY + (y * ItemSlotSize) * ItemSlotPadding);
                
                itemSlots[x, y].Rect.X = pos.X;
                itemSlots[x, y].Rect.Y = pos.Y;
            }
        }

        // Set hotbar slot positions
        for (var i = 0; i < hotBarSlots.Length; i++)
        {
            hotBarSlots[i] = new ItemSlot();
            
            var hotRectY = inventoryRect.Y + (inventoryRect.Height - ItemSlotSize) - 32f;
            var pos = new Vector2(inventoryRect.X + (i * ItemSlotSize) * ItemSlotPadding, hotRectY);

            hotBarSlots[i].Rect.X = pos.X;
            hotBarSlots[i].Rect.Y = hotRectY;
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
        
        
        // Updates
        
        // Main slots
        for (var x = 0; x < itemSlots.GetLength(0); x++)
        {
            for (var y = 0; y < itemSlots.GetLength(1); y++)
            {
                var slot = itemSlots[x, y];
                slot.Update();
            }
        }
        
        // Hotbar slots
        for (var i = 0; i < hotBarSlots.Length; i++)
        {
            var slot = hotBarSlots[i];
            slot.Update();
        }
    }

    public override void Draw()
    {
        if (isOpen)
        {
            UIBatch.DrawRect(inventoryRect, UI.MainColor);
            
            // Main slots
            for (var x = 0; x < itemSlots.GetLength(0); x++)
            {
                for (var y = 0; y < itemSlots.GetLength(1); y++)
                {
                    var slot = itemSlots[x, y];
                    slot.Draw();
                }
            }
            
            // Hotbar slots
            for (var i = 0; i < hotBarSlots.Length; i++)
            {
                var slot = hotBarSlots[i];
                slot.Draw();
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