using System.Runtime.Intrinsics.X86;
using DIBBLES.Gameplay.Terrain;
using DIBBLES.Scenes;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

namespace DIBBLES.Systems.SFX;

/// <summary>
/// Global class that manages all soundscapes.
/// </summary>
public class WorldSound
{
    public const int AudioPlayersCount = 1;
    public const float SwarmRadius = 5f;
    public const float SwarmSpeed = 0.1f;
    
    private List<AudioPlayer> AudioPlayers = new();
    private static Dictionary<string, Soundscape> soundscapes = new();

    private Block BlockBeneathPlayer => TerrainGameplay.BlockAtPlayersFeet;
    
    public void Start()
    {
        // Populate audio player collection
        for (int i = 0; i < AudioPlayersCount; i++)
        {
            var audioPlayer = new AudioPlayer();
            AudioPlayers.Add(audioPlayer);
        }
        
        // Initialize soundscapes here
        
        // Plains
        var plains = new Soundscape("Plains");
        plains.LoadSound("TestWhoosh");

        foreach (var audioPlayer in AudioPlayers)
        {
            // TODO: Get random soundscape sound from 0 to Sounds.Count
            audioPlayer.Sound = plains.Sounds[0];
        }
    }

    // TODO: Implement smooth random swarming motion for audio players
    public void Update()
    {
        for (int i = 0; i < AudioPlayers.Count; i++)
        {
            var audioBugPos = computeSwarmPosition(i, SwarmRadius, SwarmSpeed);
            var audioBug = AudioPlayers[i];
            
            audioBug.Position = GameScene.PlayerCharacter.Position.ToVector3() + audioBugPos;
            
            if (!audioBug.IsPlaying)
                audioBug.Play();

            AudioPlayers[i] = audioBug;
        }
    }

    public void DebugDraw()
    {
        foreach (var audioBug in AudioPlayers)
        {
            Debug.DrawBox(audioBug.Position, new Vector3(0.1f), Color.Blue);
        }
    }
    
    public static Soundscape? GetSoundscape(string name)
    {
        soundscapes.TryGetValue(name, out var soundScape);
        return soundScape;
    }
    
    private Vector3 computeSwarmPosition(int index, float radius, float speed)
    {
        // Unique seeds per instance for randomization
        int seed = index * 523;
        float phaseA = seed * 0.23f;
        float phaseB = seed * 0.51f;
        float phaseC = seed * 0.37f;
    
        // Time evolves smoothly per bug
        float time = Time.time * speed + seed * 0.017f;

        // Spherical coordinates with smooth oscillation
        float theta = time + phaseA;
        float phi = MathF.Sin(time * 0.5f + phaseB) * MathF.PI + MathF.PI/2f + phaseC;

        // Radial jitter for bug spread
        float r = radius * (0.7f + MathF.Sin(time + phaseB) * 0.3f);

        // Convert spherical to cartesian
        float x = r * MathF.Sin(phi) * MathF.Cos(theta);
        float y = r * MathF.Cos(phi);
        float z = r * MathF.Sin(phi) * MathF.Sin(theta);

        return new Vector3(x, y, z);
    }
}