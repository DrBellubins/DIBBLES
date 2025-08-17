using System.Numerics;
using DIBBLES.Systems;
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

    private int hotBarSelectionIndex;
    private float hotBarSelectionPosX;

    public void Start()
    {
        if (WorldSave.Data.HotbarPosition != 0)
            hotBarSelectionIndex = WorldSave.Data.HotbarPosition;

        Resize();

        // Temporary
        hotbarSlots[0] = new ItemSlot(1, BlockType.Grass);
        hotbarSlots[1] = new ItemSlot(1, BlockType.Stone);
        hotbarSlots[2] = new ItemSlot(1, BlockType.Dirt);
        hotbarSlots[3] = new ItemSlot(1, BlockType.Sand);
        hotbarSlots[4] = new ItemSlot(1, BlockType.Wood);

        SelectedItem = hotbarSlots[0];
    }

    public void Update()
    {
        var mouseWheelNormalized = MathF.Ceiling(-Raylib.GetMouseWheelMove());

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
        
        WorldSave.Data.HotbarPosition = hotBarSelectionIndex;
    }

    public void Draw()
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
                var itemTexture = Block.Textures[hotbarSlots[i].Type];

                var itemOrigRect = new Rectangle(0.0f, 0.0f, itemTexture.Width, itemTexture.Height);
                var itemDestRect = new Rectangle(xPos + 0.1f * hotbarRect.Height,
                    hotbarRect.Y + 0.1f * hotbarRect.Height,
                    hotbarRect.Height * 0.8f, hotbarRect.Height * 0.8f);

                Raylib.DrawTexturePro(itemTexture, itemOrigRect, itemDestRect, Vector2.Zero, 0.0f, Color.White);
            }
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
    }
}