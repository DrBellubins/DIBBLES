using DIBBLES.Utils;

namespace DIBBLES.Systems;

public class Time
{
    public static float time;
    //public static float DeltaTime;

    private static float deltaTime;
    public static float DeltaTime
    {
        get
        {
            return GMath.Clamp(deltaTime, 0.001f, 0.1f); // Clamp deltaTime to prevent teleportation when lagging
        }
        set{ deltaTime = value; }
    }
}