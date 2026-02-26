using DIBBLES.Utils;
using Microsoft.Xna.Framework.Audio;

namespace DIBBLES.Systems.SFX;

/// <summary>
/// A collection of environmental sounds.
/// </summary>
public class Soundscape
{
    public string Name { get; private set; }
    public List<SoundEffect> Sounds = new();

    public bool FadeInAndOut { get; private set; } = false;
    public bool UsesRandomPitch { get; private set; } = true;

    public RangeF RandomPitchRange = new RangeF(-0.25f, 0.25f);
    
    public Soundscape(string name, SoundEffect[] sounds, bool fadeInAndOut = false, bool useRandomPitch = true)
    {
        if (string.IsNullOrEmpty(name))
            Debug.Error("Can't initialize soundscape with no name!");
        
        if (sounds.Length < 1)
            Debug.Error("Can't initialize soundscape with no sounds!");
        
        Name = name;
        Sounds.AddRange(sounds);
        FadeInAndOut = fadeInAndOut;
        UsesRandomPitch = useRandomPitch;
    }
    
    public void Start()
    {
        
    }

    public void LoadSound(string name)
    {
        var sound = Resource.Load<SoundEffect>($"Sounds/World/{name}");
    }
}