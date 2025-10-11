using DIBBLES.Terrain;

namespace DIBBLES.Gameplay.Inventory;

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