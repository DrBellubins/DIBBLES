using DIBBLES;
using DIBBLES.Scenes;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

// TODO: Implement ability to play 2D sounds
public class AudioPlayer
{
    public Vector3 Position = Vector3.Zero;
    public SoundEffect? Sound;
    
    private SoundEffectInstance? instance;
    
    private AudioEmitter emitter = new();
    private AudioListener listener = new();
    
    public float Volume = 1.0f;
    public float Pitch = 0.0f; // MonoGame: -1.0f (down 1 octave) to +1.0f (up 1 octave)
    public bool IsPlaying => Sound != null && instance?.State == SoundState.Playing;

    public bool IsLooped = false;
    
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
    
    public void Update()
    {
        emitter.Position = Position;
        
        listener.Position = GameScene.PlayerCharacter.Position.ToVector3();
        listener.Forward = GameScene.PlayerCharacter.CameraForward;
        listener.Velocity = GameScene.PlayerCharacter.Velocity;

        if (instance != null)
        {
            /*Debug.Info($"AudioListener pos: {listener.Position}, vel: {listener.Velocity}, forward: {listener.Forward}");
            Debug.Info($"AudioEmitter pos: {emitter.Position}, vel: {emitter.Velocity}");

            if (float.IsNaN(listener.Position.X) || float.IsInfinity(listener.Position.X)
                || float.IsNaN(listener.Position.Y) || float.IsInfinity(listener.Position.Y)
                || float.IsNaN(listener.Position.Z) || float.IsInfinity(listener.Position.Z))
            {
                Debug.Error("Listener position is not finite!");
            }*/
            
            instance.Apply3D(listener, emitter);
            
            // Dispose after we've played.
            if (!IsPlaying && hasPlayed)
            {
                //Debug.Info("Audio player unloaded");
                //Engine.AudioPlayers.Remove(this);
                hasPlayed = false; // ensure we only run this block once
            }
        }
    }
    
    public void Play()
    {
        if (Sound == null)
            return;
        
        instance = Sound.CreateInstance();
        instance.IsLooped = IsLooped;
        
        instance.Volume = MathHelper.Clamp(Volume, 0f, 1f);
        instance.Pan = 0f;
        
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

    public void Play(SoundEffect sound)
    {
        Sound = sound;
        Play();
    }
    
    public void Play(SoundEffect sound, Vector3 position)
    {
        Sound = sound;
        Position = position;
        Play();
    }
    
    public void Stop()
    {
        instance?.Stop();
    }
}