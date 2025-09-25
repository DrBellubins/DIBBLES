using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

public class AudioPlayer
{
    public Vector3 Position = Vector3.Zero;
    public SoundEffect Sound;
    private SoundEffectInstance instance;

    public float Volume = 1.0f;
    public float Pitch = 0.0f; // MonoGame: -1.0f (down 1 octave) to +1.0f (up 1 octave)
    public bool IsPlaying => instance != null && instance.State == SoundState.Playing;

    public float MaxDistance = 5.0f;
    public float MinDistance = 1.0f;
    public float DopplerFactor = 0.0f;
    public float MinPitch = -1.0f; // MonoGame pitch range
    public float MaxPitch = 1.0f;
    public float RandomPitchRange = 0.2f;

    public void Play(Vector3 listenerPosition, Vector3 listenerForward, Vector3 listenerVelocity = default)
    {
        if (Sound == null)
            return;

        // Dispose previous instance if needed
        instance?.Dispose();
        instance = Sound.CreateInstance();

        // --- Distance-based volume ---
        float distance = Vector3.Distance(Position, listenerPosition);
        float norm = (MaxDistance - distance) / (MaxDistance - MinDistance);
        
        norm = MathHelper.Clamp(norm, 0.0f, 1.0f);
        instance.Volume = Volume * norm;

        // --- Stereo panning based on listener orientation ---
        Vector3 toSource = Vector3.Normalize(Position - listenerPosition);
        Vector3 listenerRight = Vector3.Normalize(Vector3.Cross(Vector3.Up, listenerForward));
        
        float pan = Vector3.Dot(toSource, listenerRight);
        pan = MathHelper.Clamp(pan, -1.0f, 1.0f);
        
        instance.Pan = pan; // MonoGame: -1=left, 0=center, 1=right

        // --- Pitch (with Doppler if desired) ---
        Vector3 toListener = Position - listenerPosition;
        float pitch = Pitch;
        
        if (DopplerFactor > 0f)
        {
            Vector3 relativeVelocity = listenerVelocity; // Assuming source is static, add source velocity if needed
        
            float speedTowardsListener = Vector3.Dot(relativeVelocity, Vector3.Normalize(toListener));
            float dopplerPitch = Pitch + (speedTowardsListener * DopplerFactor);
        
            pitch = Math.Clamp(dopplerPitch, MinPitch, MaxPitch);
        }

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
    }

    public void Play2D()
    {
        if (Sound == null)
            return;

        instance?.Dispose();
        instance = Sound.CreateInstance();

        float pitch = Pitch;
        
        if (RandomPitchRange > 0f)
            pitch += GMath.NextFloat(-RandomPitchRange, RandomPitchRange);
        
        pitch = MathHelper.Clamp(pitch, MinPitch, MaxPitch);
        instance.Pitch = pitch;
        instance.Volume = Volume;
        instance.Pan = 0f;

        instance.Play();
    }

    public void Stop()
    {
        instance?.Stop();
    }

    public void Unload()
    {
        instance?.Dispose();
        instance = null;
        Sound?.Dispose();
        Sound = null;
    }
}

/*using Raylib_cs;
using System.Numerics;
using DIBBLES.Utils;

namespace DIBBLES.Systems;

public class AudioPlayer
{
    public Vector3 Position = Vector3.Zero;
    public Sound Sound;
    
    public float Volume = 5.0f;
    public float Pitch = 1.0f;
    public bool IsPlaying = false;

    // Audio parameters
    public float MaxDistance = 20.0f; // Distance at which sound volume becomes zero
    public float MinDistance = 1.0f;  // Minimum distance for max volume
    public float DopplerFactor = 0.0f; // Strength of Doppler effect
    public float MinPitch = 0.5f;     // Minimum pitch for Doppler
    public float MaxPitch = 2.0f;     // Maximum pitch for Doppler
    public float RandomPitchRange = 0.2f; // How much the pitch changes either up or down

    // Play the sound with 3D audio effects relative to listener position and orientation
    public void Play(Vector3 listenerPosition, Vector3 listenerDirection, Vector3 listenerVelocity = default)
    {
        if (Sound.FrameCount == 0) return; // No sound assigned

        // Calculate distance to listener
        Vector3 toListener = Position - listenerPosition;
        float distance = toListener.Length();

        // Calculate volume based on distance (inverse distance law)
        float volume = Volume * Math.Clamp((MaxDistance - distance) / (MaxDistance - MinDistance), 0.0f, 1.0f);
        Raylib.SetSoundVolume(Sound, volume);

        // Calculate panning (-1.0f left to 1.0f right) based on listener's direction
        Vector3 listenerRight = Vector3.Cross(new Vector3(0.0f, 1.0f, 0.0f), listenerDirection);
        
        listenerRight = Vector3.Normalize(listenerRight);
        
        float pan = Vector3.Dot(Vector3.Normalize(toListener), listenerRight);
        
        pan = Math.Clamp(pan, -1.0f, 1.0f);
        
        Raylib.SetSoundPan(Sound, 0.5f + pan * 0.5f); // Convert to 0.0f-1.0f range

        // Calculate Doppler effect (pitch shift based on relative velocity)
        if (DopplerFactor > 0f)
        {
            Vector3 relativeVelocity = listenerVelocity; // Assuming source is static, add source velocity if needed
        
            float speedTowardsListener = Vector3.Dot(relativeVelocity, Vector3.Normalize(toListener));
            float pitch = Pitch + (speedTowardsListener * DopplerFactor);
        
            pitch = Math.Clamp(pitch, MinPitch, MaxPitch);
        
            Raylib.SetSoundPitch(Sound, pitch);
        }
        
        // Set random pitch before playing
        if (RandomPitchRange > 0.0f)
        {
            var rndPitch = Pitch + GMath.NextFloat(-RandomPitchRange, RandomPitchRange);
            Raylib.SetSoundPitch(Sound, rndPitch);
        }

        // Play the sound
        Raylib.PlaySound(Sound);
        IsPlaying = true;
    }

    public void Play2D()
    {
        if (Sound.FrameCount == 0) return; // No sound assigned

        // Set random pitch before playing
        if (RandomPitchRange > 0.0f)
        {
            var rndPitch = Pitch + GMath.NextFloat(-RandomPitchRange, RandomPitchRange);
            Raylib.SetSoundPitch(Sound, rndPitch);
        }
        
        // Play the sound
        Raylib.PlaySound(Sound);
        IsPlaying = true;
    }
    
    // Stop the sound
    public void Stop()
    {
        if (IsPlaying)
        {
            Raylib.StopSound(Sound);
            IsPlaying = false;
        }
    }

    // Update playing state (call when sound finishes)
    public void Update()
    {
        IsPlaying = Raylib.IsSoundPlaying(Sound);
    }

    // Unload the sound
    public void Unload()
    {
        if (Sound.FrameCount != 0)
        {
            Raylib.UnloadSound(Sound);
        }
    }
}*/