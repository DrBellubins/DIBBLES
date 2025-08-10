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
    public const float ReachDistance = 100f; // Has to be finite!
    public const float LightDecay = 1f / 16f;
    
    public readonly Dictionary<Vector3, Chunk> chunks = new Dictionary<Vector3, Chunk>();
    
    private readonly FastNoiseLite noise = new FastNoiseLite();
    private Vector3 lastCameraChunk = Vector3.One; // Needs to != zero for first gen

    public int Seed;
    
    private Vector3 selectedNormal;
    private Block? selectedBlock;
    
    private Shader terrainShader;
    
    private static readonly Vector3[] Directions = 
    {
        new (1, 0, 0), new (-1, 0, 0),
        new (0, 1, 0), new (0, -1, 0),
        new (0, 0, 1), new (0, 0, -1)
    };
    
    private static readonly int[,] FaceVertexOffsets = new int[6, 12]
    {
        // Each row: 4 vertices, each with (x, y, z) offsets
        // Front (-Z)
        { 0,0,0, 0,1,0, 1,1,0, 1,0,0 },
        // Back (+Z)
        { 1,0,1, 1,1,1, 0,1,1, 0,0,1 },
        // Left (-X)
        { 0,0,1, 0,1,1, 0,1,0, 0,0,0 },
        // Right (+X)
        { 1,0,0, 1,1,0, 1,1,1, 1,0,1 },
        // Bottom (-Y)
        { 0,0,1, 1,0,1, 1,0,0, 0,0,0 },
        // Top (+Y)
        { 0,1,0, 1,1,0, 1,1,1, 0,1,1 }
    };
    
    public void Start()
    {
        Block.InitializeBlockPrefabs();
        
        if (WorldSave.Data.Seed != 0)
            Seed = WorldSave.Data.Seed;
        else
            Seed = new Random().Next(int.MaxValue);
        
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetSeed(1337);
        noise.SetFrequency(0.02f);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(4);
        noise.SetFractalLacunarity(2.0f);
        noise.SetFractalGain(0.5f);
        
        terrainShader = Resource.LoadShader("terrain.vs", "terrain.fs");
    }

    public void UpdateTerrain(Camera3D camera)
    {
        // Calculate current chunk coordinates based on camera position
        var currentChunk = new Vector3(
            (int)Math.Floor(camera.Position.X / ChunkSize),
            0f,
            (int)Math.Floor(camera.Position.Z / ChunkSize)
        );
        
        // Only update if the camera has moved to a new chunk
        if (currentChunk != lastCameraChunk)
        {
            lastCameraChunk = currentChunk;
            GenerateTerrain(currentChunk);
            UnloadDistantChunks(currentChunk);
        }
    }
    
    public void UpdateMovement(Camera3D camera)
    {
        var (block, normal) = selectBlock(camera);
        selectedBlock = block;
        selectedNormal = normal;
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
    
        // Generate chunk data
        foreach (var pos in chunksToGenerate)
        {
            var chunk = new Chunk(pos);
            generateChunkData(chunk);
            chunks[pos] = chunk;
        }

        // Generate lighting for all chunks
        foreach (var pos in chunksToGenerate)
        {
            var chunk = chunks[pos];
            //generateLighting(chunk);
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
                    var worldPos = chunk.Position + new Vector3(x, y, z);
                    
                    if (y >= terrainHeight) // Air
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Air]);
                    }
                    else if (y == terrainHeight - 1) // Top layer: Grass
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Grass]);
                    }
                    else if (y >= terrainHeight - 3) // Near surface: Dirt
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Dirt]);
                    }
                    else // Deeper: Stone
                    {
                        chunk.Blocks[x, y, z] = new Block(worldPos, Block.Prefabs[BlockType.Stone]);
                    }
                }
            }
        }
    }
    
    private void generateLighting(Chunk chunk)
    {
        // 1. Reset all light levels
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkHeight; y++)
        for (int z = 0; z < ChunkSize; z++)
            chunk.Blocks[x, y, z].LightLevel = 0f;
    
        // 2. Sky-exposed air blocks get full light, enqueue for flood fill
        Queue<(Vector3 worldPos, float level)> queue = new();
        
        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
        {
            bool foundSolid = false;
            
            for (int y = ChunkHeight - 1; y >= 0; y--)
            {
                var block = chunk.Blocks[x, y, z];
                
                if (!foundSolid && block.Info.Type == BlockType.Air)
                {
                    block.LightLevel = 1f;
                    queue.Enqueue((chunk.Position + new Vector3(x, y, z), 1f));
                }
                else if (block.Info.Type != BlockType.Air)
                    foundSolid = true;
            }
        }
    
        // 3. Flood fill light through air blocks (across chunks)
        while (queue.Count > 0)
        {
            var (worldPos, level) = queue.Dequeue();
            float nextLevel = level - LightDecay;
            
            if (nextLevel <= 0f) continue;
    
            foreach (var dir in Directions)
            {
                var neighborPos = worldPos + dir;
                
                if (neighborPos.Y < 0 || neighborPos.Y >= ChunkHeight) continue;
                if (!TryGetBlock(neighborPos, out var neighborBlock)) continue;
                
                if (neighborBlock.Info.Type == BlockType.Air && nextLevel > neighborBlock.LightLevel)
                {
                    neighborBlock.LightLevel = nextLevel;
                    queue.Enqueue((neighborPos, nextLevel));
                }
            }
        }
    
        // 4. Propagate light into solid blocks (multiple passes for falloff)
        for (int pass = 0; pass < 8; pass++)
        {
            for (int x = 0; x < ChunkSize; x++)
            for (int y = 0; y < ChunkHeight; y++)
            for (int z = 0; z < ChunkSize; z++)
            {
                var block = chunk.Blocks[x, y, z];
                
                if (block.Info.Type == BlockType.Air) continue;
    
                float maxLight = 0f;
                
                foreach (var dir in Directions)
                {
                    int nx = x + (int)dir.X, ny = y + (int)dir.Y, nz = z + (int)dir.Z;
                    if (nx < 0 || nx >= ChunkSize || ny < 0 || ny >= ChunkHeight || nz < 0 || nz >= ChunkSize) continue;
                    var neighbor = chunk.Blocks[nx, ny, nz];
                    maxLight = Math.Max(maxLight, neighbor.LightLevel - LightDecay);
                }
                
                block.LightLevel = Math.Max(block.LightLevel, maxLight);
            }
        }
    }
    
    private float getLight(Chunk chunk, int x, int y, int z)
    {
        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkHeight || z < 0 || z >= ChunkSize)
        {
            // Try to sample from neighbor chunk, or return 1.0f if above the chunk (sky)
            if (y >= ChunkHeight) return 1.0f;
            
            return 0f;
        }
        
        return chunk.Blocks[x, y, z].LightLevel;
    }

    // Helper to get a block at world position (across chunks)
    private bool TryGetBlock(Vector3 worldPos, out Block block)
    {
        var chunkCoord = new Vector3(
            (int)Math.Floor(worldPos.X / ChunkSize) * ChunkSize,
            0f,
            (int)Math.Floor(worldPos.Z / ChunkSize) * ChunkSize
        );
        
        if (chunks.TryGetValue(chunkCoord, out var chunk))
        {
            var local = worldPos - chunkCoord;
            int lx = (int)local.X, ly = (int)local.Y, lz = (int)local.Z;
            
            if (lx >= 0 && lx < ChunkSize && ly >= 0 && ly < ChunkHeight && lz >= 0 && lz < ChunkSize)
            {
                block = chunk.Blocks[lx, ly, lz];
                return true;
            }
        }

        block = null!;
        return false;
    }

    private List<Color> GetSmoothFaceVertexColors(Chunk chunk, int x, int y, int z, int face)
    {
        var colors = new List<Color>(4);
    
        for (int v = 0; v < 4; v++)
        {
            int vx = x + FaceVertexOffsets[face, v * 3 + 0];
            int vy = y + FaceVertexOffsets[face, v * 3 + 1];
            int vz = z + FaceVertexOffsets[face, v * 3 + 2];
    
            // Average light from 8 surrounding voxels
            float light =
                (getLight(chunk, vx,     vy,     vz) +
                 getLight(chunk, vx - 1, vy,     vz) +
                 getLight(chunk, vx,     vy - 1, vz) +
                 getLight(chunk, vx,     vy,     vz - 1) +
                 getLight(chunk, vx - 1, vy - 1, vz) +
                 getLight(chunk, vx - 1, vy,     vz - 1) +
                 getLight(chunk, vx,     vy - 1, vz - 1) +
                 getLight(chunk, vx - 1, vy - 1, vz - 1)
                ) / 8f;
    
            //Console.WriteLine($"Light: {light} at ({vx}, {vy}, {vz})");
            
            byte lightByte = (byte)Math.Clamp(light * 255f, 0, 255);
            colors.Add(new Color(lightByte, lightByte, lightByte, (byte)255));
            //colors.Add(Color.White);
        }
    
        return colors;
    }
    
    private Model generateChunkMesh(Chunk chunk)
    {
        List<Vector3> vertices = new();
        List<int> indices = new();
        List<Vector3> normals = new();
        List<Vector2> texcoords = new();
        List<Color> colors = new();
    
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    if (chunk.Blocks[x, y, z]?.Info.Type == BlockType.Air) continue;
    
                    var pos = new Vector3(x, y, z);
                    var blockType = chunk.Blocks[x, y, z].Info.Type;
    
                    // Define cube vertices (8 corners)
                    Vector3[] cubeVertices =
                    {
                        pos + new Vector3(0, 0, 0), pos + new Vector3(1, 0, 0),
                        pos + new Vector3(1, 1, 0), pos + new Vector3(0, 1, 0),
                        pos + new Vector3(0, 0, 1), pos + new Vector3(1, 0, 1),
                        pos + new Vector3(1, 1, 1), pos + new Vector3(0, 1, 1)
                    };
    
                    // Get UV coordinates from atlas
                    Vector2[] uvCoords;
    
                    if (Block.AtlasUVs.TryGetValue(blockType, out var uvRect))
                    {
                        uvCoords = new Vector2[]
                        {
                            new Vector2(uvRect.X, uvRect.Y + uvRect.Height),
                            new Vector2(uvRect.X + uvRect.Width, uvRect.Y + uvRect.Height),
                            new Vector2(uvRect.X + uvRect.Width, uvRect.Y),
                            new Vector2(uvRect.X, uvRect.Y)
                        };
                    }
                    else
                    {
                        uvCoords = new Vector2[]
                        {
                            new Vector2(0, 1), new Vector2(1, 1),
                            new Vector2(1, 0), new Vector2(0, 0)
                        };
                    }
    
                    // Check each face and add only if not occluded
                    for (int face = 0; face < 6; face++)
                    {
                        if (!isVoxelSolid(chunk, x + (int)Directions[face].X, y + (int)Directions[face].Y, z + (int)Directions[face].Z))
                        {
                            int vertexOffset = vertices.Count;
                            
                            vertices.AddRange(new[]
                            {
                                cubeVertices[FaceVertexOffsets[face, 0]],
                                cubeVertices[FaceVertexOffsets[face, 1]],
                                cubeVertices[FaceVertexOffsets[face, 2]],
                                cubeVertices[FaceVertexOffsets[face, 3]]
                            });
    
                            normals.AddRange(new[]
                            {
                                Directions[face], Directions[face],
                                Directions[face], Directions[face]
                            });
    
                            texcoords.AddRange(uvCoords);
    
                            // Use smooth lighting for vertex colors
                            var faceColors = GetSmoothFaceVertexColors(chunk, x, y, z, face);
                            colors.AddRange(faceColors);
    
                            indices.AddRange(new[]
                            {
                                vertexOffset, vertexOffset + 2, vertexOffset + 1,
                                vertexOffset, vertexOffset + 3, vertexOffset + 2
                            });
                        }
                    }
                }
            }
        }
    
        // Create and upload the mesh (same as your existing code)
        Mesh mesh = new Mesh
        {
            VertexCount = vertices.Count,
            TriangleCount = indices.Count / 3
        };
    
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
    
        if (Block.TextureAtlas.Id != 0)
        {
            unsafe
            {
                model.Materials[0].Shader = Raylib.LoadMaterialDefault().Shader;
                //model.Materials[0].Shader = terrainShader;
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
    
    private (Block?, Vector3) selectBlock(Camera3D camera)
    {
        var rayPosition = camera.Position;
        var rayDirection = Vector3.Normalize(camera.Target - camera.Position);
    
        var mapPos = new Vector3(
            MathF.Floor(rayPosition.X),
            MathF.Floor(rayPosition.Y),
            MathF.Floor(rayPosition.Z)
        );
    
        // Handle near-zero components to avoid div-by-zero and precision issues
        const float epsilon = 1e-6f;
        
        var deltaDist = new Vector3(
            Math.Abs(rayDirection.X) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.X),
            Math.Abs(rayDirection.Y) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.Y),
            Math.Abs(rayDirection.Z) < epsilon ? float.PositiveInfinity : Math.Abs(1f / rayDirection.Z)
        );
    
        var step = new Vector3(
            rayDirection.X > 0 ? 1f : (rayDirection.X < 0 ? -1f : 0f),
            rayDirection.Y > 0 ? 1f : (rayDirection.Y < 0 ? -1f : 0f),
            rayDirection.Z > 0 ? 1f : (rayDirection.Z < 0 ? -1f : 0f)
        );
    
        var sideDist = new Vector3(
            rayDirection.X == 0 ? float.PositiveInfinity : (rayDirection.X > 0 ? (mapPos.X + 1f - rayPosition.X) * deltaDist.X : (rayPosition.X - mapPos.X) * deltaDist.X),
            rayDirection.Y == 0 ? float.PositiveInfinity : (rayDirection.Y > 0 ? (mapPos.Y + 1f - rayPosition.Y) * deltaDist.Y : (rayPosition.Y - mapPos.Y) * deltaDist.Y),
            rayDirection.Z == 0 ? float.PositiveInfinity : (rayDirection.Z > 0 ? (mapPos.Z + 1f - rayPosition.Z) * deltaDist.Z : (rayPosition.Z - mapPos.Z) * deltaDist.Z)
        );
    
        // Optional small nudge to avoid boundary ambiguities
        const float nudge = 1e-5f;
        
        if (sideDist.X < nudge) sideDist.X += nudge;
        if (sideDist.Y < nudge) sideDist.Y += nudge;
        if (sideDist.Z < nudge) sideDist.Z += nudge;
    
        Block? hitBlock = null;
        Vector3 hitNormal = Vector3.Zero;
    
        // Check starting voxel first
        var startChunkPos = new Vector3(
            MathF.Floor(mapPos.X / ChunkSize) * ChunkSize,
            0f,
            MathF.Floor(mapPos.Z / ChunkSize) * ChunkSize
        );
        
        if (chunks.TryGetValue(startChunkPos, out var startChunk))
        {
            var localX = (int)(mapPos.X - startChunkPos.X);
            var localY = (int)mapPos.Y;
            var localZ = (int)(mapPos.Z - startChunkPos.Z);
            
            if (localX >= 0 && localX < ChunkSize && localY >= 0 && localY < ChunkHeight && localZ >= 0 && localZ < ChunkSize)
            {
                var block = startChunk.Blocks[localX, localY, localZ];
                
                if (block.Info.Type != BlockType.Air)
                {
                    hitBlock = block;
                    return (hitBlock, hitNormal); // Normal arbitrary for starting block hit; could compute based on direction or set to zero
                }
            }
        }
    
        // DDA loop
        const int maxSteps = 500; // Adjust based on ReachDistance
        int steps = 0;
        
        while (steps < maxSteps)
        {
            steps++;
    
            float nextT;
            Vector3 stepAxis = Vector3.Zero;
    
            if (sideDist.X <= sideDist.Y && sideDist.X <= sideDist.Z)
            {
                nextT = sideDist.X;
                sideDist.X += deltaDist.X;
                mapPos.X += step.X;
                hitNormal = new Vector3(-step.X, 0, 0);
            }
            else if (sideDist.Y <= sideDist.Z)
            {
                nextT = sideDist.Y;
                sideDist.Y += deltaDist.Y;
                mapPos.Y += step.Y;
                hitNormal = new Vector3(0, -step.Y, 0);
            }
            else
            {
                nextT = sideDist.Z;
                sideDist.Z += deltaDist.Z;
                mapPos.Z += step.Z;
                hitNormal = new Vector3(0, 0, -step.Z);
            }
    
            if (nextT > ReachDistance) break;
    
            // Check current voxel
            var currentChunkPos = new Vector3(
                MathF.Floor(mapPos.X / ChunkSize) * ChunkSize,
                0f,
                MathF.Floor(mapPos.Z / ChunkSize) * ChunkSize
            );
    
            if (!chunks.TryGetValue(currentChunkPos, out var chunk)) continue;
    
            var localX = (int)(mapPos.X - currentChunkPos.X);
            var localY = (int)mapPos.Y;
            var localZ = (int)(mapPos.Z - currentChunkPos.Z);
    
            if (localX < 0 || localX >= ChunkSize || localY < 0 || localY >= ChunkHeight || localZ < 0 || localZ >= ChunkSize) continue;
    
            var block = chunk.Blocks[localX, localY, localZ];
    
            if (block.Info.Type != BlockType.Air)
            {
                hitBlock = block;
                break;
            }
        }
    
        return (hitBlock, hitNormal);
    }
    
    public void BreakBlock()
    {
        if (selectedBlock == null)
            return;

        // Get the chunk containing the selected block
        var blockPos = selectedBlock.Position;
        var chunkCoord = new Vector3(
            (int)Math.Floor(blockPos.X / ChunkSize) * ChunkSize,
            0f,
            (int)Math.Floor(blockPos.Z / ChunkSize) * ChunkSize
        );

        if (!chunks.TryGetValue(chunkCoord, out var chunk))
            return;

        // Calculate local block coordinates within the chunk
        var localPos = blockPos - chunkCoord;
        var localX = (int)localPos.X;
        var localY = (int)localPos.Y;
        var localZ = (int)localPos.Z;

        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkHeight ||
            localZ < 0 || localZ >= ChunkSize)
            return;

        // Set block to Air
        chunk.Blocks[localX, localY, localZ] = new Block(blockPos, Block.Prefabs[BlockType.Air]);
        chunk.Info.Modified = true;

        // Regenerate lighting and mesh
        generateLighting(chunk);
        Raylib.UnloadModel(chunk.Model); // Unload old model
        chunk.Model = generateChunkMesh(chunk);

        // Add to modified chunks for saving
        if (!WorldSave.Data.ModifiedChunks.Any(c => c.Position == chunk.Position))
            WorldSave.Data.ModifiedChunks.Add(chunk);

        // Play break sound
        var sound = Block.Sounds[selectedBlock.Info.Type].RND;
        
        if (sound.FrameCount != 0)
            Raylib.PlaySound(sound);
    }
    
    public void PlaceBlock(BlockType blockType)
    {
        if (selectedBlock == null || blockType == BlockType.Air)
            return;

        // Quantize the normal to the nearest axis-aligned direction
        Vector3 normal = selectedNormal;
        
        Vector3 quantizedNormal = new Vector3(
            Math.Abs(normal.X) > Math.Abs(normal.Y) && Math.Abs(normal.X) > Math.Abs(normal.Z) ? Math.Sign(normal.X) : 0,
            Math.Abs(normal.Y) > Math.Abs(normal.X) && Math.Abs(normal.Y) > Math.Abs(normal.Z) ? Math.Sign(normal.Y) : 0,
            Math.Abs(normal.Z) > Math.Abs(normal.X) && Math.Abs(normal.Z) > Math.Abs(normal.Y) ? Math.Sign(normal.Z) : 0
        );
        
        // Calculate the position to place the new block
        var newBlockPos = selectedBlock.Position + quantizedNormal;
        
        // Determine the chunk for the new block position
        var chunkCoord = new Vector3(
            (int)Math.Floor(newBlockPos.X / ChunkSize) * ChunkSize,
            0f,
            (int)Math.Floor(newBlockPos.Z / ChunkSize) * ChunkSize
        );
        
        // Create chunk if it doesn't exist
        if (!chunks.TryGetValue(chunkCoord, out var chunk))
        {
            chunk = new Chunk(chunkCoord);
            generateChunkData(chunk);
            generateLighting(chunk);
            chunks[chunkCoord] = chunk;
        }
        
        // Calculate local block coordinates within the chunk
        var localPos = newBlockPos - chunkCoord;
        var localX = (int)localPos.X;
        var localY = (int)localPos.Y;
        var localZ = (int)localPos.Z;
        
        // Check if the position is within bounds and not occupied
        if (localX < 0 || localX >= ChunkSize ||
            localY < 0 || localY >= ChunkHeight ||
            localZ < 0 || localZ >= ChunkSize ||
            chunk.Blocks[localX, localY, localZ]?.Info.Type != BlockType.Air)
            return;
        
        // Place the new block
        chunk.Blocks[localX, localY, localZ] = new Block(newBlockPos, Block.Prefabs[blockType]);
        chunk.Info.Modified = true;
        
        // Regenerate lighting and mesh
        generateLighting(chunk);
        Raylib.UnloadModel(chunk.Model); // Unload old model
        chunk.Model = generateChunkMesh(chunk);
        
        // Add to modified chunks for saving
        if (!WorldSave.Data.ModifiedChunks.Any(c => c.Position == chunk.Position))
            WorldSave.Data.ModifiedChunks.Add(chunk);
        
        // Play place sound
        var sound = Block.Sounds[selectedBlock.Info.Type].RND;
        
        if (sound.FrameCount != 0)
            Raylib.PlaySound(sound);
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
            
            var debugColor = Color.Black;
            
            if (chunk.Info.Modified)
                debugColor = Color.Red;
            else
                debugColor = Color.Blue;
            
            if (selectedBlock != null)
                Raylib.DrawCubeWires(selectedBlock.Position + new Vector3(0.5f, 0.5f, 0.5f), 1f, 1f, 1f, Color.Black);
            
            Raylib.DrawCubeWires(chunk.Position + new Vector3(ChunkSize / 2f, ChunkHeight / 2f, ChunkSize / 2f),
                ChunkSize, ChunkHeight, ChunkSize, debugColor);
        }
    }
}