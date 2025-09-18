using DIBBLES;

namespace DIBBLES.Systems;

public abstract class Scene
{
    public Scene()
    {
        MonoEngine.Scenes.Add(this);
    }
    
    public virtual void Start()
    {
        
    }
    
    public virtual void Update()
    {
        
    }
    
    public virtual void Draw()
    {
        
    }
}