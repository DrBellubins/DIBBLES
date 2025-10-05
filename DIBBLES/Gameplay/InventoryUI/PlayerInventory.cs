using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Input;

namespace DIBBLES.Gameplay.InventoryUI;

public class PlayerInventory : InventoryBase
{
    private RectangleF inventoryRect = new RectangleF(0f, 0f, 550f, 650f);
    private bool isOpen = false;
    
    public override void Start()
    {
        inventoryRect.X = (UI.CenterPivot.X - inventoryRect.Width / 2f);
        inventoryRect.Y = (UI.CenterPivot.Y - inventoryRect.Height / 2f) - 50f;
    }

    public override void Update()
    {
        if (Input.IsKeyPressed(Keys.E))
            isOpen = !isOpen;
    }

    public override void Draw()
    {
        if (isOpen)
            UIBatch.DrawRect(inventoryRect, UI.MainColor);
    }
}