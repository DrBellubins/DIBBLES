using System.Runtime.InteropServices;
using Raylib_cs;

namespace DIBBLES;

public static class Resource
{
    private static string execDirectory = AppContext.BaseDirectory;
    private static string assetsPath = Path.Combine(execDirectory, "Assets");

    private static List<Texture2D> textures = new List<Texture2D>();
    private static List<Shader> shaders = new List<Shader>();
    
    private static string findTexture(string fileName, bool isItem)
    {
        if (!Directory.Exists(assetsPath))
            throw new DirectoryNotFoundException(assetsPath);

        // Search recursively for the file
        string path;
        
        if (isItem)
            path = Path.Combine(assetsPath, "Textures", "Items");
        else
            path = Path.Combine(assetsPath, "Textures", "Blocks");
        
        var files = Directory.EnumerateFiles(path, fileName, SearchOption.AllDirectories);

        var file = files.First();
        
        if (file == string.Empty)
            throw new FileNotFoundException($"File '{fileName}' not found.");
        
        return file;
    }
    
    private static string findSound(string fileName, bool isItem)
    {
        if (!Directory.Exists(assetsPath))
            throw new DirectoryNotFoundException(assetsPath);

        
        
        // Search recursively for the file
        string path;
        
        if (isItem)
            path = Path.Combine(assetsPath, "Sounds", "Items");
        else
            path = Path.Combine(assetsPath, "Sounds", "Blocks");
        
        var files = Directory.EnumerateFiles(path, fileName, SearchOption.AllDirectories);
        
        var file = files.First();
        
        if (file == string.Empty)
            throw new FileNotFoundException($"File '{fileName}' not found.");
        
        return file;
    }
    
    private static string findMusic(string fileName)
    {
        if (!Directory.Exists(assetsPath))
            throw new DirectoryNotFoundException(assetsPath);

        // Search recursively for the file
        string path = Path.Combine(assetsPath, "Music");
        
        var files = Directory.EnumerateFiles(path, fileName, SearchOption.AllDirectories);
        
        var file = files.First();
        
        if (file == string.Empty)
            throw new FileNotFoundException($"File '{fileName}' not found.");
        
        return file;
    }
    
    // Load method that adds preprocessing universally.
    public static T Load<T>(string fileName, bool isItem = false)
    {
        switch (typeof(T))
        {
            case Type t when t == typeof(Texture2D):
            {
                var file = findTexture(fileName, isItem);
                
                var texture = Raylib.LoadTexture(file);
                
                textures.Add(texture);
                
                return (T)(object)texture;
            }
            
            case Type t when t == typeof(Sound):
            {
                var file = findSound(fileName, isItem);
                return (T)(object)Raylib.LoadSound(file);
            }

            case Type t when t == typeof(Music):
            {
                var file = findMusic(fileName);
                return (T)(object)Raylib.LoadMusicStream(file);
            }

            default:
                throw new ArgumentException($"Unsupported type: {typeof(T).Name}");
        }
    }
    
    public static Shader LoadShader(string? vsName, string fsName)
    {
        var vsFilename = vsName != null ? $"Assets/Shaders/{vsName}" : null;
        var shader = Raylib.LoadShader(vsFilename, $"Assets/Shaders/{fsName}");
        shaders.Add(shader);
        
        return shader;
    }

    public static void UnloadAllResources()
    {
        foreach (var texture in textures)
            Raylib.UnloadTexture(texture);
        
        foreach (var shader in shaders)
        {
            unsafe
            {
                if (shader.Locs != null)
                {
                    Marshal.FreeHGlobal((IntPtr)shader.Locs);
                }
            }
            Raylib.UnloadShader(shader);
        }
    }

    private static class GL
    {
        [DllImport("raylib", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint rlLoadComputeShaderProgram(IntPtr shaderCode);
    }
}