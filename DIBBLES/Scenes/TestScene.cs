using System.Numerics;
using DIBBLES.Effects;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Scenes;

public class TestScene : Scene
{
    private Player player = new Player();
    private CRTEffect crtEffect = new CRTEffect();
    
    private Shader skyboxShader;
    private Texture2D skyboxTexture;
    
    private Mesh cubemesh;
    private Model skybox;
    
    private Texture2D groundTexture;
    private Material groundMaterial;
    private Model planeModel;
    private BoundingBox groundBox;

    public override void Start()
    {
        // Load skybox shader
        skyboxShader = Resource.LoadShader("skybox.vs", "skybox.fs");

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
        var skyImg = Raylib.LoadImage("Assets/Textures/skybox.png");

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
                skyboxTexture = Raylib.LoadTextureCubemap(skyImg, CubemapLayout.AutoDetect);
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
            int texLoc = Raylib.GetShaderLocation(skyboxShader, "environmentMap");
            if (texLoc == -1)
            {
                Console.WriteLine("Failed to find environmentMap uniform in skybox shader");
            }
            int texUnit = 0;
            Raylib.SetShaderValue(skyboxShader, texLoc, texUnit, ShaderUniformDataType.Int);
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

        // Setup ground material
        groundTexture = Resource.Load<Texture2D>("grass_dark.png");
        groundMaterial = Raylib.LoadMaterialDefault();
        Raylib.SetMaterialTexture(ref groundMaterial, MaterialMapIndex.Albedo, groundTexture);

        // Create plane mesh
        var planeMesh = MeshUtils.GenMeshPlaneTiled(50.0f, 50.0f, 1, 1, 10.0f, 10.0f);
        planeModel = Raylib.LoadModelFromMesh(planeMesh);

        unsafe
        {
            planeModel.Materials[0] = groundMaterial;
        }

        groundBox = new BoundingBox(
            new Vector3(-25.0f, -0.1f, -25.0f),
            new Vector3(25.0f, 0.0f, 25.0f)
        );
        
        player.Start();
        crtEffect.Start();
    }

    public override void Update()
    {
        player.Update(groundBox);
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        
        crtEffect.DrawStart(Time.time);
        
        Raylib.ClearBackground(Color.Black);
        
        Raylib.BeginMode3D(player.Camera);
        
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
        
        // Bind cubemap texture explicitly before drawing
        Rlgl.ActiveTextureSlot(0);
        Rlgl.EnableTexture(skyboxTexture.Id);
        //Console.WriteLine($"Texture bound: ID={skyboxTexture.Id}"); (in console = 4 currently)
        
        Raylib.DrawModel(skybox, Vector3.Zero, 1.0f, Color.White);
        
        Raylib.EndShaderMode();
        Rlgl.DisableTexture();
        Rlgl.EnableBackfaceCulling();
        Rlgl.EnableDepthMask();
        
        // Draw ground plane
        Raylib.DrawModel(planeModel, Vector3.Zero, 1.0f, Color.White);
        
        Raylib.EndMode3D();
        
        // Debug: Draw skyboxTexture to check contents
        //if (skyboxTexture.Id != 0)
        //{
        //    Raylib.DrawTexture(skyboxTexture, 0, 100, Color.White);
        //}
        
        // Draw simple crosshair
        Raylib.DrawRectangle(Engine.VirtualScreenWidth / 2 - 1, Engine.VirtualScreenHeight / 2 - 1, 2, 2, Color.White);
        
        // Test text
        Raylib.DrawTextEx(Engine.MainFont, $"FPS: {1f / Time.DeltaTime}", Vector2.Zero, 18f, 2f, Color.White);
        
        crtEffect.DrawEnd();
        
        Raylib.EndDrawing();
    }

    public void Unload()
    {
        Raylib.UnloadTexture(groundTexture);
        Raylib.UnloadModel(planeModel);
        Raylib.UnloadTexture(skyboxTexture);
        Raylib.UnloadShader(skyboxShader);
        Raylib.UnloadModel(skybox);
    }
}