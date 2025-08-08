using Raylib_cs;
using DIBBLES.Systems;
using System.Numerics;
using System.Collections.Generic;
using DIBBLES.Utils;

namespace DIBBLES.Systems;

public class TerrainGeneration
{
    public const int RenderDistance = 16;
    public const int ChunkSize = 16;
    public const int ChunkHeight = 32;
    
    private Dictionary<Vector3, Chunk> chunks = new Dictionary<Vector3, Chunk>();
    
    private FastNoiseLite noise = new FastNoiseLite();
    private Vector3 lastCameraChunk = Vector3.Zero;

    public void Start()
    {
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(0.02f);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(4);
        noise.SetFractalLacunarity(2.0f);
        noise.SetFractalGain(0.5f);
    }
    
    public void UpdateTerrain(Vector3 cameraPosition)
    {
        // Calculate current chunk coordinates based on camera position
        var currentChunk = new Vector3(
            (int)Math.Floor(cameraPosition.X / ChunkSize),
            0f,
            (int)Math.Floor(cameraPosition.Z / ChunkSize)
        );
        
        // Only update if the camera has moved to a new chunk
        if (currentChunk != lastCameraChunk)
        {
            lastCameraChunk = currentChunk;
            GenerateTerrain(currentChunk);
            UnloadDistantChunks(currentChunk);
        }
    }
    
    private void GenerateTerrain(Vector3 centerChunk)
    {
        int halfRenderDistance = RenderDistance / 2;
        List<Vector3> chunksToGenerate = new List<Vector3>();
        
        for (int cx = (int)centerChunk.X - halfRenderDistance; cx <= centerChunk.X + halfRenderDistance; cx++)
        {
            for (int cz = (int)centerChunk.Z - halfRenderDistance; cz <= centerChunk.Z + halfRenderDistance; cz++)
            {
                Vector3 chunkPos = new Vector3(cx * ChunkSize, 0f, cz * ChunkSize);
                
                if (!chunks.ContainsKey(chunkPos))
                {
                    chunksToGenerate.Add(chunkPos);
                }
            }
        }
    
        foreach (var pos in chunksToGenerate)
        {
            var chunk = new Chunk(pos);
            GenerateChunkData(chunk);
            
            chunks[pos] = chunk;
        
            chunk.Model = generateChunkMesh(chunk);
        }
    }
    
    private void GenerateChunkData(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                float height = noise.GetNoise(chunk.Position.X + x, chunk.Position.Z + z) * 0.5f + 0.5f;
                int terrainHeight = (int)(height * (ChunkHeight - 10)) + 10;

                for (int y = 0; y < ChunkHeight; y++)
                {
                    var block = new Block();
                    block.Type = BlockType.Dirt;
                    block.Position = new Vector3(x, y, z);
                    
                    chunk.VoxelData[x, y, z] = (byte)(y < terrainHeight ? 1 : 0);
                }
            }
        }
    }
    
    private void UnloadDistantChunks(Vector3 centerChunk)
    {
        List<Vector3> chunksToRemove = new List<Vector3>();

        foreach (var chunk in chunks)
        {
            // Convert world-space key to chunk coordinates
            int chunkX = (int)Math.Floor(chunk.Key.X / ChunkSize);
            int chunkZ = (int)Math.Floor(chunk.Key.Z / ChunkSize);
            int centerX = (int)centerChunk.X;
            int centerZ = (int)centerChunk.Z;

            int dx = Math.Abs(chunkX - centerX);
            int dz = Math.Abs(chunkZ - centerZ);
        
            if (dx > RenderDistance / 2 || dz > RenderDistance / 2)
            {
                chunksToRemove.Add(chunk.Key);
            }
        }

        foreach (var coord in chunksToRemove)
        {
            Raylib.UnloadModel(chunks[coord].Model); // Unload the model to free memory
            chunks.Remove(coord);
        }
    }
    
    private Model generateChunkMesh(Chunk chunk)
    {
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Color> colors = [];
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (chunk.VoxelData[x, y, z] == 0) continue;

                    var pos = new Vector3(x, y, z);
                    var color = Raylib.ColorLerp(Color.Green, Color.Brown, (float)y / ChunkHeight);
                    
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
                    if (!isVoxelSolid(chunk, x, y, z - 1))
                    {
                        vertices.AddRange([cubeVertices[0], cubeVertices[3], cubeVertices[2], cubeVertices[1]]);
                        normals.AddRange([new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1)]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 2, vertexOffset + 3, vertexOffset, vertexOffset + 1, vertexOffset + 2]);
                        vertexOffset += 4;
                    }

                    // Back face (+Z)
                    if (!isVoxelSolid(chunk, x, y, z + 1))
                    {
                        vertices.AddRange([cubeVertices[5], cubeVertices[6], cubeVertices[7], cubeVertices[4]]);
                        normals.AddRange([new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1)]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Left face (-X)
                    if (!isVoxelSolid(chunk, x - 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[7], cubeVertices[3], cubeVertices[0]]);
                        normals.AddRange([new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0)]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Right face (+X)
                    if (!isVoxelSolid(chunk, x + 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[1], cubeVertices[2], cubeVertices[6], cubeVertices[5]]);
                        normals.AddRange([new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Bottom face (-Y)
                    if (!isVoxelSolid(chunk, x, y - 1, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[0], cubeVertices[1], cubeVertices[5]]);
                        normals.AddRange([new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0)]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Top face (+Y)
                    if (!isVoxelSolid(chunk, x, y + 1, z))
                    {
                        vertices.AddRange([cubeVertices[3], cubeVertices[7], cubeVertices[6], cubeVertices[2]]);
                        normals.AddRange([new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0)]);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
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
    
    public Dictionary<Vector3, Chunk> GetChunks()
    {
        return chunks;
    }
    
    private bool isVoxelSolid(Chunk chunk, int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkHeight || z < 0 || z >= ChunkSize)
        {
            // Calculate the current chunk's coordinates from its Position
            Vector3 chunkCoord = new Vector3(
                (int)(chunk.Position.X / ChunkSize),
                0f,
                (int)(chunk.Position.Z / ChunkSize)
            );

            // Adjust chunk coordinates based on out-of-bounds voxel
            Vector3 neighborCoord = chunkCoord;
            int nx = x, nz = z;

            if (x < 0) { nx = ChunkSize - 1; neighborCoord.X -= 1; }
            else if (x >= ChunkSize) { nx = 0; neighborCoord.X += 1; }

            if (z < 0) { nz = ChunkSize - 1; neighborCoord.Z -= 1; }
            else if (z >= ChunkSize) { nz = 0; neighborCoord.Z += 1; }

            if (y < 0 || y >= ChunkHeight) return false;

            // Look up the neighboring chunk
            if (chunks.TryGetValue(neighborCoord, out var neighborChunk))
            {
                return neighborChunk.VoxelData[nx, y, nz] == 1;
            }

            return false;
        }

        return chunk.VoxelData[x, y, z] == 1;
    }
    
    public Block? GetBlockAt(Vector3 worldPos)
    {
        // Calculate chunk coordinates
        Vector3 chunkCoord = new Vector3(
            (int)Math.Floor(worldPos.X / ChunkSize),
            0f,
            (int)Math.Floor(worldPos.Z / ChunkSize)
        );

        // Calculate local block coordinates
        int localX = (int)(worldPos.X - (chunkCoord.X * ChunkSize));
        int localY = (int)worldPos.Y;
        int localZ = (int)(worldPos.Z - (chunkCoord.Z * ChunkSize));

        // Check if chunk exists and coordinates are valid
        if (chunks.TryGetValue(chunkCoord, out var chunk) &&
            localX >= 0 && localX < ChunkSize &&
            localY >= 0 && localY < ChunkHeight &&
            localZ >= 0 && localZ < ChunkSize)
        {
            if (chunk.VoxelData[localX, localY, localZ] == 1)
            {
                return new Block
                {
                    Type = BlockType.Dirt,
                    Position = new Vector3(localX, localY, localZ)
                };
            }
        }
    
        return null;
    }
    
    public void Draw()
    {
        foreach (var chunk in chunks.Values)
        {
            Raylib.DrawModel(chunk.Model, chunk.Position, 1.0f, Color.White);
            
            //Raylib.DrawCubeWires(chunk.Position + new Vector3(ChunkSize / 2f, ChunkHeight / 2f, ChunkSize / 2f),
            //    ChunkSize, ChunkHeight, ChunkSize, Color.Red);
        }
    }
}