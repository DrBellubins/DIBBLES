using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;

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
            throw new FileNotFoundException($"Texture file '{fullPath}' not found.");

        return fullPath;
    }

    private static string FindSound(string fileName, bool isItem)
    {
        string path = Path.Combine(assetsPath, "Sounds", isItem ? "Items" : "Blocks");
        string fullPath = Path.Combine(path, fileName);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Sound file '{fullPath}' not found.");

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
            
            var texture = Texture2D.FromFile(MonoEngine.Graphics.GraphicsDevice, file);
            textures.Add(texture);
            
            return (T)(object)texture;
        }
        else if (typeof(T) == typeof(SoundEffect))
        {
            string file = FindSound(fileName, isItem);
            
            var sound = SoundEffect.FromFile(file);
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

    public static SoundEffect LoadSoundSpecial(string fileName)
    {
        string path = Path.Combine(assetsPath, "Sounds", fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Sound file '{path}' not found.");
        var sound = SoundEffect.FromFile(path);
        sounds.Add(sound);
        return sound;
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