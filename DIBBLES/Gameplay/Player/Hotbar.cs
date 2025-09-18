using System.Numerics;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay.Player;

public class ItemSlot
{
    public int StackAmount;
    public BlockType Type;

    public ItemSlot(int stackAmount, BlockType type)
    {
        StackAmount = stackAmount;
        Type = type;
    }
}

public class Hotbar
{
    public ItemSlot? SelectedItem;

    // Item slots
    private ItemSlot[] hotbarSlots = new ItemSlot[9];

    private Rectangle hotbarRect = new Rectangle(0f, 0f, 900f, 100f);
    private Rectangle hotbarSelectionRect;
    
    private int hotBarSelectionIndex;
    private float hotBarSelectionPosX;
    
    // Health
    private const float healthBarWidth = 400f;
    private Rectangle healthBarRect = new Rectangle(0f, 0f, healthBarWidth, 10f);

    // Icons
    private Dictionary<BlockType, Texture2D> blockIcons = new();
    
    public void Start()
    {
        if (WorldSave.Data.HotbarPosition != 0)
            hotBarSelectionIndex = WorldSave.Data.HotbarPosition;

        Resize();
        
        hotbarSlots[0] = new ItemSlot(WorldSave.Data.HotbarPosition, BlockType.Grass);
        
        Commands.RegisterCommand("give", "Give yourself a block: /give blocktype", args =>
        {
            if (args.Length != 1)
            {
                Chat.Write(ChatMessageType.Error, "Usage: /give blocktype");
                return;
            }

            var blockName = args[0].ToLower();
            
            if (Enum.TryParse<BlockType>(blockName, true, out var blockType))
            {
                // Give block logic (e.g., add to hotbar or inventory)
                hotbarSlots[hotBarSelectionIndex] = new ItemSlot(1, blockType);
                
                Chat.Write(ChatMessageType.Command, $"Gave yourself '{blockType}'");
            }
            else
            {
                Chat.Write(ChatMessageType.Error, $"Unknown block type: '{blockName}'");
            }
        });

        renderBlockIcons();
    }

    public void Update(bool isPlayerDead, bool isFrozen)
    {
        if (!isPlayerDead && !isFrozen)
        {
            var mouseWheelNormalized = MathF.Ceiling(-Input.ScrollDelta());

            if (mouseWheelNormalized > 0.0f || mouseWheelNormalized < 0.0f)
            {
                hotBarSelectionIndex += (int)mouseWheelNormalized;
                hotBarSelectionIndex = GMath.Repeat(hotBarSelectionIndex, 0, 8);
            }

            var numKeys = (KeyboardKey)Raylib.GetKeyPressed();

            switch (numKeys)
            {
                case KeyboardKey.One:
                    hotBarSelectionIndex = 0;
                    break;
                case KeyboardKey.Two:
                    hotBarSelectionIndex = 1;
                    break;
                case KeyboardKey.Three:
                    hotBarSelectionIndex = 2;
                    break;
                case KeyboardKey.Four:
                    hotBarSelectionIndex = 3;
                    break;
                case KeyboardKey.Five:
                    hotBarSelectionIndex = 4;
                    break;
                case KeyboardKey.Six:
                    hotBarSelectionIndex = 5;
                    break;
                case KeyboardKey.Seven:
                    hotBarSelectionIndex = 6;
                    break;
                case KeyboardKey.Eight:
                    hotBarSelectionIndex = 7;
                    break;
                case KeyboardKey.Nine:
                    hotBarSelectionIndex = 8;
                    break;
            }

            hotBarSelectionPosX = hotBarSelectionIndex * hotbarRect.Height;
            SelectedItem = hotbarSlots[hotBarSelectionIndex];

            hotbarSelectionRect.X = hotbarRect.X + hotBarSelectionPosX;
            hotbarSelectionRect.Y = hotbarRect.Y;
        }
        
        WorldSave.Data.HotbarPosition = hotBarSelectionIndex;
    }

    public void Draw(int health)
    {
        Raylib.DrawRectangleRec(hotbarRect, UI.MainColor);

        // Hotbar dividers
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            var xPos = hotbarRect.X + (i + 1.0f) * hotbarRect.Height;

            Raylib.DrawLineEx(new Vector2(xPos, hotbarRect.Y),
                new Vector2(xPos, hotbarRect.Y + hotbarRect.Height), 1.0f, UI.AccentColor);
        }

        Raylib.DrawRectangleRounded(hotbarSelectionRect, 0.5f, 2, UI.AccentColor);

        // Hotbar items
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] != null && hotbarSlots[i].StackAmount > 0)
            {
                var xPos = hotbarRect.X + i * hotbarRect.Height;
                var itemTexture = BlockData.Textures[hotbarSlots[i].Type];

                var itemOrigRect = new Rectangle(0.0f, 0.0f, itemTexture.Width, itemTexture.Height);
                var itemDestRect = new Rectangle(xPos + 0.1f * hotbarRect.Height,
                    hotbarRect.Y + 0.1f * hotbarRect.Height,
                    hotbarRect.Height * 0.8f, hotbarRect.Height * 0.8f);

                if (blockIcons.TryGetValue(hotbarSlots[i].Type, out var iconTex))
                {
                    Raylib.DrawTexturePro(iconTex, itemOrigRect, itemDestRect, Vector2.Zero, 0.0f, Color.White);
                }
                
                //Raylib.DrawTexturePro(itemTexture, itemOrigRect, itemDestRect, Vector2.Zero, 0.0f, Color.White);
            }
        }
        
        var healthPercent = ((float)health * 0.01f) * healthBarWidth;
        healthBarRect.Width = healthPercent;
        
        Raylib.DrawRectangleRec(new Rectangle(healthBarRect.X, healthBarRect.Y, healthBarWidth, healthBarRect.Height), new Color(0f,0f,0f,0.5f));
        Raylib.DrawRectangleRec(healthBarRect, Color.Red);
    }

    // Draw each block type as a cube, then render out to a texture
    private void renderBlockIcons()
    {
        int iconSize = 96; // icon pixel size
        float cubeScale = 0.9f; // scale the cube to fit nicely in the icon

        foreach (BlockType blockType in Enum.GetValues(typeof(BlockType)))
        {
            if (blockType == BlockType.Air || blockType == BlockType.Water) continue; // Skip air and water

            RenderTexture2D renderTexture = Raylib.LoadRenderTexture(iconSize, iconSize);

            // Set up the isometric orthographic camera
            var orthoCamera = new Camera3D();
            float isoYaw = MathF.PI / 4f; // 45 deg
            float isoPitch = MathF.Atan(1f / MathF.Sqrt(2f)); // ≈ 35.264 deg

            float radius = 2.5f;
            float x = radius * MathF.Cos(isoPitch) * MathF.Sin(isoYaw);
            float y = radius * MathF.Sin(isoPitch);
            float z = radius * MathF.Cos(isoPitch) * MathF.Cos(isoYaw);
            
            orthoCamera.Position = new Vector3(x, y, z);
            orthoCamera.Target = Vector3.Zero;
            orthoCamera.Up = Vector3.UnitY;
            orthoCamera.FovY = 2.0f;
            orthoCamera.Projection = CameraProjection.Orthographic;

            // Create the cube model with correct texture
            Model cubeModel = MeshUtils.GenTexturedCube(BlockData.Textures[blockType]);

            Raylib.BeginTextureMode(renderTexture);
            Raylib.ClearBackground(new Color(0,0,0,0)); // Transparent background
            Raylib.BeginMode3D(orthoCamera);

            Raylib.DrawCube(Vector3.Zero, 1f, 1f, 1f, Color.Red);
            //Raylib.DrawModel(cubeModel, Vector3.Zero, cubeScale, Color.White);

            Raylib.EndMode3D();
            Raylib.EndTextureMode();

            // Free the model after use
            Raylib.UnloadModel(cubeModel);

            // Store the icon texture (renderTexture.Texture) in your dictionary
            blockIcons[blockType] = renderTexture.Texture;
        }
    }
    
    public void Resize()
    {
        var hotbarPos = UI.BottomCenterPivot;
        hotbarPos.X -= hotbarRect.Width / 2f;
        hotbarPos.Y -= 110f;

        hotbarRect.X = hotbarPos.X;
        hotbarRect.Y = hotbarPos.Y;

        hotbarSelectionRect = new Rectangle(hotbarRect.X, hotbarRect.Y, hotbarRect.Height, hotbarRect.Height);

        var healthBarPos = hotbarPos;
        healthBarPos.Y -= 20f;
        
        healthBarRect = new Rectangle(healthBarPos.X, healthBarPos.Y, healthBarRect.Width, healthBarRect.Height);
    }
}