using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;

namespace DIBBLES.Scenes;

public class VoxelTerrainScene : Scene
{
    private const int CHUNK_SIZE = 16;
    private const int CHUNK_HEIGHT = 64;
    private readonly byte[,,] _voxelData;
    private readonly FastNoiseLite _noise;
    private Camera3D _camera;
    private readonly Vector3 _cameraOffset = new Vector3(CHUNK_SIZE / 2f, CHUNK_HEIGHT / 2f, CHUNK_SIZE / 2f);
    private const float VOXEL_SCALE = 1.0f;
    private Model _chunkModel;

    public VoxelTerrainScene()
    {
        _voxelData = new byte[CHUNK_SIZE, CHUNK_HEIGHT, CHUNK_SIZE];
        _noise = new FastNoiseLite();
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _noise.SetFrequency(0.02f);
        _noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        _noise.SetFractalOctaves(4);
        _noise.SetFractalLacunarity(2.0f);
        _noise.SetFractalGain(0.5f);

        _camera = new Camera3D
        {
            Position = new Vector3(CHUNK_SIZE / 2f, CHUNK_HEIGHT / 2f, CHUNK_SIZE * 2f),
            Target = new Vector3(CHUNK_SIZE / 2f, CHUNK_HEIGHT / 2f, CHUNK_SIZE / 2f),
            Up = new Vector3(0, 1, 0),
            FovY = 60.0f,
            Projection = CameraProjection.Perspective
        };
    }

    public override void Start()
    {
        GenerateTerrain();
        _chunkModel = GenerateChunkMesh();
    }

    private void GenerateTerrain()
    {
        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int z = 0; z < CHUNK_SIZE; z++)
            {
                float height = _noise.GetNoise(x, z) * 0.5f + 0.5f;
                int terrainHeight = (int)(height * (CHUNK_HEIGHT - 10)) + 10;

                for (int y = 0; y < CHUNK_HEIGHT; y++)
                {
                    if (y < terrainHeight)
                        _voxelData[x, y, z] = 1;
                    else
                        _voxelData[x, y, z] = 0;
                }
            }
        }
    }

    private bool IsVoxelSolid(int x, int y, int z)
    {
        if (x < 0 || x >= CHUNK_SIZE || y < 0 || y >= CHUNK_HEIGHT || z < 0 || z >= CHUNK_SIZE)
            return false;
        return _voxelData[x, y, z] == 1;
    }

    private Model GenerateChunkMesh()
    {
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Color> colors = [];

        for (int x = 0; x < CHUNK_SIZE; x++)
        {
            for (int y = 0; y < CHUNK_HEIGHT; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    if (_voxelData[x, y, z] == 0) continue;

                    Vector3 pos = new Vector3(x, y, z) * VOXEL_SCALE;
                    
                    Color color = Raylib.ColorLerp(Color.Green, Color.Brown, (float)y / CHUNK_HEIGHT);
                    int vertexOffset = vertices.Count;

                    // Define cube vertices (8 corners)
                    Vector3[] cubeVertices =
                    [
                        pos + new Vector3(0, 0, 0), pos + new Vector3(1, 0, 0),
                        pos + new Vector3(1, 1, 0), pos + new Vector3(0, 1, 0),
                        pos + new Vector3(0, 0, 1), pos + new Vector3(1, 0, 1),
                        pos + new Vector3(1, 1, 1), pos + new Vector3(0, 1, 1)
                    ];

                    // Check each face and add only if not occluded
                    // Front face (-Z)
                    if (!IsVoxelSolid(x, y, z - 1))
                    {
                        vertices.AddRange([cubeVertices[0], cubeVertices[3], cubeVertices[2], cubeVertices[1]]);
                        normals.AddRange([new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1)
                        ]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 2, vertexOffset + 3, vertexOffset, vertexOffset + 1, vertexOffset + 2
                        ]);
                        vertexOffset += 4;
                    }

                    // Back face (+Z)
                    if (!IsVoxelSolid(x, y, z + 1))
                    {
                        vertices.AddRange([cubeVertices[5], cubeVertices[6], cubeVertices[7], cubeVertices[4]]);
                        normals.AddRange([new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1)
                        ]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3
                        ]);
                        vertexOffset += 4;
                    }

                    // Left face (-X)
                    if (!IsVoxelSolid(x - 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[7], cubeVertices[3], cubeVertices[0]]);
                        normals.AddRange([new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0)
                        ]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3
                        ]);
                        vertexOffset += 4;
                    }

                    // Right face (+X)
                    if (!IsVoxelSolid(x + 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[1], cubeVertices[2], cubeVertices[6], cubeVertices[5]]);
                        normals.AddRange([new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)
                        ]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3
                        ]);
                        vertexOffset += 4;
                    }

                    // Bottom face (-Y)
                    if (!IsVoxelSolid(x, y - 1, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[0], cubeVertices[1], cubeVertices[5]]);
                        normals.AddRange([new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0)
                        ]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3
                        ]);
                        vertexOffset += 4;
                    }

                    // Top face (+Y)
                    if (!IsVoxelSolid(x, y + 1, z))
                    {
                        vertices.AddRange([cubeVertices[3], cubeVertices[7], cubeVertices[6], cubeVertices[2]]);
                        normals.AddRange([new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0)
                        ]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3
                        ]);
                    }
                }
            }
        }

        // Create mesh
        Mesh mesh = new Mesh
        {
            VertexCount = vertices.Count,
            TriangleCount = indices.Count / 3
        };

        // Upload mesh data
        unsafe
        {
            mesh.Vertices = (float*)Raylib.MemAlloc((uint)mesh.VertexCount * 3 * sizeof(float));
            for (int i = 0; i < vertices.Count; i++)
            {
                mesh.Vertices[i * 3] = vertices[i].X;
                mesh.Vertices[i * 3 + 1] = vertices[i].Y;
                mesh.Vertices[i * 3 + 2] = vertices[i].Z;
            }

            mesh.Normals = (float*)Raylib.MemAlloc((uint)mesh.VertexCount * 3 * sizeof(float));
            for (int i = 0; i < normals.Count; i++)
            {
                mesh.Normals[i * 3] = normals[i].X;
                mesh.Normals[i * 3 + 1] = normals[i].Y;
                mesh.Normals[i * 3 + 2] = normals[i].Z;
            }

            mesh.Colors = (byte*)Raylib.MemAlloc((uint)mesh.VertexCount * 4 * sizeof(byte));
            for (int i = 0; i < colors.Count; i++)
            {
                mesh.Colors[i * 4] = colors[i].R;
                mesh.Colors[i * 4 + 1] = colors[i].G;
                mesh.Colors[i * 4 + 2] = colors[i].B;
                mesh.Colors[i * 4 + 3] = colors[i].A;
            }

            mesh.Indices = (ushort*)Raylib.MemAlloc((uint)indices.Count * sizeof(ushort));
            for (int i = 0; i < indices.Count; i++)
            {
                mesh.Indices[i] = (ushort)indices[i];
            }
            
            Raylib.UploadMesh(&mesh, false);
        }
        
        Model model = Raylib.LoadModelFromMesh(mesh);
        return model;
    }

    public override void Update()
    {
        Raylib.UpdateCamera(ref _camera, CameraMode.FirstPerson);

        float moveSpeed = 20.0f * Time.DeltaTime;
        
        if (Raylib.IsKeyDown(KeyboardKey.W))
            _camera.Position += Vector3.Normalize(_camera.Target - _camera.Position) * moveSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.S))
            _camera.Position -= Vector3.Normalize(_camera.Target - _camera.Position) * moveSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.A))
            _camera.Position += Vector3.Normalize(Vector3.Cross(_camera.Up, _camera.Target - _camera.Position)) * moveSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.D))
            _camera.Position -= Vector3.Normalize(Vector3.Cross(_camera.Up, _camera.Target - _camera.Position)) * moveSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.Space))
            _camera.Position.Y += moveSpeed;
        if (Raylib.IsKeyDown(KeyboardKey.LeftControl))
            _camera.Position.Y -= moveSpeed;

        _camera.Target = _camera.Position - Vector3.Normalize(_camera.Position - _cameraOffset) * 10f;
    }

    public override void Draw()
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(Color.SkyBlue);
        Raylib.BeginMode3D(_camera);

        Raylib.DrawModel(_chunkModel, Vector3.Zero, 1.0f, Color.White);

        Raylib.EndMode3D();
        Raylib.DrawFPS(10, 10);
        Raylib.EndDrawing();
    }
}