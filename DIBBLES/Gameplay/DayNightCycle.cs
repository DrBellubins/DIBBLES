using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Systems.DebugMenu;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

namespace DIBBLES.Gameplay;

// TODO: In between Dusk - Day transition is gray
public class DayNightCycle
{
    public float TimeOfDay = 8.0f; // 0..24

    private float testValue = 1.0f;
    
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
    
    public static Color ZenithColor = new();
    public static Color HorizonColor = new();
    
    public static Color DaySkyColor = new Color(0.423529411765f, 0.705882352941f, 1.0f);
    public static Color SunriseSunsetColor = new Color(0.98f, 0.6f, 0.41f, 1f);
    public static Color NightSkyColor = new Color(0.08f, 0.10f, 0.18f, 1f);
    
    public static Color AmbientLightColor = new();
    
    public static Color AmbientDayColor = DaySkyColor.HSV(1f, 0.45f, 0.3f);
    public static Color AmbientSunriseSunsetColor = SunriseSunsetColor.HSV(1f, 0.35f, 0.2f);
    public static Color AmbientNightColor = NightSkyColor.HSV(1f, 0.35f, 1.0f);
    
    private bool _lastIsDay = true;

    public void Start()
    {
        Commands.Register("time", "Set time of day", timeCMD);
        
        DebugMenu.RegisterMenuItem("DayNight",
        
            new SliderParam("Time of day", 0f, 24f, () => TimeOfDay, v => TimeOfDay = v),
            new SliderParam("Test", 0f, 10f, () => testValue, v => testValue = v)
        );
    }
    
    public void Update()
    {
        // Advance time
        //TimeOfDay += (Time.DeltaTime / RealSecondsPerGameHour);
        
        if (TimeOfDay >= FullDayHours)
            TimeOfDay -= FullDayHours;

        // Determine day/night
        bool currentlyDay = TimeOfDay >= DayStart && TimeOfDay < NightStart;

        // Detect transition
        if (currentlyDay != _lastIsDay)
        {
            IsDay = currentlyDay;
            //NeedsRelight = true;
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

        Color sunriseSunsetHorizon = SunriseSunsetColor;
        Color sunriseSunsetZenith = SunriseSunsetColor.Darken(0.2f);
        
        Color DayHorizon = DaySkyColor.Brighten(1f);
        Color DayZenith = DaySkyColor;
        
        Color NightHorizon = NightSkyColor.MultiplyValue(2.5f);
        Color NightZenith = NightSkyColor.Darken(0.2f);
        
        // Update sky colors
        if (tod >= dawnStart && tod < dawnEnd) // 5–7 - Sunrise
        {
            float t = (tod - dawnStart) / (dawnEnd - dawnStart);

            HorizonColor = Color.Lerp(NightHorizon, sunriseSunsetHorizon, t);
            ZenithColor  = Color.Lerp(NightZenith, DayZenith, t * 0.74f);

            AmbientLightColor = Color.Lerp(AmbientNightColor, AmbientSunriseSunsetColor, t);
            SunIntensity = MathHelper.SmoothStep(0f, 1f, t);
        }
        else if (tod >= dawnEnd && tod < dawnEnd + 1f) // 7–8 fade sunrise -> day
        {
            float t = (tod - dawnEnd) / 1.0f;

            HorizonColor = Color.Lerp(sunriseSunsetHorizon, DayHorizon, t);
            ZenithColor  = DayZenith; // stabilizes pure blue
            
            AmbientLightColor = Color.Lerp(AmbientSunriseSunsetColor, AmbientDayColor, t);
            SunIntensity = 1f;
        }
        else if (tod >= dayStart + 1f && tod < duskStart) // 8–17 - Day
        {
            HorizonColor = DayHorizon;
            ZenithColor  = DayZenith;
            
            AmbientLightColor = AmbientDayColor;
            SunIntensity = 1f;
        }
        else if (tod >= duskStart && tod < duskEnd) // 17–19 - Sunset
        {
            float t = (tod - duskStart) / (duskEnd - duskStart);

            HorizonColor = Color.Lerp(DayHorizon, sunriseSunsetHorizon, t);
            ZenithColor  = Color.Lerp(DayZenith, NightZenith, t);
            
            AmbientLightColor = Color.Lerp(AmbientDayColor, AmbientSunriseSunsetColor, t);
            SunIntensity = MathHelper.SmoothStep(1f, 0f, t);
        }
        else if (tod >= duskEnd && tod < duskEnd + 1f) // 19–20 fade sunset -> night
        {
            float t = (tod - duskEnd) / 1.0f;

            HorizonColor = Color.Lerp(sunriseSunsetHorizon, NightHorizon, t);
            ZenithColor  = NightZenith;

            AmbientLightColor = Color.Lerp(AmbientSunriseSunsetColor, AmbientNightColor, t);
            SunIntensity = 0f;
        }
        else // night
        {
            HorizonColor = NightHorizon;
            ZenithColor  = NightZenith;
            
            AmbientLightColor = AmbientNightColor;
            SunIntensity = 0f;
        }

        CurrentSkyColor = ZenithColor;
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