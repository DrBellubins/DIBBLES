//using System.Numerics;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

    private Rectangle hotbarRect = new Rectangle(0, 0, 900, 100);
    private Rectangle hotbarSelectionRect;
    
    private int hotBarSelectionIndex;
    private int hotBarSelectionPosX;
    
    // Health
    private const int healthBarWidth = 400;
    private Rectangle healthBarRect = new Rectangle(0, 0, healthBarWidth, 10);

    // Icons
    private Dictionary<BlockType, Texture2D> blockIcons = new();
    
    public void Start()
    {
        if (WorldSave.Data.HotbarPosition != 0)
            hotBarSelectionIndex = WorldSave.Data.HotbarPosition;

        Resize();
        
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
        /*if (!isPlayerDead && !isFrozen)
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
        
        WorldSave.Data.HotbarPosition = hotBarSelectionIndex;*/
    }

    public void Draw(int health)
    {
        Primatives.DrawRectangleRec(hotbarRect, UI.MainColor);
        
        // Hotbar dividers
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            var xPos = hotbarRect.X + (i + 1.0f) * hotbarRect.Height;

            //Raylib.DrawLineEx(new Vector2(xPos, hotbarRect.Y),
            //    new Vector2(xPos, hotbarRect.Y + hotbarRect.Height), 1.0f, UI.AccentColor);
        }

        Primatives.DrawRectangleRec(hotbarSelectionRect, UI.AccentColor);
        //Raylib.DrawRectangleRounded(hotbarSelectionRect, 0.5f, 2, UI.AccentColor);

        // Hotbar items
        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (hotbarSlots[i] != null && hotbarSlots[i].StackAmount > 0)
            {
                var xPos = hotbarRect.X + i * hotbarRect.Height;
                
                var itemDestRect = new Rectangle((int)(xPos + 0.1f * hotbarRect.Height),
                    (int)(hotbarRect.Y + 0.1f * hotbarRect.Height),
                    (int)(hotbarRect.Height * 0.8f), (int)(hotbarRect.Height * 0.8f));

                if (blockIcons.TryGetValue(hotbarSlots[i].Type, out var iconTex))
                {
                    var itemOrigRect = new Rectangle(0, 0, iconTex.Width, iconTex.Height);
                    
                    //Raylib.DrawTexturePro(iconTex, itemOrigRect, itemDestRect, Vector2.Zero, 0.0f, Color.White);
                }
            }
        }
        
        var healthPercent = ((float)health * 0.01f) * healthBarWidth;
        healthBarRect.Width = (int)healthPercent;
        
        Primatives.DrawRectangleRec(new Rectangle(healthBarRect.X, healthBarRect.Y, healthBarWidth, healthBarRect.Height), new Color(0f,0f,0f,0.5f));
        Primatives.DrawRectangleRec(healthBarRect, Color.Red);
    }

    // Draw each block type as a cube, then render out to a texture
    private void renderBlockIcons()
    {
        int iconSize = 96; // icon pixel size
        float cubeScale = 1.25f; // scale the cube to fit nicely in the icon

        foreach (BlockType blockType in Enum.GetValues(typeof(BlockType)))
        {
            if (blockType == BlockType.Air || blockType == BlockType.Water) continue; // Skip air and water

            RenderTarget2D renderTexture = new RenderTarget2D(MonoEngine.Graphics, iconSize, iconSize);

            // Set up the isometric orthographic camera
            var cam = new Camera3D();
            cam.Position = new Vector3(2, 2, 2);
            cam.Target = Vector3.Zero;
            cam.Up = Vector3.UnitY;
            cam.Fov = 2f;
            cam.SetOrthographic();

            // Create the cube model with correct texture
            RuntimeModel cubeModel = MeshUtilsMonoGame.GenTexturedCubeIcon(BlockData.Textures[blockType]);
            
            /*Raylib.BeginTextureMode(renderTexture);
            Raylib.ClearBackground(new Color(0,0,0,0)); // Transparent background
            Raylib.BeginMode3D(cam);

            Raylib.DrawModel(cubeModel, Vector3.Zero, cubeScale, Color.White);

            Raylib.EndMode3D();
            Raylib.EndTextureMode();

            // Free the model after use
            Raylib.UnloadModel(cubeModel);

            // Store the icon texture (renderTexture.Texture) in your dictionary
            blockIcons[blockType] = renderTexture.Texture;*/
        }
    }
    
    public void Resize()
    {
        var hotbarPos = UI.BottomCenterPivot;
        hotbarPos.X -= hotbarRect.Width / 2f;
        hotbarPos.Y -= 110f;

        hotbarRect.X = (int)hotbarPos.X;
        hotbarRect.Y = (int)hotbarPos.Y;

        hotbarSelectionRect = new Rectangle(hotbarRect.X, hotbarRect.Y, hotbarRect.Height, hotbarRect.Height);

        var healthBarPos = hotbarPos;
        healthBarPos.Y -= 20f;
        
        healthBarRect = new Rectangle((int)healthBarPos.X, (int)healthBarPos.Y, healthBarRect.Width, healthBarRect.Height);
    }
}