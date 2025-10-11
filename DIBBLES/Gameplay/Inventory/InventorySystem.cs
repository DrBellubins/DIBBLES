using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Gameplay.Inventory;

// Monolithic class for everything inventory logic-related.
// Independent sub-classes are for Hotbar, PlayerInventory, Chests, Furnaces etc.
public class InventorySystem
{
    public const float ItemSlotSize = 56f;
    public const float ItemSlotPadding = 1.1f; // Multiply by this each item slot
    
    public readonly Dictionary<BlockType, Texture2D> BlockIcons = new();

    public static InventoryStateMachine StateMachine = new();
    public static List<InventoryBase> Inventories = new();
    
    // initialize inventory classes here to add them to the Inventories list
    private PlayerInventory playerInventory = new();

    public void Start()
    {
        renderBlockIcons();

        foreach (var inventory in Inventories)
            inventory.Start();
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
    }
    
    // Draw each block type as a cube, then render out to a texture
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