namespace DIBBLES.Terrain.Blocks;

public abstract class BlockLogic
{
    // Universal logic
    public virtual void Tick(Block block, Chunk chunk)
    {
        
    }

    // World-space rendering
    public virtual void Draw3D(Chunk chunk)
    {
        
    }
    
    // UI rendering
    public virtual void Draw2D(Chunk chunk)
    {
        
    }
}