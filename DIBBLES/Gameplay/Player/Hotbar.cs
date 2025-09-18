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

                drawBlockCube2D(itemTexture, new Vector2(itemDestRect.X, itemDestRect.Y), 1f);
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
        // Parameters
        float face = size;
        float skew = size * 0.5f;
        float thickness = size * 0.25f;
    
        // Source rect: whole block texture
        Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
    
        // 1. Draw top face (skewed upwards)
        Vector2[] top = new Vector2[]
        {
            pos + new Vector2(0, -thickness),                         // TL
            pos + new Vector2(face, -thickness),                      // TR
            pos + new Vector2(face - skew, 0),                        // BR
            pos + new Vector2(skew, 0)                                // BL
        };
    
        // 2. Draw side face (skewed right)
        Vector2[] side = new Vector2[]
        {
            pos + new Vector2(face, -thickness),                      // TL
            pos + new Vector2(face, face - thickness),                // TR
            pos + new Vector2(face - skew, face),                     // BR
            pos + new Vector2(face - skew, 0)                         // BL
        };
    
        // 3. Draw front face (straight)
        Vector2[] front = new Vector2[]
        {
            pos + new Vector2(skew, 0),                               // TL
            pos + new Vector2(face - skew, 0),                        // TR
            pos + new Vector2(face - skew, face),                     // BR
            pos + new Vector2(skew, face)                             // BL
        };
    
        // Helper for drawing a quad with a texture
        void DrawQuad(Texture2D t, Vector2[] verts)
        {
            Vertex[] vertices = new Vertex[4];
            
            for (int i = 0; i < 4; i++)
            {
                vertices[i].Position = verts[i];
                vertices[i].TexCoord = new Vector2((i == 1 || i == 2) ? 1 : 0, (i >= 2) ? 1 : 0);
                vertices[i].Color = Color.White;
            }
            
            Rlgl.SetTexture(t.Id);
            Rlgl.Begin(DrawMode.Quads);
            
            foreach (var v in vertices)
            {
                Rlgl.Color4ub(v.Color.R, v.Color.G, v.Color.B, v.Color.A);
                Rlgl.TexCoord2f(v.TexCoord.X, v.TexCoord.Y);
                Rlgl.Vertex2f(v.Position.X, v.Position.Y);
            }
            
            Rlgl.End();
            Rlgl.SetTexture(0);
        }
    
        // Draw order: side, top, front (front last covers seams)
        DrawQuad(tex, side);
        DrawQuad(tex, top);
        DrawQuad(tex, front);
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