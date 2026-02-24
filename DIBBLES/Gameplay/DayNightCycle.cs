using DIBBLES.Systems;
using DIBBLES.Terrain;

namespace DIBBLES.Gameplay;

// TODO: Dusk -> Night transition needs to happen quicker
public class DayNightCycle
{
    public float TimeOfDay = 8.0f; // 0..24
    public bool IsDay = true;

    // Constants
    public const float DayStart = 6.0f;
    public const float NightStart = 18.0f;
    public const float FullDayHours = 24f;
    public const float RealSecondsPerGameHour = 10f; // 1 min = 1 hour
    public const float RealSecondsPerFullDay = RealSecondsPerGameHour * FullDayHours;

    public bool NeedsRelight = false;
    
    private bool _lastIsDay = true;

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
            NeedsRelight = true;
            _lastIsDay = currentlyDay;
        }
    }

    private void timeCMD(string[] args)
    {
        if (args.Length < 1 || args[0] == string.Empty)
        {
            Chat.Write("No time set! Usage: /time [time]", ChatMessageType.Error);
        }
        else
        {
            float time = 0f;
            if (float.TryParse(args[0], out time))
            {
                // Change time and check for day/night transition
                TimeOfDay = time;

                // Determine current day/night after command
                bool currentlyDay = TimeOfDay >= DayStart && TimeOfDay < NightStart;
            
                if (currentlyDay != IsDay)
                {
                    IsDay = currentlyDay;
                    NeedsRelight = true;
                }
            
                // Keep state consistent for next Update
                _lastIsDay = IsDay;
            }
            else
            {
                Chat.Write("Couldn't parse time! Usage: /time [0.0 - 24.0]", ChatMessageType.Error);
            }
        }
    }
}