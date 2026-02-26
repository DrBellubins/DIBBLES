using DIBBLES.Terrain;

namespace DIBBLES.Systems.SFX;

/// <summary>
/// Global class that manages all soundscapes.
/// </summary>
public class WorldSound
{
    private static Dictionary<string, Soundscape> soundscapes = new();

    private Block? BlockBeneathPlayer;
    
    public void Start()
    {
        // Initialize soundscapes here
        
        // Plains
    }

    public void Update()
    {
        
    }
    
    public static Soundscape? GetSoundscape(string name)
    {
        soundscapes.TryGetValue(name, out var soundScape);
        return soundScape;
    }
}