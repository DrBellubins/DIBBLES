using Raylib_cs;
using System.Numerics;
using DIBBLES.Utils;

namespace DIBBLES.Rendering;

public class VoxelPathTracer
{
    private const int VOXEL_GRID_SIZE = 16;
    private const float VOXEL_SIZE = 1.0f;
    private readonly bool[,,] voxels;
    private readonly Color[,,] voxelColors;
    private readonly Vector3 lightDir = Vector3.Normalize(new Vector3(-1, -1, -1));
    private readonly Color skyColor = Color.SkyBlue;
    private readonly Color lightColor = Color.White;
    private readonly Shader computeShader;
    private readonly uint voxelBuffer;
    private readonly uint colorBuffer;
    private readonly RenderTexture2D outputTexture;

    public VoxelPathTracer()
    {
        voxels = new bool[VOXEL_GRID_SIZE, VOXEL_GRID_SIZE, VOXEL_GRID_SIZE];
        voxelColors = new Color[VOXEL_GRID_SIZE, VOXEL_GRID_SIZE, VOXEL_GRID_SIZE];
        InitializeVoxels();

        // Load compute shader
        computeShader = Resource.LoadComputeShader("path_tracer.comp");
        outputTexture = Raylib.LoadRenderTexture(Engine.VirtualScreenWidth, Engine.VirtualScreenHeight);

        // Create and populate voxel buffer (1 for occupied, 0 for empty)
        int[] voxelData = new int[VOXEL_GRID_SIZE * VOXEL_GRID_SIZE * VOXEL_GRID_SIZE];
        for (int x = 0; x < VOXEL_GRID_SIZE; x++)
        for (int y = 0; y < VOXEL_GRID_SIZE; y++)
        for (int z = 0; z < VOXEL_GRID_SIZE; z++)
            voxelData[x + y * VOXEL_GRID_SIZE + z * VOXEL_GRID_SIZE * VOXEL_GRID_SIZE] = voxels[x, y, z] ? 1 : 0;

        // Create and populate color buffer (RGBA packed as uint)
        uint[] colorData = new uint[VOXEL_GRID_SIZE * VOXEL_GRID_SIZE * VOXEL_GRID_SIZE];
        for (int x = 0; x < VOXEL_GRID_SIZE; x++)
        for (int y = 0; y < VOXEL_GRID_SIZE; y++)
        for (int z = 0; z < VOXEL_GRID_SIZE; z++)
            colorData[x + y * VOXEL_GRID_SIZE + z * VOXEL_GRID_SIZE * VOXEL_GRID_SIZE] = 
                (uint)(voxelColors[x, y, z].R << 24 | voxelColors[x, y, z].G << 16 | voxelColors[x, y, z].B << 8 | voxelColors[x, y, z].A);
        
        unsafe
        {
            fixed (int* voxelPtr = voxelData)
            fixed (uint* colorPtr = colorData)
            {
                voxelBuffer = Rlgl.LoadShaderBuffer((uint)(voxelData.Length * sizeof(int)), voxelPtr, Rlgl.STATIC_DRAW);
                colorBuffer = Rlgl.LoadShaderBuffer((uint)(colorData.Length * sizeof(uint)), colorPtr, Rlgl.STATIC_DRAW);
            }
        }
    }

    private void InitializeVoxels()
    {
        for (int x = 0; x < VOXEL_GRID_SIZE; x++)
        for (int y = 0; y < VOXEL_GRID_SIZE; y++)
        for (int z = 0; z < VOXEL_GRID_SIZE; z++)
        {
            Vector3 pos = new Vector3(x - VOXEL_GRID_SIZE / 2, y - VOXEL_GRID_SIZE / 2, z - VOXEL_GRID_SIZE / 2);
            if (pos.Length() < VOXEL_GRID_SIZE / 3)
            {
                voxels[x, y, z] = true;
                voxelColors[x, y, z] = new Color(
                    (byte)(x * 255 / VOXEL_GRID_SIZE),
                    (byte)(y * 255 / VOXEL_GRID_SIZE),
                    (byte)(z * 255 / VOXEL_GRID_SIZE),
                    ( byte)255
                );
            }
        }
    }

    public void Render(Vector3 cameraPos, RenderTexture2D target)
    {
        Raylib.BeginShaderMode(computeShader);

        // Set shader uniforms
        Raylib.SetShaderValue(computeShader, Raylib.GetShaderLocation(computeShader, "cameraPos"), cameraPos, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(computeShader, Raylib.GetShaderLocation(computeShader, "lightDir"), lightDir, ShaderUniformDataType.Vec3);
        Raylib.SetShaderValue(computeShader, Raylib.GetShaderLocation(computeShader, "skyColor"), 
            new Vector4(skyColor.R / 255f, skyColor.G / 255f, skyColor.B / 255f, skyColor.A / 255f), ShaderUniformDataType.Vec4);
        Raylib.SetShaderValue(computeShader, Raylib.GetShaderLocation(computeShader, "lightColor"), 
            new Vector4(lightColor.R / 255f, lightColor.G / 255f, lightColor.B / 255f, lightColor.A / 255f), ShaderUniformDataType.Vec4);
        Raylib.SetShaderValue(computeShader, Raylib.GetShaderLocation(computeShader, "voxelSize"), VOXEL_SIZE, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(computeShader, Raylib.GetShaderLocation(computeShader, "gridSize"), VOXEL_GRID_SIZE, ShaderUniformDataType.Int);

        // Bind buffers and texture
        Rlgl.BindShaderBuffer((uint)voxelBuffer, 0);
        Rlgl.BindShaderBuffer((uint)colorBuffer, 1);
        Rlgl.BindImageTexture(outputTexture.Texture.Id, 0, 0, false);
        
        // Dispatch compute shader
        Rlgl.ComputeShaderDispatch((uint)Engine.VirtualScreenWidth / 16, (uint)Engine.VirtualScreenHeight / 16, 1);

        Raylib.EndShaderMode();
    }

    public RenderTexture2D GetOutputTexture() => outputTexture;

    public void Unload()
    {
        Raylib.UnloadShader(computeShader);
        Raylib.UnloadRenderTexture(outputTexture);
        //Raylib.UnloadStorageBuffer(voxelBuffer);
        //Raylib.UnloadStorageBuffer(colorBuffer);
    }
}