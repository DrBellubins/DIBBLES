using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

// Define a custom vertex struct with Position, Normal, Texcoord, Color
[StructLayout(LayoutKind.Sequential)]
public struct VertexPositionNormalTextureColor : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;
    public Color Color;

    public readonly static VertexDeclaration VertexDeclaration = new VertexDeclaration
    (
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(32, VertexElementFormat.Color, VertexElementUsage.Color, 0)
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;

    public VertexPositionNormalTextureColor(Vector3 pos, Vector3 norm, Vector2 tex, Color color)
    {
        Position = pos;
        Normal = norm;
        TexCoord = tex;
        Color = color;
    }
}

public class TerrainMesh
{
    public const bool Fullbright = false;
    public const bool SmoothLighting = false;
    
    public HashSet<Vector3Int> RecentlyRemeshedNeighbors = new();

    public Dictionary<Vector3Int, RuntimeModel> OpaqueModels = new();
    public Dictionary<Vector3Int, RuntimeModel> TransparentModels = new();
    
    // MeshData generation (thread-safe, no Raylib calls)
    public MeshData GenerateMeshData(Chunk chunk, bool isTransparencyPass, Vector3? cameraPosition = null)
    {
        List<(float dist, FaceData face)> transparentFaces = new();
        List<Vector3> vertices = [];
        List<int> indices = [];
        List<Vector3> normals = [];
        List<Vector2> texcoords = [];
        List<Color> colors = [];
        
        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var pos = new Vector3(x, y, z);
            var block = chunk.GetBlock(x, y, z);
            var blockType = block.Type;
            var isTransparent = block.Info.IsTransparent;
            
            // Opaque mesh pass: skip transparent and air blocks
            if (!isTransparencyPass && (isTransparent || blockType == BlockType.Air)) continue;

            // Transparent mesh pass: skip opaque and air blocks
            if (isTransparencyPass && (!isTransparent || blockType == BlockType.Air)) continue;
            
            int vertexOffset = vertices.Count;

            foreach (var (faceIdx, normal, neighborOffset) in FaceUtils.VoxelFaceInfos())
            {
                int nx = x + neighborOffset.X;
                int ny = y + neighborOffset.Y;
                int nz = z + neighborOffset.Z;
        
                long chunkSeed = GameSceneMono.TerrainGen.Seed 
                                 ^ (block.Position.X * 73428767L)
                                 ^ (block.Position.Y * 9127841L)
                                 ^ (block.Position.Z * 192837465L);
        
                var rng = new SeededRandom(chunkSeed);
                
                if (!isVoxelSolid(chunk, isTransparencyPass, nx, ny, nz))
                {
                    var faceVerts = FaceUtils.GetFaceVertices(pos, faceIdx);
                    var faceUVs = FaceUtils.GetFaceUVs(blockType, faceIdx);
                    var faceColors = FaceUtils.GetFaceColors(chunk, Vector3Int.FromVector3(pos), faceIdx);

                    var rndOffset = (int)(rng.NextFloat() * ChunkSize);
                    var worldBlockPos = new Vector3Int(block.Position.X + rndOffset, block.Position.Y + rndOffset, block.Position.Z + rndOffset);
                    
                    // Deterministic random rotation for this block face
                    //int rotation = ((worldBlockPos.X) ^ (worldBlockPos.Y) ^ (worldBlockPos.Z) ^ faceIdx) & 3;
                    //faceUVs = FaceUtils.RotateUVs(faceUVs, rotation);
                    
                    int flip = ((worldBlockPos.X) ^ (worldBlockPos.Y) ^ (worldBlockPos.Z) ^ faceIdx) & 3;
                    faceUVs = FaceUtils.FlipUVsAtlas(faceUVs, flip);
                    
                    if (isTransparencyPass && cameraPosition.HasValue)
                    {
                        // For transparent faces, store for sorting
                        var center = (faceVerts[0] + faceVerts[1] + faceVerts[2] + faceVerts[3]) / 4f;
                        var dist = Vector3.Distance(cameraPosition.Value, center);
                        
                        transparentFaces.Add((dist, new FaceData
                        {
                            Verts = faceVerts,
                            Normal = normal,
                            UVs = faceUVs,
                            Colors = faceColors,
                            VertexOffset = vertexOffset,
                            CenterDistance =  dist
                        }));
                    }
                    else
                    {
                        // Opaque faces: add immediately
                        vertices.AddRange(faceVerts);
                        normals.AddRange(Enumerable.Repeat(normal, 4));
                        texcoords.AddRange(faceUVs);
                        colors.AddRange(faceColors);
        
                        indices.AddRange(new int[]
                        {
                            vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
                            vertexOffset + 0, vertexOffset + 2, vertexOffset + 3
                        });
        
                        vertexOffset += 4;
                    }
                }
            }
        }
        
        // If transparent: sort faces back-to-front and build arrays
        if (isTransparencyPass && cameraPosition.HasValue)
        {
            transparentFaces.Sort((a, b) => b.dist.CompareTo(a.dist));
            
            int vertexOffset = 0;
            
            foreach (var (_, face) in transparentFaces)
            {
                vertices.AddRange(face.Verts);
                normals.AddRange(Enumerable.Repeat(face.Normal, 4));
                texcoords.AddRange(face.UVs);
                colors.AddRange(face.Colors);
                
                indices.AddRange(new int[]
                {
                    vertexOffset + 0, vertexOffset + 1, vertexOffset + 2,
                    vertexOffset + 0, vertexOffset + 2, vertexOffset + 3
                });
                
                vertexOffset += 4;
            }
        }

        // Convert lists to arrays
        int vcount = vertices.Count;
        int icount = indices.Count / 3;
        var meshData = new MeshData(vcount, icount);
        
        for (int i = 0; i < vertices.Count; i++)
        {
            meshData.Vertices[i * 3 + 0] = vertices[i].X;
            meshData.Vertices[i * 3 + 1] = vertices[i].Y;
            meshData.Vertices[i * 3 + 2] = vertices[i].Z;
        }
        
        for (int i = 0; i < normals.Count; i++)
        {
            meshData.Normals[i * 3 + 0] = normals[i].X;
            meshData.Normals[i * 3 + 1] = normals[i].Y;
            meshData.Normals[i * 3 + 2] = normals[i].Z;
        }
        
        for (int i = 0; i < texcoords.Count; i++)
        {
            meshData.TexCoords[i * 2 + 0] = texcoords[i].X;
            meshData.TexCoords[i * 2 + 1] = texcoords[i].Y;
        }
        
        for (int i = 0; i < colors.Count; i++)
        {
            meshData.Colors[i * 4 + 0] = colors[i].R;
            meshData.Colors[i * 4 + 1] = colors[i].G;
            meshData.Colors[i * 4 + 2] = colors[i].B;
            meshData.Colors[i * 4 + 3] = colors[i].A;
        }
        
        for (int i = 0; i < indices.Count; i++)
        {
            meshData.Indices[i] = (ushort)indices[i];
        }

        return meshData;
    }
    
    public void RemeshAllTransparentChunks(Vector3 cameraPos)
    {
        foreach (var chunk in ECSChunks.Values)
        {
            if (TMesh.TransparentModels.TryGetValue(chunk.Position, out var oldModel))
                oldModel.Dispose();

            var tMeshData = TMesh.GenerateMeshData(chunk, true, cameraPos);
            TMesh.TransparentModels[chunk.Position] = TMesh.UploadMesh(tMeshData);
        }
    }
    
    // Main-thread only: allocates Raylib Mesh, uploads data, returns Model
    public RuntimeModel UploadMesh(MeshData data)
    {
        var graphicsDevice = MonoEngine.Graphics;

        // Step 1: Pack mesh data into vertex structs
        var vertices = new VertexPositionNormalTextureColor[data.VertexCount];

        for (int i = 0; i < data.VertexCount; i++)
        {
            var pos = new Vector3(
                data.Vertices[i * 3 + 0],
                data.Vertices[i * 3 + 1],
                data.Vertices[i * 3 + 2]);

            var norm = new Vector3(
                data.Normals[i * 3 + 0],
                data.Normals[i * 3 + 1],
                data.Normals[i * 3 + 2]);

            var tex = new Vector2(
                data.TexCoords[i * 2 + 0],
                data.TexCoords[i * 2 + 1]);

            var color = new Color(
                data.Colors[i * 4 + 0],
                data.Colors[i * 4 + 1],
                data.Colors[i * 4 + 2],
                data.Colors[i * 4 + 3]);

            vertices[i] = new VertexPositionNormalTextureColor(pos, norm, tex, color);
        }

        // Step 2: Create VertexBuffer
        var vertexBuffer = new VertexBuffer(
            graphicsDevice,
            VertexPositionNormalTextureColor.VertexDeclaration,
            vertices.Length,
            BufferUsage.WriteOnly
        );
        
        vertexBuffer.SetData(vertices);

        // Step 3: Create IndexBuffer
        var indexBuffer = new IndexBuffer(
            graphicsDevice,
            IndexElementSize.SixteenBits,
            data.Indices.Length,
            BufferUsage.WriteOnly
        );
        indexBuffer.SetData(data.Indices);

        // Step 4: Create or assign an Effect
        // You can use BasicEffect for basic lighting, or a custom Effect if you have one
        var effect = new BasicEffect(graphicsDevice)
        {
            LightingEnabled = false,
            TextureEnabled = true,
            VertexColorEnabled = true,
            // Texture = ... assign your block atlas here if needed
        };

        // Step 5: Build RuntimeModel and return
        return new RuntimeModel
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            TriangleCount = data.TriangleCount,
            Effect = effect,
            // Texture = ... assign atlas here if needed
        };
    }
    
    private bool isVoxelSolid(Chunk chunk, bool isTransparentPass, int x, int y, int z)
    {
        if (isTransparentPass)
            return false;
        
        BlockInfo info = new BlockInfo();
        
        if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize || z < 0 || z >= ChunkSize)
        {
            // Compute which chunk to check in all axes
            Vector3Int chunkCoord = new Vector3Int(
                chunk.Position.X / ChunkSize,
                chunk.Position.Y / ChunkSize,
                chunk.Position.Z / ChunkSize
            );

            Vector3Int neighborCoord = chunkCoord;
            int nx = x, ny = y, nz = z;

            if (x < 0) { nx = ChunkSize - 1; neighborCoord.X -= 1; }
            else if (x >= ChunkSize) { nx = 0; neighborCoord.X += 1; }

            if (y < 0) { ny = ChunkSize - 1; neighborCoord.Y -= 1; }
            else if (y >= ChunkSize) { ny = 0; neighborCoord.Y += 1; }

            if (z < 0) { nz = ChunkSize - 1; neighborCoord.Z -= 1; }
            else if (z >= ChunkSize) { nz = 0; neighborCoord.Z += 1; }

            Vector3Int neighborChunkPos = new Vector3Int(
                neighborCoord.X * ChunkSize,
                neighborCoord.Y * ChunkSize,
                neighborCoord.Z * ChunkSize
            );

            // Look up the neighboring chunk
            if (ECSChunks.TryGetValue(neighborChunkPos, out var neighborChunk))
            {
                info = neighborChunk.GetBlock(nx, ny, nz).Info;
            }
        }
        else
        {
            info = chunk.GetBlock(x, y, z).Info;
        }

        // Air blocks are NOT solid
        //if (info == null)
        //    return false;
        
        if (!isTransparentPass) // Opaque pass: treat transparent blocks as non-solid
            return chunk.GetBlock(x, y, z).Type != BlockType.Air && !info.IsTransparent;
        else // Transparent pass: treat only transparent blocks as solid
            return chunk.GetBlock(x, y, z).Type != BlockType.Air && info.IsTransparent;
    }

    public void RemeshNeighbors(Chunk chunk, bool isTransparentPass)
    {
        int[] offsets = { -ChunkSize, ChunkSize };
        
        foreach (var axis in new[] { 0, 1, 2 })
        {
            foreach (int offset in offsets)
            {
                Vector3Int neighborPos = chunk.Position;
                
                if (axis == 0) neighborPos.X += offset;
                if (axis == 1) neighborPos.Y += offset;
                if (axis == 2) neighborPos.Z += offset;

                if (ECSChunks.TryGetValue(neighborPos, out var neighborChunk))
                    RemeshNeighborPos(neighborChunk.Position, isTransparentPass);
            }
        }
    }
    
    public void RemeshNeighborPos(Vector3Int neighborPos, bool isTransparentPass)
    {
        if (RecentlyRemeshedNeighbors.Contains(neighborPos))
            return; // Already remeshed this frame

        if (ECSChunks.TryGetValue(neighborPos, out var neighborChunk))
        {
            // Opaque or transparent model dictionary
            var modelDict = isTransparentPass ? TransparentModels : OpaqueModels;

            // Unload existing model if present
            if (modelDict.TryGetValue(neighborPos, out var oldModel))
                oldModel.Dispose();

            // Generate new mesh
            var meshData = GenerateMeshData(neighborChunk, isTransparentPass);
            var newModel = UploadMesh(meshData);

            // Store new model
            modelDict[neighborPos] = newModel;

            RecentlyRemeshedNeighbors.Add(neighborPos);
        }
    }
}