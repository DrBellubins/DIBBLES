using DIBBLES;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

public class AudioPlayer
{
    public Vector3 Position = Vector3.Zero;
    public SoundEffect Sound;
    
    private SoundEffectInstance? instance;
    
    private AudioEmitter emitter = new();
    private AudioListener listener = new();
    
    public float Volume = 1.0f;
    public float Pitch = 0.0f; // MonoGame: -1.0f (down 1 octave) to +1.0f (up 1 octave)
    public bool IsPlaying => Sound != null && instance.State == SoundState.Playing;

    public float MaxDistance = 5.0f;
    public float MinDistance = 1.0f;
    public float DopplerFactor = 0.0f;
    public float MinPitch = -1.0f; // MonoGame pitch range
    public float MaxPitch = 1.0f;
    public float RandomPitchRange = 0.2f;

    private bool hasPlayed = false;
    
    public AudioPlayer()
    {
        Engine.AudioPlayers.Add(this);
    }

    public static void CreateAndPlay(SoundEffect sound, Vector3 position)
    {
        var audioPlayer = new AudioPlayer();
        audioPlayer.Sound = sound;
        audioPlayer.Position = position;
        
        Console.WriteLine($"Audio players in buffer: {Engine.AudioPlayers.Count}");
        
        audioPlayer.Play();
    }
    
    public void Update()
    {
        emitter.Position = Position;
        
        listener.Position = GameScene.PlayerCharacter.Position.ToVector3();
        listener.Forward = GameScene.PlayerCharacter.CameraForward;
        listener.Velocity = GameScene.PlayerCharacter.Velocity;

        if (instance != null)
        {
            instance.Apply3D(listener, emitter);
            
            // Dispose after we've played.
            if (!IsPlaying && hasPlayed)
            {
                Console.WriteLine("Unloaded");
                Engine.AudioPlayers.Remove(this);
                hasPlayed = false; // ensure we only run this block once
            }
        }
    }
    
    public void Play()
    {
        if (Sound == null)
            return;
        
        instance = Sound.CreateInstance();
        
        float pitch = Pitch;

        // --- Random pitch variation ---
        if (RandomPitchRange > 0f)
        {
            float randomPitch = GMath.NextFloat(-RandomPitchRange, RandomPitchRange);
            pitch += randomPitch;
        }
        
        pitch = MathHelper.Clamp(pitch, MinPitch, MaxPitch);
        instance.Pitch = pitch;
        
        // Play
        instance.Play();
        hasPlayed = true;
    }
    
    public void Stop()
    {
        instance?.Stop();
    }
}