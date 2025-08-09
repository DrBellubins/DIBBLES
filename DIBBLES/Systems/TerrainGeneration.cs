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
    //public const float ReachDistance = 5.0f;
    public const float ReachDistance = float.PositiveInfinity;
    
    public readonly Dictionary<Vector3, Chunk> chunks = new Dictionary<Vector3, Chunk>();
    
    private readonly FastNoiseLite noise = new FastNoiseLite();
    private Vector3 lastCameraChunk = Vector3.One; // Needs to != zero for first gen

    public void Start(Camera3D _camera)
    {
        Block.InitializeBlockPrefabs();
        
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
            generateChunkData(chunk);
            generateLighting(chunk);
            
            chunks[pos] = chunk;
        
            chunk.Model = generateChunkMesh(chunk);
        }
    }
    
    private void generateChunkData(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                float height = noise.GetNoise(chunk.Position.X + x, chunk.Position.Z + z) * 0.5f + 0.5f;
                int terrainHeight = (int)(height * (ChunkHeight - 10)) + 10;

                for (int y = 0; y < ChunkHeight; y++)
                {
                    if (y >= terrainHeight) // Air
                    {
                        chunk.Blocks[x, y, z] = new Block(new Vector3(x, y, z), Block.Prefabs[BlockType.Air]);
                    }
                    else if (y == terrainHeight - 1) // Top layer: Grass
                    {
                        chunk.Blocks[x, y, z] = new Block(new Vector3(x, y, z), Block.Prefabs[BlockType.Grass]);
                    }
                    else if (y >= terrainHeight - 3) // Near surface: Dirt
                    {
                        chunk.Blocks[x, y, z] = new Block(new Vector3(x, y, z), Block.Prefabs[BlockType.Dirt]);
                    }
                    else // Deeper: Stone
                    {
                        chunk.Blocks[x, y, z] = new Block(new Vector3(x, y, z), Block.Prefabs[BlockType.Stone]);
                    }
                }
            }
        }
    }

    private void generateLighting(Chunk chunk)
    {
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                // Start at the top of the chunk
                bool isSkyExposed = true; // Assume the top is exposed to sky
                float currentLightLevel = 1.0f; // Maximum light level (full sunlight)

                for (int y = ChunkHeight - 1; y >= 0; y--)
                {
                    var block = chunk.Blocks[x, y, z];
                    
                    if (block.Info.Type == BlockType.Air)
                    {
                        // Air blocks get full light if exposed to sky
                        block.LightLevel = isSkyExposed ? 1.0f : 0.0f;
                    }
                    else
                    {
                        // Solid block: assign light level and reduce for blocks below
                        block.LightLevel = currentLightLevel;
                        isSkyExposed = false; // Block occludes sunlight
                        currentLightLevel = Math.Max(0.0f, currentLightLevel - 0.2f); // Decrease light level (adjust 0.2f as needed)
                    }
                }
            }
        }
    }
    
    private Model generateChunkMesh(Chunk chunk)
    {
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Vector2> texcoords = [];
        List<Color> colors = [];
        
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (chunk.Blocks[x, y, z]?.Info.Type == BlockType.Air) continue; // Skip air blocks
                    
                    var pos = new Vector3(x, y, z);
                    var blockType = chunk.Blocks[x, y, z].Info.Type;
                    
                    var lightLevel = chunk.Blocks[x, y, z].LightLevel;
                    
                    var color = new Color(
                        255f * lightLevel,
                        255f * lightLevel,
                        255f * lightLevel,
                        255f
                    );
                    
                    int vertexOffset = vertices.Count;

                    // Define cube vertices (8 corners)
                    Vector3[] cubeVertices =
                    [
                        pos + new Vector3(0, 0, 0), pos + new Vector3(1, 0, 0),
                        pos + new Vector3(1, 1, 0), pos + new Vector3(0, 1, 0),
                        pos + new Vector3(0, 0, 1), pos + new Vector3(1, 0, 1),
                        pos + new Vector3(1, 1, 1), pos + new Vector3(0, 1, 1)
                    ];
                    
                    // Get UV coordinates from atlas
                    Vector2[] uvCoords;
                    if (Block.AtlasUVs.TryGetValue(blockType, out var uvRect))
                    {
                        uvCoords = new Vector2[]
                        {
                            new Vector2(uvRect.X, uvRect.Y + uvRect.Height), // Top-left
                            new Vector2(uvRect.X + uvRect.Width, uvRect.Y + uvRect.Height), // Top-right
                            new Vector2(uvRect.X + uvRect.Width, uvRect.Y), // Bottom-right
                            new Vector2(uvRect.X, uvRect.Y) // Bottom-left
                        };
                    }
                    else
                    {
                        // Fallback UVs (e.g., for Air or missing textures)
                        uvCoords = new Vector2[]
                        {
                            new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(1, 0), new Vector2(0, 0)
                        };
                    }
                    
                    // UVs need to be rotated for some reason
                    Vector2[] rotatedUvCoords = new Vector2[]
                    {
                        uvCoords[1], uvCoords[2], uvCoords[3], uvCoords[0] // Rotate 90 degrees CW
                    };
                    
                    // Check each face and add only if not occluded
                    // Front face (-Z)
                    if (!isVoxelSolid(chunk, x, y, z - 1))
                    {
                        vertices.AddRange([cubeVertices[0], cubeVertices[3], cubeVertices[2], cubeVertices[1]]);
                        normals.AddRange([new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(0, 0, -1)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 2, vertexOffset + 3, vertexOffset, vertexOffset + 1, vertexOffset + 2]);
                        vertexOffset += 4;
                    }

                    // Back face (+Z)
                    if (!isVoxelSolid(chunk, x, y, z + 1))
                    {
                        vertices.AddRange([cubeVertices[5], cubeVertices[6], cubeVertices[7], cubeVertices[4]]);
                        normals.AddRange([new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Left face (-X)
                    if (!isVoxelSolid(chunk, x - 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[7], cubeVertices[3], cubeVertices[0]]);
                        normals.AddRange([new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0), new Vector3(-1, 0, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Right face (+X)
                    if (!isVoxelSolid(chunk, x + 1, y, z))
                    {
                        vertices.AddRange([cubeVertices[1], cubeVertices[2], cubeVertices[6], cubeVertices[5]]);
                        normals.AddRange([new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Bottom face (-Y)
                    if (!isVoxelSolid(chunk, x, y - 1, z))
                    {
                        vertices.AddRange([cubeVertices[4], cubeVertices[0], cubeVertices[1], cubeVertices[5]]);
                        normals.AddRange([new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0), new Vector3(0, -1, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
                        colors.AddRange([color, color, color, color]);
                        indices.AddRange([vertexOffset, vertexOffset + 1, vertexOffset + 2, vertexOffset, vertexOffset + 2, vertexOffset + 3]);
                        vertexOffset += 4;
                    }

                    // Top face (+Y)
                    if (!isVoxelSolid(chunk, x, y + 1, z))
                    {
                        vertices.AddRange([cubeVertices[3], cubeVertices[7], cubeVertices[6], cubeVertices[2]]);
                        normals.AddRange([new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0)]);
                        texcoords.AddRange(rotatedUvCoords);
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
            
            mesh.TexCoords = (float*)Raylib.MemAlloc((uint)mesh.VertexCount * 2 * sizeof(float));
            
            for (int i = 0; i < texcoords.Count; i++)
            {
                mesh.TexCoords[i * 2] = texcoords[i].X;
                mesh.TexCoords[i * 2 + 1] = texcoords[i].Y;
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
        
        // Assign texture atlas
        if (Block.TextureAtlas.Id != 0)
        {
            unsafe
            {
                model.Materials[0].Shader = Raylib.LoadMaterialDefault().Shader;
                model.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = Block.TextureAtlas;
            }
        }
        
        return model;
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
                return neighborChunk.Blocks[nx, y, nz]?.Info.Type != BlockType.Air;
            }

            return false;
        }

        return chunk.Blocks[x, y, z]?.Info.Type != BlockType.Air;
    }

    public void BreakBlock(Camera3D camera)
    {
        Ray ray = Raylib.GetScreenToWorldRay(new Vector2(Engine.ScreenWidth / 2f, Engine.ScreenHeight / 2f), camera);
        
        Block? hitBlock = null;
        Vector3? hitPosition = null;
        float closestDistance = float.MaxValue;

        foreach (var chunk in chunks.Values)
        {
            // Create a bounding box for the chunk
            BoundingBox chunkBox = new BoundingBox(
                chunk.Position,
                chunk.Position + new Vector3(ChunkSize, ChunkHeight, ChunkSize)
            );

            if (Raylib.GetRayCollisionBox(ray, chunkBox).Hit)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    for (int y = 0; y < ChunkHeight; y++)
                    {
                        for (int z = 0; z < ChunkSize; z++)
                        {
                            var block = chunk.Blocks[x, y, z];
                            if (block?.Info.Type == BlockType.Air) continue;

                            Vector3 blockPos = chunk.Position + new Vector3(x, y, z);
                            
                            BoundingBox blockBox = new BoundingBox(
                                blockPos,
                                blockPos + Vector3.One
                            );

                            var collision = Raylib.GetRayCollisionBox(ray, blockBox);
                            if (collision.Hit && collision.Distance < closestDistance && collision.Distance <= ReachDistance)
                            {
                                closestDistance = collision.Distance;
                                hitBlock = block;
                                hitPosition = blockPos;
                            }
                        }
                    }
                }
            }
        }

        if (hitBlock != null && hitPosition.HasValue)
        {
            // Calculate chunk coordinates in world space
            Vector3 chunkCoord = new Vector3(
                (int)Math.Floor(hitPosition.Value.X / ChunkSize) * ChunkSize,
                0f,
                (int)Math.Floor(hitPosition.Value.Z / ChunkSize) * ChunkSize
            );

            if (chunks.TryGetValue(chunkCoord, out var chunk))
            {
                // Calculate local block coordinates
                int localX = (int)(hitPosition.Value.X - chunkCoord.X);
                int localY = (int)hitPosition.Value.Y;
                int localZ = (int)(hitPosition.Value.Z - chunkCoord.Z);

                if (localX >= 0 && localX < ChunkSize &&
                    localY >= 0 && localY < ChunkHeight &&
                    localZ >= 0 && localZ < ChunkSize)
                {
                    // Replace block with air
                    chunk.Blocks[localX, localY, localZ] = new Block(new Vector3(localX, localY, localZ), Block.Prefabs[BlockType.Air]);
                    chunk.Info.Modified = true;

                    // Update chunk lighting and mesh
                    generateLighting(chunk);
                    Raylib.UnloadModel(chunk.Model);
                    chunk.Model = generateChunkMesh(chunk);
                }
            }
        }
    }
    
    public void PlaceBlock(BlockType blockType, Camera3D camera)
    {
        Ray ray = Raylib.GetScreenToWorldRay(new Vector2(Engine.ScreenWidth / 2f, Engine.ScreenHeight / 2f), camera);
        
        Block? hitBlock = null;
        Vector3? hitPosition = null;
        Vector3? hitNormal = null;
        float closestDistance = float.MaxValue;

        foreach (var chunk in chunks.Values)
        {
            BoundingBox chunkBox = new BoundingBox(
                chunk.Position,
                chunk.Position + new Vector3(ChunkSize, ChunkHeight, ChunkSize)
            );

            if (Raylib.GetRayCollisionBox(ray, chunkBox).Hit)
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    for (int y = 0; y < ChunkHeight; y++)
                    {
                        for (int z = 0; z < ChunkSize; z++)
                        {
                            var block = chunk.Blocks[x, y, z];
                            if (block?.Info.Type == BlockType.Air) continue;

                            Vector3 blockPos = chunk.Position + new Vector3(x, y, z);
                            BoundingBox blockBox = new BoundingBox(
                                blockPos,
                                blockPos + Vector3.One
                            );

                            var collision = Raylib.GetRayCollisionBox(ray, blockBox);
                            if (collision.Hit && collision.Distance < closestDistance && collision.Distance <= ReachDistance)
                            {
                                closestDistance = collision.Distance;
                                hitBlock = block;
                                hitPosition = blockPos;
                                hitNormal = collision.Normal;
                            }
                        }
                    }
                }
            }
        }

        if (hitBlock != null && hitPosition.HasValue && hitNormal.HasValue)
        {
            // Calculate the position to place the new block
            Vector3 placePos = hitPosition.Value + hitNormal.Value;

            // Calculate chunk coordinates in world space
            Vector3 chunkCoord = new Vector3(
                (int)Math.Floor(hitPosition.Value.X / ChunkSize) * ChunkSize,
                0f,
                (int)Math.Floor(hitPosition.Value.Z / ChunkSize) * ChunkSize
            );

            if (chunks.TryGetValue(chunkCoord, out var chunk))
            {
                // Calculate local block coordinates
                int localX = (int)(placePos.X - chunkCoord.X);
                int localY = (int)placePos.Y;
                int localZ = (int)(placePos.Z - chunkCoord.Z);

                if (localX >= 0 && localX < ChunkSize &&
                    localY >= 0 && localY < ChunkHeight &&
                    localZ >= 0 && localZ < ChunkSize)
                {
                    // Only place if the target position is air
                    if (chunk.Blocks[localX, localY, localZ]?.Info.Type == BlockType.Air)
                    {
                        // Place the new block
                        chunk.Blocks[localX, localY, localZ] = new Block(new Vector3(localX, localY, localZ), Block.Prefabs[blockType]);
                        chunk.Info.Modified = true;

                        // Update chunk lighting and mesh
                        generateLighting(chunk);
                        Raylib.UnloadModel(chunk.Model);
                        chunk.Model = generateChunkMesh(chunk);
                    }
                }
            }
        }
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
            var block = chunk.Blocks[localX, localY, localZ];
            
            if (block?.Info.Type != BlockType.Air)
            {
                return block;
            }
        }
    
        return null;
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
    
    public void Draw()
    {
        foreach (var chunk in chunks.Values)
        {
            Raylib.DrawModel(chunk.Model, chunk.Position, 1.0f, Color.White);
            
            Raylib.DrawCubeWires(chunk.Position + new Vector3(ChunkSize / 2f, ChunkHeight / 2f, ChunkSize / 2f),
                ChunkSize, ChunkHeight, ChunkSize, Color.Blue);
        }
    }
}