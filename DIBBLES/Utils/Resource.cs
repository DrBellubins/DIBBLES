using Raylib_cs;

namespace DIBBLES;

public static class Resource
{
    private static string execDirectory = AppContext.BaseDirectory;
    private static string assetsPath = $"{execDirectory}/Assets";

    private static List<Texture2D> textures = new List<Texture2D>();
    private static List<Shader> shaders = new List<Shader>();
    
    public static string FindFile(string fileName)
    {
        if (!Directory.Exists(assetsPath))
            throw new DirectoryNotFoundException(assetsPath);

        // Search recursively for the file
        var files = Directory.EnumerateFiles(assetsPath, fileName, SearchOption.AllDirectories);
        return files.First();
    }
    
    // Load method that adds preprocessing universally.
    public static T Load<T>(string filename)
    {
        var file = FindFile(filename);
        
        if (file == string.Empty)
            throw new FileNotFoundException($"File '{filename}' not found.");

        switch (typeof(T))
        {
            case Type t when t == typeof(Texture2D):
            {
                var texture = Raylib.LoadTexture(file);
                Raylib.GenTextureMipmaps(ref texture);
                Raylib.SetTextureFilter(texture, TextureFilter.Point);
                
                textures.Add(texture);
                
                return (T)(object)texture;
            }
            
            case Type t when t == typeof(Sound):
            {
                return (T)(object)Raylib.LoadSound(file);
            }

            case Type t when t == typeof(Music):
            {
                return (T)(object)Raylib.LoadMusicStream(file);
            }

            default:
                throw new ArgumentException($"Unsupported type: {typeof(T).Name}");
        }
    }

    public static Shader LoadShader(string vsName, string fsName)
    {
        var shader = Raylib.LoadShader(vsName, fsName);
        shaders.Add(shader);
        
        return shader;
    }

    public static void UnloadAllResources()
    {
        foreach (var texture in textures)
            Raylib.UnloadTexture(texture);
        
        foreach (var shader in shaders)
            Raylib.UnloadShader(shader);
    }
}