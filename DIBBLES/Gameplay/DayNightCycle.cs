using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

namespace DIBBLES.Gameplay;

// TODO: Dusk -> Night transition needs to happen quicker
public class DayNightCycle
{
    public float TimeOfDay = 8.0f; // 0..24
    public bool IsDay = true;

    public float SunIntensity = 1f;
    
    // Constants
    public const float DayStart = 6.0f;
    public const float NightStart = 18.0f;
    public const float FullDayHours = 24f;
    public const float RealSecondsPerGameHour = 1f; // 1 min = 1 hour
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
        
        // Smoothly lerp sky color based on time of day
        float tod = GameScene.DayNightCycle.TimeOfDay;
    
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
    
        // Night: 19–5 (wraps over midnight)
        float nightStart = duskEnd, nightEnd = dawnStart;
    
        Color color;
        
        // Dawn transition
        if (tod >= dawnStart && tod < dawnEnd)
        {
            float t = (tod - dawnStart) / (dawnEnd - dawnStart);
            color = Color.Lerp(RenderEngine.NightSkyColor, RenderEngine.DawnDuskSkyColor, t);
            SunIntensity = GMath.Smoothstep(t);
            SunIntensity = GMath.Clamp(SunIntensity, 0f, 1f);
        }
        else if (tod >= dayStart && tod < dayEnd) // Day transition
        {
            // Optionally blend slightly at edges
            float blendEdge = 2.0f;
            
            if (tod < dayStart + blendEdge) // dawn to day
            {
                float t = (tod - dayStart) / blendEdge;
                color = Color.Lerp(RenderEngine.DawnDuskSkyColor, RenderEngine.DaySkyColor, t);
            }
            else if (tod > dayEnd - blendEdge) // day to dusk
            {
                float t = (tod - (dayEnd - blendEdge)) / blendEdge;
                color = Color.Lerp(RenderEngine.DaySkyColor, RenderEngine.DawnDuskSkyColor, t);
            }
            else // full day
            {
                color = RenderEngine.DaySkyColor;
                SunIntensity = 1f;
            }
        }
        else if (tod >= duskStart && tod < duskEnd) // Dusk transition
        {
            float t = (tod - duskStart) / (duskEnd - duskStart);
            color = Color.Lerp(RenderEngine.DawnDuskSkyColor, RenderEngine.NightSkyColor, t);
            SunIntensity = 1f - GMath.Smoothstep(t);
            SunIntensity = GMath.Clamp(SunIntensity, 0f, 1f);
        }
        else // Night transition (19–24 OR 0–5)
        {
            // Handle night wrapping 19–24 and 0–5
            float t;
            
            if (tod >= nightStart && tod < 24f)
                t = (tod - nightStart) / (24f - nightStart);
            else // 0–5
                t = tod / nightEnd;

            SunIntensity = 0f;
            color = Color.Lerp(RenderEngine.NightSkyColor, RenderEngine.NightSkyColor, t); // pure night, no blend
        }
    
        RenderEngine.CurrentSkyColor = color;
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