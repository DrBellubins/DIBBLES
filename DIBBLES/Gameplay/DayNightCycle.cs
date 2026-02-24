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

    public static bool NeedsRelight = false;
    
    private static bool _lastIsDay = true;

    public void Start()
    {
        Commands.Register("time", "Set time of day", timeCMD);
    }
    
    public void Update()
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
        }
        
        NeedsRelight = true;
    }

    private void timeCMD(string[] args)
    {
        if (args.Length < 1 || args[0] == string.Empty)
            Chat.Write("No time set! Usage: /time [time]", ChatMessageType.Error);
        else
        {
            float time = 0f;

            if (float.TryParse(args[0], out time))
                TimeOfDay = time;
            else
            {
                Chat.Write("Couldn't parse time! Usage: /time [0.0 - 24.0]", ChatMessageType.Error);
            }
        }
    }
}