namespace DIBBLES.Gameplay.InventoryUI;

public abstract class InventoryBase
{
    public InventoryBase()
    {
        InventorySystem.Inventories.Add(this);
    }
    
    public virtual void Start() {}
    public virtual void Update() {}
    public virtual void Draw() {}
}