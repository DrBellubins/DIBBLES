using DIBBLES.Gameplay.Inventory;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Gameplay.Player;

public class Hotbar
{
    public ItemSlot? SelectedItem;
    public int HotBarSelectionIndex;
    
    private const float selectionSizeMultiplier = 0.9f;
    
    // Item slots
    private RectangleF hotbarRect = new RectangleF(0f, 0f, 900f, 100f);
    private RectangleF hotbarSelectionRect;
    
    private float hotBarSelectionPosX;
    
    // Health
    private const float healthBarWidth = 400f;
    private RectangleF healthBarRect = new RectangleF(0f, 0f, healthBarWidth, 10);
    
    public void Start()
    {
        if (WorldSave.Data.HotbarPosition != 0)
            HotBarSelectionIndex = WorldSave.Data.HotbarPosition;

        Resize();
    }

    public void Update(bool isPlayerDead, bool isFrozen)
    {
        if (!isPlayerDead && !isFrozen &&
            !InventorySystem.StateMachine.IsAnyOtherInventoryOpen(UIState.Chat))
        {
            var hotbarSlots = GameScene.Inventory.PlayerInventory.HotBarSlots;
            
            var mouseWheelNormalized = MathF.Ceiling(-Input.ScrollDelta());

            if (mouseWheelNormalized > 0.0f || mouseWheelNormalized < 0.0f)
            {
                HotBarSelectionIndex += (int)mouseWheelNormalized;
                HotBarSelectionIndex = GMath.Repeat(HotBarSelectionIndex, 0, 8);
            }

            var numKeys = Input.GetKeyPressed();

            switch (numKeys)
            {
                case Keys.D1:
                    HotBarSelectionIndex = 0;
                    break;
                case Keys.D2:
                    HotBarSelectionIndex = 1;
                    break;
                case Keys.D3:
                    HotBarSelectionIndex = 2;
                    break;
                case Keys.D4:
                    HotBarSelectionIndex = 3;
                    break;
                case Keys.D5:
                    HotBarSelectionIndex = 4;
                    break;
                case Keys.D6:
                    HotBarSelectionIndex = 5;
                    break;
                case Keys.D7:
                    HotBarSelectionIndex = 6;
                    break;
                case Keys.D8:
                    HotBarSelectionIndex = 7;
                    break;
                case Keys.D9:
                    HotBarSelectionIndex = 8;
                    break;
            }

            // Selection rectangle centered in current slot
            float slotSize = hotbarRect.Height;
            float selectionSize = slotSize * selectionSizeMultiplier;

            hotBarSelectionPosX = HotBarSelectionIndex * slotSize;
        
            float selectionX = hotbarRect.X + hotBarSelectionPosX + (slotSize - selectionSize) / 2f;
            float selectionY = hotbarRect.Y + (slotSize - selectionSize) / 2f;
            
            SelectedItem = hotbarSlots[HotBarSelectionIndex];

            hotbarSelectionRect.X = selectionX;
            hotbarSelectionRect.Y = selectionY;
        }
        
        WorldSave.Data.HotbarPosition = HotBarSelectionIndex;
    }

    public void Draw(int health)
    {
        var hotbarSlots = GameScene.Inventory.PlayerInventory.HotBarSlots;

        var uiAlpha = InventorySystem.StateMachine.IsAnyInventoryOpen ? 0.25f : 1f;
        var mainAlpha = InventorySystem.StateMachine.IsAnyInventoryOpen ? 0.49f : 1f;

        var mainColor = new Color(UI.MainColor.R / 255f, UI.MainColor.G / 255f,
            UI.MainColor.B / 255f, (UI.MainColor.A / 255f) * mainAlpha);
        
        var accentColor = new Color(UI.AccentColor.R / 255f * uiAlpha, UI.AccentColor.G / 255f * uiAlpha,
            UI.AccentColor.B / 255f * uiAlpha, (UI.AccentColor.A / 255f) * (uiAlpha * 0.25f));
        
        var textColor = new Color(uiAlpha, uiAlpha, uiAlpha, uiAlpha);
        var healthColor = new Color(uiAlpha, 0f, 0f, uiAlpha);
        
        UIBatch.DrawRectRounded(hotbarRect, 0.5f, 4, mainColor);
        
        // Hotbar dividers
        for (int i = 0; i < hotbarSlots.Length - 1; i++)
        {
            var xPos = hotbarRect.X + (i + 1.0f) * hotbarRect.Height;
        
            UIBatch.DrawLine(new Vector2(xPos, hotbarRect.Y),
                new Vector2(xPos, hotbarRect.Y + hotbarRect.Height), 1.0f, accentColor);
        }
        
        // Selection
        UIBatch.DrawRectRounded(hotbarSelectionRect, 0.5f, 4, accentColor);
        
        // Hotbar items
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] != null && hotbarSlots[i].StackAmount > 0)
            {
                var slot = hotbarSlots[i];
                var xPos = hotbarRect.X + i * hotbarRect.Height;
                
                var itemDestRect = new RectangleF((xPos + 0.1f * hotbarRect.Height),
                    (hotbarRect.Y + 0.1f * hotbarRect.Height),
                    (hotbarRect.Height * 0.8f), (hotbarRect.Height * 0.8f));
        
                // Icon
                if (GameScene.Inventory.BlockIcons.TryGetValue(hotbarSlots[i].Type, out var iconTex))
                {
                    var itemOrigRect = new RectangleF(0f, 0f, iconTex.Width, iconTex.Height);
                    
                    var flippedDestRect = new RectangleF(
                        itemDestRect.X,
                        itemDestRect.Y + itemDestRect.Height, // move Y down by height
                        itemDestRect.Width,
                        -itemDestRect.Height // negative height to flip
                    );
                    
                    UIBatch.DrawTexturePro(iconTex, itemOrigRect, flippedDestRect, Vector2.Zero, 0.0f, textColor);
                }
                
                // Stack amount
                if (slot.Type != BlockType.Air && slot.StackAmount > 0)
                {
                    var padding = 8f;
                    var text = $"{slot.StackAmount}";
                    var textSize = Engine.MainFont.MeasureString(text) * 0.93f;
                    
                    var pos = new Vector2((itemDestRect.X + itemDestRect.Width) - textSize.X,
                        (itemDestRect.Y + itemDestRect.Height) - textSize.Y);
        
                    UIBatch.DrawString(text, pos, textColor);
                }
            }
        }
        
        // Health bar
        var healthPercent = (health * 0.01f) * healthBarWidth;
        healthBarRect.Width = (int)healthPercent;

        var healthBGRect = new RectangleF(healthBarRect.X, healthBarRect.Y, healthBarWidth, healthBarRect.Height);
        
        UIBatch.DrawRectRounded(healthBGRect, 0.65f, 2, mainColor);
        UIBatch.DrawRectRounded(healthBarRect, 0.65f, 2, healthColor);
    }
    
    public void Resize()
    {
        var hotbarPos = UI.BottomCenterPivot;
        hotbarPos.X -= hotbarRect.Width / 2f;
        hotbarPos.Y -= 110f;

        hotbarRect.X = (int)hotbarPos.X;
        hotbarRect.Y = (int)hotbarPos.Y;

        hotbarSelectionRect = new RectangleF(0f, 0f,
            hotbarRect.Height * selectionSizeMultiplier, hotbarRect.Height * selectionSizeMultiplier);
        
        var healthBarPos = hotbarPos;
        healthBarPos.Y -= 20f;
        
        healthBarRect = new RectangleF((int)healthBarPos.X, (int)healthBarPos.Y, healthBarRect.Width, healthBarRect.Height);
    }
}