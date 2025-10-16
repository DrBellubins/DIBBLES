using DIBBLES;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

public class AudioPlayer
{
    public Vector3 Position = Vector3.Zero;
    public SoundEffectInstance Sound;
    
    private AudioEmitter emitter = new();
    private AudioListener listener = new();
    
    public float Volume = 1.0f;
    public float Pitch = 0.0f; // MonoGame: -1.0f (down 1 octave) to +1.0f (up 1 octave)
    public bool IsPlaying => Sound != null && Sound.State == SoundState.Playing;

    public float MaxDistance = 5.0f;
    public float MinDistance = 1.0f;
    public float DopplerFactor = 0.0f;
    public float MinPitch = -1.0f; // MonoGame pitch range
    public float MaxPitch = 1.0f;
    public float RandomPitchRange = 0.2f;

    public AudioPlayer()
    {
        Engine.AudioPlayers.Add(this);
    }
    
    public void Update()
    {
        emitter.Position = Position;
        
        listener.Position = GameScene.PlayerCharacter.Position.ToVector3();
        listener.Forward = GameScene.PlayerCharacter.CameraForward;
        listener.Velocity = GameScene.PlayerCharacter.Velocity;
        
        if (Sound != null)
            Sound.Apply3D(listener, emitter);
    }
    
    public void Play()
    {
        if (Sound == null)
            return;
        
        float pitch = Pitch;

        // --- Random pitch variation ---
        if (RandomPitchRange > 0f)
        {
            float randomPitch = GMath.NextFloat(-RandomPitchRange, RandomPitchRange);
            pitch += randomPitch;
        }
        
        pitch = MathHelper.Clamp(pitch, MinPitch, MaxPitch);
        Sound.Pitch = pitch;
        
        // Play
        Sound.Play();
    }
    
    public void Stop()
    {
        Sound?.Stop();
    }
    
    public void Unload()
    {
        Sound?.Dispose();
        Sound = null;
    }
}