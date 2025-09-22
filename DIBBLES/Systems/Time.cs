using DIBBLES.Utils;

namespace DIBBLES.Systems;

public class Time
{
    public static float time;
    
    private static float deltaTime;
    public static float DeltaTime
    {
        get
        {
            // Clamp deltaTime to prevent teleportation when lagging
            return !Engine.IsPaused ? GMath.Clamp(deltaTime, 0f, 0.1f) : 0f;
        }
        set{ deltaTime = value; }
    }
}