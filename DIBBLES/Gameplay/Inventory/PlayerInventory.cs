using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Gameplay.Inventory;

using static InventorySystem;

// TODO: Can't set an item back where it was picked up from

public class PlayerInventory : InventoryBase
{
    private RectangleF inventoryRect = new RectangleF(0f, 0f, 550f, 650f);
    private bool isOpen = false;

    // Main item slots
    public ItemSlot[,] ItemSlots = new ItemSlot[9, 3];
    
    // Hotbar item slots
    public ItemSlot[] HotBarSlots = new ItemSlot[9];
    
    public override void Start()
    {
        inventoryRect.X = (UI.CenterPivot.X - inventoryRect.Width * 0.5f);
        inventoryRect.Y = (UI.CenterPivot.Y - inventoryRect.Height * 0.5f) - 50f;

        if (WorldSave.Exists)
        {
            ItemSlots = WorldSave.Data.PlayerItemSlots;
            HotBarSlots = WorldSave.Data.HotbarItemSlots;
        }
        
        // Set main slot positions
        for (var x = 0; x < ItemSlots.GetLength(0); x++)
        {
            for (var y = 0; y < ItemSlots.GetLength(1); y++)
            {
                if (ItemSlots[x, y] == null) // Only create if not loaded
                    ItemSlots[x, y] = new ItemSlot();
                
                var invRectY = inventoryRect.Y + (inventoryRect.Height * 0.5f) + 32f;
                
                var pos = new Vector2(inventoryRect.X + (x * ItemSlotSize) * ItemSlotPadding,
                    invRectY + (y * ItemSlotSize) * ItemSlotPadding);
                
                ItemSlots[x, y].Rect.X = pos.X;
                ItemSlots[x, y].Rect.Y = pos.Y;
            }
        }

        // Set hotbar slot positions
        for (var i = 0; i < HotBarSlots.Length; i++)
        {
            if (HotBarSlots[i] == null)
                HotBarSlots[i] = new ItemSlot();
            
            var hotRectY = inventoryRect.Y + (inventoryRect.Height - ItemSlotSize) - 32f;
            var pos = new Vector2(inventoryRect.X + (i * ItemSlotSize) * ItemSlotPadding, hotRectY);

            HotBarSlots[i].Rect.X = pos.X;
            HotBarSlots[i].Rect.Y = hotRectY;
        }
        
        Commands.RegisterCommand("give", "Give yourself a block: /give blocktype", giveCMD);
        Commands.RegisterCommand("clear", "Clears your entire inventory", clearCMD);
    }

    public override void Update()
    {
        WorldSave.Data.PlayerItemSlots = ItemSlots;
        WorldSave.Data.HotbarItemSlots = HotBarSlots;
        
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
        
        // Main slots
        for (var x = 0; x < ItemSlots.GetLength(0); x++)
        {
            for (var y = 0; y < ItemSlots.GetLength(1); y++)
            {
                var slot = ItemSlots[x, y];
                slot.Update();
            }
        }
        
        // Hotbar slots
        for (var i = 0; i < HotBarSlots.Length; i++)
        {
            var slot = HotBarSlots[i];
            slot.Update();
        }
    }

    public override void Draw()
    {
        if (isOpen)
        {
            UIBatch.DrawRectRounded(inventoryRect, 0.1f, 4, UI.MainColor);
            
            // Main slots
            for (var x = 0; x < ItemSlots.GetLength(0); x++)
            {
                for (var y = 0; y < ItemSlots.GetLength(1); y++)
                {
                    var slot = ItemSlots[x, y];
                    slot.Draw();
                }
            }
            
            // Hotbar slots
            for (var i = 0; i < HotBarSlots.Length; i++)
            {
                var slot = HotBarSlots[i];
                slot.Draw();
            }
        }
    }

    public void AddBlock(BlockType blockType, int stackAmount = 1)
    {
        var selectedSlot = HotBarSlots[GameScene.PlayerCharacter.hotbar.HotBarSelectionIndex];
        
        if (selectedSlot.Type == BlockType.Air && selectedSlot.StackAmount <= 0)
            selectedSlot.Set(blockType, stackAmount);
        else if (selectedSlot.Type == blockType)
            InrementStack(stackAmount);
    }
    
    public void InrementStack(int amount = 1)
    {
        HotBarSlots[GameScene.PlayerCharacter.hotbar.HotBarSelectionIndex].StackAmount += amount;
    }
    
    public void DecrementStack(int amount = 1)
    {
        HotBarSlots[GameScene.PlayerCharacter.hotbar.HotBarSelectionIndex].StackAmount -= amount;
    }
    
    // Commands
    private void giveCMD(string[] args)
    {
        if (args.Length < 1)
        {
            Chat.Write("Usage: /give blocktype amount", ChatMessageType.Error);
            return;
        }

        var blockName = args[0].ToLower();
            
        if (Enum.TryParse<BlockType>(blockName, true, out var blockType))
        {
            // Give block at selected slot in hotbar
            var selectionIndex = GameScene.PlayerCharacter.hotbar.HotBarSelectionIndex;

            if (args.Length == 1)
            {
                HotBarSlots[selectionIndex].Set(blockType, 1);
                Chat.Write($"Gave yourself '{blockType}'", ChatMessageType.Command);
            }
            else if (args.Length == 2)
            {
                if (int.TryParse(args[1], out var stackAmount))
                {
                    HotBarSlots[selectionIndex].Set(blockType, stackAmount);
                    Chat.Write($"Gave yourself {stackAmount} '{blockType}'", ChatMessageType.Command);
                }
                else
                    Chat.Write("Couldn't parse amount", ChatMessageType.Error);
            }
        }
        else
            Chat.Write($"Unknown block type: '{blockName}'", ChatMessageType.Error);
    }

    private void clearCMD(string[] args)
    {
        // Main inventory
        for (var x = 0; x < ItemSlots.GetLength(0); x++)
        for (var y = 0; y < ItemSlots.GetLength(1); y++)
            ItemSlots[x,y].Set(BlockType.Air, 0);
        
        // Hotbar
        for (var i = 0; i < HotBarSlots.Length; i++)
            HotBarSlots[i].Set(BlockType.Air, 0);
        
        Chat.Write($"Cleared inventory", ChatMessageType.Command);
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