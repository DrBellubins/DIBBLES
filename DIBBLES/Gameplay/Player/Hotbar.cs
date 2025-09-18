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

    private ItemSlot[] hotbarSlots = new ItemSlot[9];

    private Rectangle hotbarRect = new Rectangle(0f, 0f, 900f, 100f);
    private Rectangle hotbarSelectionRect;

    private const float healthBarWidth = 400f;
    private Rectangle healthBarRect = new Rectangle(0f, 0f, healthBarWidth, 10f);
    
    private int hotBarSelectionIndex;
    private float hotBarSelectionPosX;

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

                //Raylib.ImageDrawTriangle();
                drawBlockCube2D(itemTexture, new Vector2(itemDestRect.X, itemDestRect.Y), itemDestRect.Width);
                //Raylib.DrawTexturePro(itemTexture, itemOrigRect, itemDestRect, Vector2.Zero, 0.0f, Color.White);
            }
        }
        
        var healthPercent = ((float)health * 0.01f) * healthBarWidth;
        healthBarRect.Width = healthPercent;
        
        Raylib.DrawRectangleRec(new Rectangle(healthBarRect.X, healthBarRect.Y, healthBarWidth, healthBarRect.Height), new Color(0f,0f,0f,0.5f));
        Raylib.DrawRectangleRec(healthBarRect, Color.Red);
    }

    struct Vertex
    {
        public Vector2 Position;
        public Vector2 TexCoord;
        public Color Color;
    }
    
    private void drawBlockCube2D(Texture2D tex, Vector2 pos, float size)
    {
        // Parameters for cube illusion
        float s = size;
        float skew = s * 0.2f;
        float thickness = s * 0.2f;
    
        // Triangle vertices for the 3 visible faces
        // Top face (skewed above)
        Vector2 top0 = pos + new Vector2(skew, 0);
        Vector2 top1 = pos + new Vector2(s - skew, 0);
        Vector2 top2 = pos + new Vector2(s, thickness);
        Vector2 top3 = pos + new Vector2(0, thickness);
    
        // Side face (right face, skewed)
        Vector2 side0 = pos + new Vector2(s, thickness);
        Vector2 side1 = pos + new Vector2(s, s - thickness);
        Vector2 side2 = pos + new Vector2(s - skew, s);
        Vector2 side3 = pos + new Vector2(s - skew, 0);
    
        // Front face
        Vector2 front0 = pos + new Vector2(0, thickness);
        Vector2 front1 = pos + new Vector2(s, thickness);
        Vector2 front2 = pos + new Vector2(s - skew, s);
        Vector2 front3 = pos + new Vector2(skew, s);
    
        // 1. Create a temporary image to draw the cube faces
        int imgSize = (int)Math.Ceiling(size);
        Image img = Raylib.GenImageColor(imgSize, imgSize, new Color(0, 0, 0, 0)); // transparent
    
        // Helper to draw a textured quad (2 triangles)
        unsafe void DrawFace(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color tint)
        {
            Vector2 uv0 = new Vector2(0, 0);
            Vector2 uv1 = new Vector2(1, 0);
            Vector2 uv2 = new Vector2(1, 1);
            Vector2 uv3 = new Vector2(0, 1);

            fixed (Image* imgPtr = &img)
            {
                Raylib.ImageDrawTriangle(imgPtr,
                    p0 - pos, p1 - pos, p2 - pos,
                    uv0 * new Vector2(tex.Width, tex.Height),
                    uv1 * new Vector2(tex.Width, tex.Height),
                    uv2 * new Vector2(tex.Width, tex.Height),
                    tex, tint);

                Raylib.ImageDrawTriangle(imgPtr,
                    p0 - pos, p2 - pos, p3 - pos,
                    uv0 * new Vector2(tex.Width, tex.Height),
                    uv2 * new Vector2(tex.Width, tex.Height),
                    uv3 * new Vector2(tex.Width, tex.Height),
                    tex, tint);
            }
        }
    
        // Draw order: side (dark), top (light), front (full)
        DrawFace(side0, side1, side2, side3, Color.Gray);      // Side (dark)
        DrawFace(top0, top1, top2, top3, Color.LightGray);     // Top (light)
        DrawFace(front0, front1, front2, front3, Color.White); // Front
    
        // Convert image to texture and draw it
        Texture2D cubeTex = Raylib.LoadTextureFromImage(img);
        Raylib.DrawTextureV(cubeTex, pos, Color.White);
    
        // Clean up
        Raylib.UnloadTexture(cubeTex);
        Raylib.UnloadImage(img);
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