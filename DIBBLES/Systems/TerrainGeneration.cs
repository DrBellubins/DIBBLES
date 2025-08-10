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
    
    public readonly Dictionary<Vector3, Chunk> chunks = new Dictionary<Vector3, Chunk>();
    
    private readonly FastNoiseLite noise = new FastNoiseLite();
    private Vector3 lastCameraChunk = Vector3.One; // Needs to != zero for first gen

    public int Seed;
    
    private Vector3 selectedNormal;
    private Block? selectedBlock;
    
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
        // Initialize sky lighting - propagate from top down
        initializeSkyLighting(chunk);
        
        // Initialize block lighting from emissive blocks
        initializeBlockLighting(chunk);
        
        // Propagate lighting using BFS
        propagateLighting(chunk);
    }
    
    private void initializeSkyLighting(Chunk chunk)
    {
        // Initialize sky light from the top of the world down
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                byte skyLight = 15; // Maximum sky light at the top
                
                for (int y = ChunkHeight - 1; y >= 0; y--)
                {
                    var block = chunk.Blocks[x, y, z];
                    
                    if (block.Info.Type == BlockType.Air || block.Info.IsTransparent)
                    {
                        block.SkyLight = skyLight;
                    }
                    else
                    {
                        block.SkyLight = 0;
                        skyLight = 0; // Block the light from going further down
                    }
                }
            }
        }
    }
    
    private void initializeBlockLighting(Chunk chunk)
    {
        // Initialize block light from emissive blocks
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    var block = chunk.Blocks[x, y, z];
                    block.BlockLight = block.Info.LightEmission;
                }
            }
        }
    }
    
    private void propagateLighting(Chunk chunk)
    {
        // Use BFS to propagate both sky and block light
        Queue<Vector3Int> lightQueue = new Queue<Vector3Int>();
        
        // Add all blocks with light to the queue for propagation
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                for (int z = 0; z < ChunkSize; z++)
                {
                    var block = chunk.Blocks[x, y, z];
                    if (block.SkyLight > 0 || block.BlockLight > 0)
                    {
                        lightQueue.Enqueue(new Vector3Int(x, y, z));
                    }
                }
            }
        }
        
        // Process light propagation
        while (lightQueue.Count > 0)
        {
            var pos = lightQueue.Dequeue();
            var block = chunk.Blocks[pos.X, pos.Y, pos.Z];
            
            // Check all 6 neighboring positions
            Vector3Int[] neighbors = {
                new Vector3Int(pos.X - 1, pos.Y, pos.Z),
                new Vector3Int(pos.X + 1, pos.Y, pos.Z),
                new Vector3Int(pos.X, pos.Y - 1, pos.Z),
                new Vector3Int(pos.X, pos.Y + 1, pos.Z),
                new Vector3Int(pos.X, pos.Y, pos.Z - 1),
                new Vector3Int(pos.X, pos.Y, pos.Z + 1)
            };
            
            foreach (var neighborPos in neighbors)
            {
                // Skip if outside chunk bounds (we'll handle cross-chunk later)
                if (neighborPos.X < 0 || neighborPos.X >= ChunkSize ||
                    neighborPos.Y < 0 || neighborPos.Y >= ChunkHeight ||
                    neighborPos.Z < 0 || neighborPos.Z >= ChunkSize)
                    continue;
                
                var neighbor = chunk.Blocks[neighborPos.X, neighborPos.Y, neighborPos.Z];
                
                // Skip if neighbor is opaque
                if (!neighbor.Info.IsTransparent && neighbor.Info.Type != BlockType.Air)
                    continue;
                
                bool updated = false;
                
                // Propagate sky light
                if (block.SkyLight > 1)
                {
                    byte newSkyLight = (byte)(block.SkyLight - 1);
                    if (newSkyLight > neighbor.SkyLight)
                    {
                        neighbor.SkyLight = newSkyLight;
                        updated = true;
                    }
                }
                
                // Propagate block light
                if (block.BlockLight > 1)
                {
                    byte newBlockLight = (byte)(block.BlockLight - 1);
                    if (newBlockLight > neighbor.BlockLight)
                    {
                        neighbor.BlockLight = newBlockLight;
                        updated = true;
                    }
                }
                
                // Add to queue if lighting was updated
                if (updated)
                {
                    lightQueue.Enqueue(neighborPos);
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
                    
                    // Normalize light level from 0-15 to 0-1, ensuring minimum visibility
                    float lightValue = Math.Max(0.1f, lightLevel / 15.0f);
                    
                    var color = new Color(
                        (int)(255 * lightValue),
                        (int)(255 * lightValue),
                        (int)(255 * lightValue),
                        255
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
        var oldBlock = chunk.Blocks[localX, localY, localZ];
        chunk.Blocks[localX, localY, localZ] = new Block(blockPos, Block.Prefabs[BlockType.Air]);
        chunk.Info.Modified = true;

        // Update lighting if the broken block was opaque or emissive
        updateLightingForBlockChange(chunk, localX, localY, localZ, oldBlock, chunk.Blocks[localX, localY, localZ]);
        
        // Regenerate mesh
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
        var oldBlock = chunk.Blocks[localX, localY, localZ];
        chunk.Blocks[localX, localY, localZ] = new Block(newBlockPos, Block.Prefabs[blockType]);
        chunk.Info.Modified = true;
        
        // Update lighting for the placed block
        updateLightingForBlockChange(chunk, localX, localY, localZ, oldBlock, chunk.Blocks[localX, localY, localZ]);
        
        // Regenerate mesh
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
    
    private void updateLightingForBlockChange(Chunk chunk, int x, int y, int z, Block oldBlock, Block newBlock)
    {
        // For now, do full relighting of the chunk when blocks change
        // This is simpler but less efficient than proper local relighting
        generateLighting(chunk);
        
        // TODO: Implement proper local relighting for better performance
        // This would involve:
        // 1. Light removal BFS from the changed block
        // 2. Light addition BFS from neighboring blocks
        // 3. Handling cross-chunk light propagation
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
            
            //Raylib.DrawCubeWires(chunk.Position + new Vector3(ChunkSize / 2f, ChunkHeight / 2f, ChunkSize / 2f),
            //    ChunkSize, ChunkHeight, ChunkSize, debugColor);
        }
    }
}