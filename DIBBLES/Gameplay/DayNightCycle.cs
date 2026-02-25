using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

namespace DIBBLES.Gameplay;

// TODO: In between Dusk - Day transition is gray
public class DayNightCycle
{
    public float TimeOfDay = 8.0f; // 0..24
    public bool IsDay = true;

    public float SunIntensity = 1f;
    
    // Constants
    public const float DayStart = 6.0f;
    public const float NightStart = 18.0f;
    public const float FullDayHours = 24f;
    public const float RealSecondsPerGameHour = 30f; // 30 sec = 1 hour, 12 min = 24 hours
    public const float RealSecondsPerFullDay = RealSecondsPerGameHour * FullDayHours;

    public bool NeedsRelight = false;
    
    // Colors
    public static Color CurrentSkyColor = new();
    
    public static Color DaySkyColor = new Color(0.4f, 0.74f, 1.0f, 1f);
    public static Color DawnDuskPeakColor = new Color(0.98f, 0.6f, 0.41f, 1f);
    public static Color DawnDuskFadeColor = new Color(0.90f, 0.75f, 0.60f, 1f);
    public static Color NightSkyColor = new Color(0.08f, 0.10f, 0.18f, 1f);
    
    // TODO: Set ambient to transition of ambient day/dawn/dusk/night colors.
    public static Color AmbientLightColor = new();
    
    public static Color AmbientDayColor = DaySkyColor.HSV(1f, 0.45f, 0.3f);
    public static Color AmbientDawnDuskColor = DawnDuskPeakColor.HSV(1f, 0.35f, 0.2f);
    public static Color AmbientNightColor = NightSkyColor.HSV(1f, 0.35f, 1.0f);
    
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
        
        // Smoothly lerp sky color based on time of day
        float tod = GameScene.TimeCycle.TimeOfDay;
    
        // Normalize to [0,24)
        if (tod < 0f)
            tod += 24f;
        if (tod >= 24f)
            tod -= 24f;
    
        // Dawn/Dusk spans: 5-7 (dawn) and 17-19 (dusk)
        float dawnStart = 5f, dawnEnd = 7f;
        float duskStart = 17f, duskEnd = 19f;
    
        // Day: 7–17 (peaks at noon)
        float dayStart = dawnEnd, dayEnd = duskStart;
        float noon = 12f;
    
        Color color;
        
        // Dawn transition
        if (tod >= dawnStart && tod < dawnEnd) // 5–7
        {
            float t = (tod - dawnStart) / (dawnEnd - dawnStart); // 0→1

            // Night → DawnPeak (warm-up)
            if (t < 0.5f)
            {
                float t1 = t * 2f;
                color = Color.Lerp(NightSkyColor, DawnDuskPeakColor, t1);
            }
            // DawnPeak → DawnFade (warm to pale)
            else
            {
                float t2 = (t - 0.5f) * 2f;
                color = Color.Lerp(DawnDuskPeakColor, DawnDuskFadeColor, t2);
            }

            SunIntensity = MathHelper.SmoothStep(0f, 1f, t);
        }
        else if (tod >= dayStart && tod < dayEnd) // 7–17
        {
            float blendEdge = 1.5f; // shorter than before to reduce purple risk

            if (tod < dayStart + blendEdge) // DawnFade → Day
            {
                float t = (tod - dayStart) / blendEdge;
                color = Color.Lerp(DawnDuskFadeColor, DaySkyColor, t);
                SunIntensity = 1f;
            }
            else if (tod > dayEnd - blendEdge) // Day → DuskPeak
            {
                float t = (tod - (dayEnd - blendEdge)) / blendEdge;
                color = Color.Lerp(DaySkyColor, DawnDuskPeakColor, t); // symmetric
                SunIntensity = 1f;
            }
            else
            {
                color = DaySkyColor;
                SunIntensity = 1f;
            }
        }
        else if (tod >= duskStart && tod < duskEnd) // 17–19 symmetric to dawn
        {
            float t = (tod - duskStart) / (duskEnd - duskStart);

            if (t < 0.5f)
                color = Color.Lerp(DaySkyColor, DawnDuskPeakColor, t * 2f); // reverse path
            else
                color = Color.Lerp(DawnDuskPeakColor, NightSkyColor, (t - 0.5f) * 2f);

            SunIntensity = MathHelper.SmoothStep(1f, 0f, t);
        }
        else // night
        {
            color = NightSkyColor;
            SunIntensity = 0f;
        }
    
        CurrentSkyColor = color;
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