using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Gameplay.Inventory;

// Monolithic class for everything inventory logic-related.
// Independent sub-classes are for Hotbar, PlayerInventory, Chests, Furnaces etc.
public class InventorySystem
{
    public const float ItemSlotSize = 56f;
    public const float ItemSlotPadding = 1.1f; // Multiply by this each item slot
    
    public readonly Dictionary<BlockType, Texture2D> BlockIcons = new();
    
    // Events for item grab/place
    public static event Action<ItemSlot>? ItemGrabbed;
    public static event Action<ItemSlot>? ItemPlaced;
    
    public static ItemSlot? HeldItem;
    public static bool IsItemHeld => HeldItem != null;
    
    public static UIStateMachine StateMachine = new();
    public static List<InventoryBase> Inventories = new();
    
    // List of all previous interactions, for dragging.
    private List<ItemSlot> slotInteractionQueue = new List<ItemSlot>();
    
    // initialize inventory classes here to add them to the Inventories list
    public PlayerInventory PlayerInventory = new();

    public void Start()
    {
        renderBlockIcons();

        foreach (var inventory in Inventories)
            inventory.Start();
        
        ItemGrabbed += OnItemGrabbed;
        ItemPlaced += OnItemPlaced;
    }

    public void Update()
    {
        foreach (var inventory in Inventories)
            inventory.Update();
    }

    public void Draw()
    {
        foreach (var inventory in Inventories)
            inventory.Draw();

        // Draw held item
        if (IsItemHeld && HeldItem != null)
        {
            var cursorPos = Mouse.GetState().Position.ToVector2();
            cursorPos = new Vector2(cursorPos.X - (ItemSlotSize * 0.5f), cursorPos.Y - (ItemSlotSize * 0.5f));
            
            // Icons
            if (HeldItem.Type != BlockType.Air && HeldItem.StackAmount > 0)
            {
                if (GameScene.Inventory.BlockIcons.TryGetValue(HeldItem.Type, out var iconTex))
                {
                    var itemOrigRect = new RectangleF(0f, 0f, iconTex.Width, iconTex.Height);
                        
                    var flippedDestRect = new RectangleF(
                        cursorPos.X,
                        cursorPos.Y + HeldItem.Rect.Height, // move Y down by height
                        HeldItem.Rect.Width,
                        -HeldItem.Rect.Height // negative height to flip
                    );
                        
                    UIBatch.DrawTexturePro(iconTex, itemOrigRect, flippedDestRect, Vector2.Zero, 0.0f, Color.White);
                }
            }

            // Stack amount
            if (HeldItem.Type != BlockType.Air &&
                HeldItem.StackAmount > 0 && GameScene.PlayerCharacter.IsSurvival)
            {
                var padding = 8f;
                var text = $"{HeldItem.StackAmount}";
                var textSize = Engine.MainFont.MeasureString(text) * 0.93f; 
                var pos = new Vector2((cursorPos.X + ItemSlotSize) - textSize.X, (cursorPos.Y + ItemSlotSize) - textSize.Y);
            
                UIBatch.DrawString(text, pos, Color.White);
            }
        }
    }
    
    private void OnItemGrabbed(ItemSlot slot)
    {
        if (HeldItem == null && slot.StackAmount > 0)
            HeldItem = slot;
    }

    private void OnItemPlaced(ItemSlot targetSlot)
    {
        if (HeldItem == null)
            return;

        // Only place if the target is not the same slot as held
        if (HeldItem != targetSlot)
        {
            // TODO: In creative items can not be placed back to slot grabbed from
            // Here, move all stack to target, if target is empty or same type
            if (targetSlot.Type == BlockType.Air || targetSlot.Type == HeldItem.Type)
            {
                targetSlot.Set(HeldItem.Type, HeldItem.StackAmount + targetSlot.StackAmount);
                HeldItem.Set(BlockType.Air, 0);
                HeldItem = null;
            }
            else
            {
                // Swap item
                var tempType = targetSlot.Type;
                var tempAmount = targetSlot.StackAmount;
                targetSlot.Set(HeldItem.Type, HeldItem.StackAmount);
                HeldItem.Set(tempType, tempAmount);
            }
        }
        else
        {
            // Dropping onto same slot: just clear held state
            HeldItem = null;
        }
    }
    
    public static void InvokeItemGrabbed(ItemSlot slot)
    {
        ItemGrabbed?.Invoke(slot);
    }

    public static void InvokeItemPlaced(ItemSlot slot)
    {
        ItemPlaced?.Invoke(slot);
    }
    
    // Draw each block type as a cube, then render out to a texture
    // TODO: Render icon with correct UVs (including face specific textures)
    private void renderBlockIcons()
    {
        int iconSize = 128; // icon pixel size

        foreach (BlockType blockType in Enum.GetValues(typeof(BlockType)))
        {
            if (blockType == BlockType.Air || blockType == BlockType.Water) continue; // Skip air and water

            RenderTarget2D renderTexture = new RenderTarget2D(Engine.Graphics, iconSize, iconSize);

            // Set up the isometric orthographic camera
            var cam = new Camera3D();
            cam.Position = new GVec3(2, 2, 2);
            cam.Target = Vector3.Zero;
            cam.Up = Vector3.UnitY;
            cam.AspectRatio = (float)iconSize / iconSize; // which is 1.0f for a square
            cam.Fov = 1.7f;
            cam.SetOrthographic();

            // Create the cube model with correct texture
            RuntimeModel cubeModel = MeshUtils.GenTexturedCubeIcon(BlockData.Textures[(blockType, 0)]);
            
            var world = Matrix.CreateTranslation(Vector3.Zero);
            var shader = (BasicEffect)cubeModel.Shader;
            
            shader.World = world;
            shader.View = cam.View;
            shader.Projection = cam.Projection;
            
            shader.LightingEnabled = true;
            shader.AmbientLightColor = new Vector3(0.5f, 0.5f, 0.5f);
            shader.DirectionalLight0.Enabled = true;
            shader.DirectionalLight0.Direction = new Vector3(0.3f, 1f, 0.7f);
            shader.DirectionalLight0.DiffuseColor = new Vector3(0.5f, 0.5f, 0.5f);
            
            Engine.Graphics.SetRenderTarget(renderTexture);
            Engine.Graphics.Clear(new Color(0f, 0f, 0f, 0f));
            
            cubeModel.Draw(world, cam.View, cam.Projection);
            
            Engine.Graphics.SetRenderTarget(null);
            
            BlockIcons[blockType] = renderTexture;
        }
    }
}

public class InventoryEventArgs : EventArgs
{
    public string Message { get; }
    public int Value { get; }

    public InventoryEventArgs(string message, int value)
    {
        Message = message;
        Value = value;
    }
}