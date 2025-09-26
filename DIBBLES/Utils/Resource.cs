using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using NVorbis;

namespace DIBBLES.Utils;

public static class Resource
{
    private static string execDirectory = AppContext.BaseDirectory;
    private static string assetsPath = Path.Combine(execDirectory, "Assets");

    private static List<Texture2D> textures = new();
    private static List<SoundEffect> sounds = new();
    //private static List<Effect> shaders = new();
    
    private static string FindTexture(string fileName, bool isItem)
    {
        string path = Path.Combine(assetsPath, "Textures", isItem ? "Items" : "Blocks");
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
            return Path.Combine(path, "Error.png");
            //throw new FileNotFoundException($"Texture file '{fullPath}' not found.");

        return fullPath;
    }

    private static string FindSound(string fileName, bool isItem)
    {
        string path = Path.Combine(assetsPath, "Sounds", isItem ? "Items" : "Blocks");
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
            return Path.Combine(path, "Error.ogg");
            //throw new FileNotFoundException($"Sound file '{fullPath}' not found.");

        return fullPath;
    }

    private static string FindMusic(string fileName)
    {
        string path = Path.Combine(assetsPath, "Music");
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Music file '{fullPath}' not found.");

        return fullPath;
    }

    // Load method for Texture2D and SoundEffect
    public static T Load<T>(string fileName, bool isItem = false)
    {
        if (typeof(T) == typeof(Texture2D))
        {
            string file = FindTexture(fileName, isItem);
            
            var texture = Texture2D.FromFile(Engine.Graphics, file);
            textures.Add(texture);
            
            return (T)(object)texture;
        }
        else if (typeof(T) == typeof(SoundEffect))
        {
            string file = FindSound(fileName, isItem);

            var sound = LoadOggSound(file);
            sounds.Add(sound);
            
            return (T)(object)sound;
        }
        //else if (typeof(T) == typeof(Effect))
        //{
        //    // TODO: Use Content.Load<Effect> later
        //}
        else
        {
            throw new ArgumentException($"Unsupported type: {typeof(T).Name}");
        }
    }

    public static SoundEffect LoadOggSound(string filePath)
    {
        using (var vorbis = new VorbisReader(File.OpenRead(filePath), false))
        {
            int channels = vorbis.Channels;
            int sampleRate = vorbis.SampleRate;
            
            List<float> samples = new List<float>();
            
            float[] readBuffer = new float[4096];
            int read;

            while ((read = vorbis.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
                samples.AddRange(readBuffer.Take(read));

            // Convert float samples [-1,1] to 16-bit PCM
            var pcm = new byte[samples.Count * 2];
            
            for (int i = 0; i < samples.Count; i++)
            {
                short value = (short)Math.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
                pcm[i * 2] = (byte)(value & 0xff);
                pcm[i * 2 + 1] = (byte)((value >> 8) & 0xff);
            }

            // Create SoundEffect
            return new SoundEffect(pcm, sampleRate, (AudioChannels)channels);
        }
    }
    
    //public static Effect LoadShader(string? vsName, string fsName)
    //{
    //    // Comment out for now, will use Content Pipeline later
    //    return null;
    //}

    public static void UnloadAllResources()
    {
        foreach (var texture in textures)
            texture.Dispose();

        foreach (var sound in sounds)
            sound.Dispose();

        //foreach (var shader in shaders)
        //    shader.Dispose();
    }
}