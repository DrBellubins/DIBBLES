using System.Numerics;
using DIBBLES.Effects;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Scenes;

public class TestScene : Scene
{
    private Player player = new Player();
    //private SrcPlayer player =  new SrcPlayer();
    private CRTEffect crtEffect = new CRTEffect();
    
    private Texture2D groundTexture;
    private Material groundMaterial;
    private Model planeModel;
    private BoundingBox groundBox;
    
    public override void Start()
    {
        // Setup ground material (temporary)
        groundTexture = Resource.Load<Texture2D>("grass_dark.png");
        groundMaterial = Raylib.LoadMaterialDefault();
        
        Raylib.SetMaterialTexture(ref groundMaterial, MaterialMapIndex.Albedo, groundTexture);
        
        // Create plane mesh
        var planeMesh = MeshUtils.GenMeshPlaneTiled(50.0f, 50.0f, 1, 1, 10.0f, 10.0f); // 50x50 plane, 1x1 subdivisions
        planeModel = Raylib.LoadModelFromMesh(planeMesh);

        unsafe
        {
            planeModel.Materials[0] = groundMaterial; // Assign material to model
        }
        
        groundBox = new BoundingBox(
            new Vector3(-25.0f, -0.1f, -25.0f), // Slightly below y=0 for tolerance
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
            
        Raylib.ClearBackground(Color.SkyBlue);
        Raylib.BeginMode3D(player.Camera);
        
        // Draw ground plane
        Raylib.DrawModel(planeModel, new Vector3(0.0f, 0.0f, 0.0f), 1.0f, Color.White);
        
        Raylib.EndMode3D();
            
        // Draw simple crosshair
        Raylib.DrawRectangle(Engine.VirtualScreenWidth / 2 - 1, Engine.VirtualScreenHeight / 2 - 1, 2, 2, Color.White);
            
        // Test text
        Raylib.DrawTextEx(Engine.MainFont, "Hello World! This is a test", Vector2.Zero, 18f, 2f,Color.White);
            
        crtEffect.DrawEnd();
        
        Raylib.DrawFPS(10, 10);
            
        Raylib.EndDrawing();
    }

    public void Unload()
    {
        Raylib.UnloadTexture(groundTexture);
        Raylib.UnloadModel(planeModel); // Also unloads the material
    }
}