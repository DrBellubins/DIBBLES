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
    
    private ItemSlot[,] itemSlots = new ItemSlot[9, 3];
    private Vector2[,] itemSlotPositions = new Vector2[9, 3];
    
    public override void Start()
    {
        inventoryRect.X = (UI.CenterPivot.X - inventoryRect.Width * 0.5f);
        inventoryRect.Y = (UI.CenterPivot.Y - inventoryRect.Height * 0.5f) - 50f;

        // Set item slot positions
        for (var x = 0; x < itemSlots.GetLength(0); x++)
        {
            for (var y = 0; y < itemSlots.GetLength(1); y++)
            {
                var invRectY = inventoryRect.Y + (inventoryRect.Height * 0.5f) + 120f;
                var pos = new Vector2(inventoryRect.X + (x * ItemSlotSize) * ItemSlotPadding,
                    invRectY + (y * ItemSlotSize) * ItemSlotPadding);
                
                itemSlotPositions[x, y] = pos;
            }
        }
    }

    public override void Update()
    {
        if (Input.IsKeyPressed(Keys.E))
        {
            if (isOpen)
                close();
            else
                open();
        }
    }

    public override void Draw()
    {
        if (isOpen)
        {
            UIBatch.DrawRect(inventoryRect, UI.MainColor);
            
            for (var x = 0; x < itemSlots.GetLength(0); x++)
            {
                for (var y = 0; y < itemSlots.GetLength(1); y++)
                {
                    var pos = itemSlotPositions[x, y];
                    UIBatch.DrawRect(new RectangleF(pos, ItemSlotSize, ItemSlotSize), UI.MainColor);
                }
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