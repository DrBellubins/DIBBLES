/*using System.Numerics;
using DIBBLES.Effects;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Scenes;

public class TestScene : Scene
{
    private Player player = new Player();
    
    private Skybox skybox = new Skybox();
    private CRTEffect crtEffect = new CRTEffect();
    
    private Texture2D groundTexture;
    private Material groundMaterial;
    private Model planeModel;
    private BoundingBox groundBox;
    
    public override void Start()
    {
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
        skybox.Start();
        crtEffect.Start();
    }

    public override void Update()
    {
        //player.Update(groundBox);
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        
        crtEffect.DrawStart(Time.time);
        
        Raylib.ClearBackground(Color.Black);
        
        Raylib.BeginMode3D(player.Camera);
        
        skybox.Draw(player);
        
        Raylib.DrawSphere(Vector3.Zero, 1f, Color.Green);
        
        // Draw ground plane
        Raylib.DrawModel(planeModel, Vector3.Zero, 1.0f, Color.White);
        
        Raylib.EndMode3D();
        
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
        skybox.Unload();
    }
}*/