using System.Numerics;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Effects;

public class Skybox
{
    private Shader skyboxShader;
    private Texture2D skyboxTexture;

    private int textureLocation = 0;
    
    private Mesh cubemesh;
    private Model skybox;

    public void Start()
    {
        // Load skybox shader
        skyboxShader = Resource.LoadShader("skybox.vs", "skybox2.fs");

        // Check shader loading
        if (skyboxShader.Id == 0)
        {
            Console.WriteLine("Skybox shader compilation failed");
        }
        else
        {
            Console.WriteLine($"Skybox shader loaded: ID={skyboxShader.Id}");
        }

        // Load skybox texture as cubemap cross (assuming 2048x1536 is a 4x3 layout)
        var skyImg = Raylib.LoadImage("Assets/Textures/skybox_noise.png");

        unsafe
        {
            if (skyImg.Data == null)
            {
                Console.WriteLine("Failed to load skybox.png as image");
                var fallbackImg = Raylib.GenImageColor(512, 512, Color.Blue);
                skyboxTexture = Raylib.LoadTextureFromImage(fallbackImg);
                Raylib.UnloadImage(fallbackImg);
            }
            else
            {
                skyboxTexture = Raylib.LoadTextureFromImage(skyImg);
                Raylib.SetTextureFilter(skyboxTexture, TextureFilter.Bilinear);
                
                //skyboxTexture = Raylib.LoadTextureCubemap(skyImg, CubemapLayout.AutoDetect);
                Raylib.UnloadImage(skyImg);
                Console.WriteLine($"Skybox texture loaded: ID={skyboxTexture.Id}, Width={skyboxTexture.Width}, Height={skyboxTexture.Height}");
            }
        }

        // Check if cubemap was created successfully
        if (skyboxTexture.Id == 0)
        {
            Console.WriteLine("Skybox texture loading failed");
            var fallbackImg = Raylib.GenImageColor(512, 512, Color.Blue);
            skyboxTexture = Raylib.LoadTextureFromImage(fallbackImg);
            Raylib.UnloadImage(fallbackImg);
        }

        cubemesh = Raylib.GenMeshCube(1000.0f, 1000.0f, 1000.0f);
        skybox = Raylib.LoadModelFromMesh(cubemesh);

        unsafe
        {
            skybox.Materials[0].Shader = skyboxShader;
            textureLocation= Raylib.GetShaderLocation(skyboxShader, "environmentMap");
            
            Console.WriteLine($"Skybox material loc: {textureLocation}");
            
            if (textureLocation == -1)
            {
                Console.WriteLine("Failed to find environmentMap uniform in skybox shader");
            }
            
            int texUnit = 2;
            Raylib.SetShaderValue(skyboxShader, textureLocation, texUnit, ShaderUniformDataType.Int);
            int vflippedLoc = Raylib.GetShaderLocation(skyboxShader, "vflipped");
            
            if (vflippedLoc != -1)
            {
                int vflipped = 0; // Test with 0 first, try 1 if needed
                Raylib.SetShaderValue(skyboxShader, vflippedLoc, vflipped, ShaderUniformDataType.Int);
                Console.WriteLine($"vflipped set to: {vflipped}");
            }
            
            int doGammaLoc = Raylib.GetShaderLocation(skyboxShader, "doGamma");
            
            if (doGammaLoc != -1)
            {
                int doGamma = 0; // Disable gamma for now
                Raylib.SetShaderValue(skyboxShader, doGammaLoc, doGamma, ShaderUniformDataType.Int);
                Console.WriteLine($"doGamma set to: {doGamma}");
            }
        }
    }

    public void Draw(Player player)
    {
        // Draw skybox
        Rlgl.DisableBackfaceCulling();
        Rlgl.DisableDepthMask();
        Raylib.BeginShaderMode(skyboxShader);
        
        // Set view and projection matrices
        Matrix4x4 view = Matrix4x4.CreateLookAt(
            player.Camera.Position,
            player.Camera.Target,
            player.Camera.Up
        );
        
        Matrix4x4 rotView = Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromRotationMatrix(view));
        rotView.M44 = 1.0f;
    
        Raylib.SetShaderValueMatrix(skyboxShader, Raylib.GetShaderLocation(skyboxShader, "matView"), rotView);
        Raylib.SetShaderValueMatrix(skyboxShader, Raylib.GetShaderLocation(skyboxShader, "matProjection"), Matrix4x4.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(player.Camera.FovY),
            (float)Engine.VirtualScreenWidth / Engine.VirtualScreenHeight,
            0.1f, 1000.0f
        ));
        
        Raylib.SetShaderValue(skyboxShader, Raylib.GetShaderLocation(skyboxShader, "Time"), Time.time, ShaderUniformDataType.Float);
        
        // Bind cubemap texture explicitly before drawing
        Rlgl.ActiveTextureSlot(2);
        Rlgl.EnableTexture(skyboxTexture.Id);
        
        //Console.WriteLine($"Texture unit: {textureLocation}, Texture ID: {skyboxTexture.Id}");
        
        Raylib.DrawModel(skybox, Vector3.Zero, 1.0f, Color.White);
        
        Raylib.EndShaderMode();
        Rlgl.DisableTexture();
        Rlgl.EnableBackfaceCulling();
        Rlgl.EnableDepthMask();
    }

    public void Unload()
    {
        Raylib.UnloadTexture(skyboxTexture);
        Raylib.UnloadShader(skyboxShader);
        Raylib.UnloadModel(skybox);
    }
}