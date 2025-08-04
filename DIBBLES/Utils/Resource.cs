using System.Runtime.InteropServices;
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
        var shader = Raylib.LoadShader($"Assets/Shaders/{vsName}", $"Assets/Shaders/{fsName}");
        shaders.Add(shader);
        
        return shader;
    }
    
    public static Shader LoadComputeShader(string compName)
    {
        string compPath = FindFile(compName);
        string shaderCode = File.ReadAllText(compPath);
        Shader shader;
        unsafe
        {
            IntPtr codePtr = Marshal.StringToHGlobalAnsi(shaderCode);
            IntPtr locsPtr = Marshal.AllocHGlobal(Rlgl.MAX * sizeof(int));
            for (int i = 0; i < Rlgl.MAX; i++)
            {
                ((int*)locsPtr)[i] = -1; // Initialize locations to -1
            }
            try
            {
                shader = new Shader
                {
                    Id = GL.rlLoadComputeShaderProgram(codePtr),
                    Locs = (int*)locsPtr
                };
            }
            finally
            {
                Marshal.FreeHGlobal(codePtr);
                // Note: locsPtr is not freed here because Shader struct takes ownership
            }
        }
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