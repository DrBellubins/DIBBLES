namespace DIBBLES.Gameplay;

public class DayNightCycle
{
    public static float WorldTime;     // In seconds or ticks
    public static float DayLength = 1200f; // Length of a day in seconds (20 minutes)
    public static bool IsDay => WorldTime % DayLength < DayLength / 2;
    
    public void Start()
    {
        
    }
    
    public void Update()
    {
        
    }
    
    public void Draw()
    {
        
    }
}