/*namespace DIBBLES.Scenes;

using System;
using Raylib_cs;
using static Raylib_cs.Raylib;

public class TestScene
{
    private Shader skyboxShader;
    private Model skybox;
    
    public void Initialize()
    {
        // NOTE: Adjust shader locations as needed; check your Raylib-CS/shader setup!
        skyboxShader = LoadShader(
            "resources/shaders/glsl330/skybox.vs",
            "resources/shaders/glsl330/skybox.fs"
        );
        
        Mesh cube = GenMeshCube(1.0f, 1.0f, 1.0f);
        skybox = LoadModelFromMesh(cube);
        
        skybox.Materials[0].Shader = skyboxShader;

        // Set the shader values
        int envMapLoc = GetShaderLocation(skyboxShader, "environmentMap");
        SetShaderValue(skyboxShader, envMapLoc, new int[] { (int)MaterialMapIndex.Cubemap }, ShaderUniformDataType.Int);

        int doGammaLoc = GetShaderLocation(skyboxShader, "doGamma");
        SetShaderValue(skyboxShader, doGammaLoc, new int[] { 0 }, ShaderUniformDataType.Int);

        int vflippedLoc = GetShaderLocation(skyboxShader, "vflipped");
        SetShaderValue(skyboxShader, vflippedLoc, new int[] { 0 }, ShaderUniformDataType.Int);

        Image img = LoadImage("resources/skybox.png");
        TextureCubemap cubemap = LoadTextureCubemap(img, CubemapLayout.AutoDetect);
        UnloadImage(img);

        skybox.Materials[0].Maps[(int)MaterialMapIndex.Cubemap].Texture = cubemap;

        while (!WindowShouldClose())
        {
            
        }
    }

    public void Update(float deltaTime)
    {
        
    }

    public void Render(float deltaTime)
    {
        UpdateCamera(ref camera, CameraMode.CAMERA_FIRST_PERSON);

        BeginDrawing();
        ClearBackground(Raylib_cs.Color.RAYWHITE);

        BeginMode3D(camera);

        Raylib_cs.Rlgl.rlDisableBackfaceCulling();
        Raylib_cs.Rlgl.rlDisableDepthMask();

        DrawModel(skybox, new System.Numerics.Vector3(0, 0, 0), 1.0f, Raylib_cs.Color.WHITE);

        Raylib_cs.Rlgl.rlEnableBackfaceCulling();
        Raylib_cs.Rlgl.rlEnableDepthMask();

        DrawGrid(10, 1.0f);

        EndMode3D();

        DrawFPS(10, 10);

        EndDrawing();
    }

    public void Unload()
    {
        UnloadShader(skyboxShader);
        UnloadTexture(cubemap);
        UnloadModel(skybox);
    }
}*/