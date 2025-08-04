using Raylib_cs;
using System.Numerics;
using DIBBLES.Rendering;
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Scenes;

public class PathTracerScene : Scene
{
    private VoxelPathTracer pathTracer;
    private float cameraAngle = 0f;
    private Vector3 cameraPos = new Vector3(0, 0, 20);

    public override void Start()
    {
        pathTracer = new VoxelPathTracer();
    }

    public override void Update()
    {
        cameraAngle += Time.DeltaTime * 30f;
        cameraPos = new Vector3(
            (float)Math.Cos(GMath.ToRadians(cameraAngle)) * 20,
            5,
            (float)Math.Sin(GMath.ToRadians(cameraAngle)) * 20
        );
    }

    public override void Draw()
    {
        pathTracer.Render(cameraPos, pathTracer.GetOutputTexture());

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.Black);
        Raylib.DrawTexturePro(
            pathTracer.GetOutputTexture().Texture,
            new Rectangle(0, Engine.VirtualScreenHeight, Engine.VirtualScreenWidth, -Engine.VirtualScreenHeight),
            new Rectangle(0, 0, Engine.ScreenWidth, Engine.ScreenHeight),
            new Vector2(0, 0),
            0,
            Color.White
        );
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }

    public void Unload()
    {
        pathTracer.Unload();
    }
}