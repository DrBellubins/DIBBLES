using DIBBLES.Utils;

namespace DIBBLES.Systems;

public class Time
{
    public static double time;
    
    private static double deltaTime;
    public static double DeltaTime
    {
        get
        {
            // Clamp deltaTime to prevent teleportation when lagging
            return !Engine.IsPaused ? GMath.Clamp(deltaTime, 0d, 0.1d) : 0d;
        }
        set{ deltaTime = value; }
    }
}