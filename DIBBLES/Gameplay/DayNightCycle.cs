using DIBBLES.Systems;
using DIBBLES.Terrain;

namespace DIBBLES.Gameplay;

public class DayNightCycle
{
    public static float TimeOfDay = 8.0f; // 0..24
    public static bool IsDay = true;

    // Constants
    public const float DayStart = 6.0f;
    public const float NightStart = 18.0f;
    public const float FullDayHours = 24f;
    public const float RealSecondsPerGameHour = 60f; // 1 min = 1 hour
    public const float RealSecondsPerFullDay = RealSecondsPerGameHour * FullDayHours;

    private static bool _lastIsDay = true;
    
    public static void Update()
    {
        // Advance time
        TimeOfDay += (Time.DeltaTime / RealSecondsPerGameHour);
        
        if (TimeOfDay >= FullDayHours)
            TimeOfDay -= FullDayHours;

        // Determine day/night
        bool currentlyDay = TimeOfDay >= DayStart && TimeOfDay < NightStart;

        // Detect transition
        if (currentlyDay != _lastIsDay)
        {
            IsDay = currentlyDay;
            SetGlobalSkyLight(IsDay ? (byte)15 : (byte)0);
            _lastIsDay = currentlyDay;
        }
    }
    
    // Sets all skylights and regenerates lighting for all loaded chunks
    private static void SetGlobalSkyLight(byte level)
    {
        foreach (var chunk in TerrainGeneration.ChunkBuffer.Values)
        {
            // Set all skylights in the chunk
            for (int x = 0; x < TerrainGeneration.ChunkSize; x++)
            for (int y = 0; y < TerrainGeneration.ChunkSize; y++)
            for (int z = 0; z < TerrainGeneration.ChunkSize; z++)
            {
                chunk.SetSkyLightAt(x, y, z, level);
            }

            // Re-run light generation for the new skylight state
            TerrainGeneration.Lighting.Generate(chunk);

            // Remesh so lighting is visible (do it on next tick if you want async)
            TerrainGeneration.Mesh.Generate(chunk);
        }
    }
}