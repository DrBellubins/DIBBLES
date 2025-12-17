using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using System.Runtime.InteropServices;
using DIBBLES.Effects;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainMesh
{
    public const bool SmoothLighting = true; // Flat lighting doesn't work, don't care to fix it
    
    public HashSet<Vector3Int> RecentlyRemeshedNeighbors = new();

    public Dictionary<Vector3Int, RuntimeModel> OpaqueModels = new();
    public Dictionary<Vector3Int, RuntimeModel> TransparentModels = new();

    // Main-thread mesh upload queue
    public readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> MeshUploadQueue = new(); // Opaque
    public readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> TMeshUploadQueue = new(); // Transparent
    
    public void Generate(Chunk chunk)
    {
        var meshData = Mesh.GenerateMeshData(chunk, false);
        var tMeshData = Mesh.GenerateMeshData(chunk, true);
        
        // Enqueue for main thread mesh upload
        MeshUploadQueue.Enqueue((chunk.Position, meshData));
        TMeshUploadQueue.Enqueue((chunk.Position, tMeshData));
    }

    public void DrawAllMeshes()
    {
        // Draw opaque
        foreach (var oModel in Mesh.OpaqueModels)
        {
            // oModel.Value is a RuntimeModel
            if (oModel.Value != null)
            {
                var world = Matrix.CreateTranslation(oModel.Key.ToVector3());
                
                var shader = oModel.Value.Shader;
                
                shader.Parameters["World"].SetValue(world);
                shader.Parameters["View"].SetValue(GameScene.PlayerCharacter.Camera.View);
                shader.Parameters["Projection"].SetValue(GameScene.PlayerCharacter.Camera.Projection);
                shader.Parameters["Texture0"].SetValue(BlockData.TextureAtlas);
                shader.Parameters["CameraPos"].SetValue(GameScene.PlayerCharacter.Camera.Position.ToVector3());
                shader.Parameters["FogNear"].SetValue(FogEffect.FogNear);
                shader.Parameters["FogFar"].SetValue(FogEffect.FogFar);
                shader.Parameters["FogColor"].SetValue(FogEffect.FogColor());
                
                foreach (var pass in shader.CurrentTechnique.Passes)
                    pass.Apply();
                
                oModel.Value.Draw(world,                        // World matrix for chunk position
                    GameScene.PlayerCharacter.Camera.View,      // Your camera's view matrix
                    GameScene.PlayerCharacter.Camera.Projection // Your camera's projection matrix
                );
            }
        }
        
        // Draw transparent
        foreach (var tModel in Mesh.TransparentModels)
        {
            // oModel.Value is a RuntimeModel
            if (tModel.Value != null)
            {
                var world = Matrix.CreateTranslation(tModel.Key.ToVector3());
                
                var shader = tModel.Value.Shader;
                
                shader.Parameters["World"].SetValue(world);
                shader.Parameters["View"].SetValue(GameScene.PlayerCharacter.Camera.View);
                shader.Parameters["Projection"].SetValue(GameScene.PlayerCharacter.Camera.Projection);
                shader.Parameters["Texture0"].SetValue(BlockData.TextureAtlas);
                shader.Parameters["CameraPos"].SetValue(GameScene.PlayerCharacter.Camera.Position.ToVector3());
                shader.Parameters["FogNear"].SetValue(FogEffect.FogNear);
                shader.Parameters["FogFar"].SetValue(FogEffect.FogFar);
                shader.Parameters["FogColor"].SetValue(FogEffect.FogColor());
                
                foreach (var pass in shader.CurrentTechnique.Passes)
                    pass.Apply();
                
                tModel.Value.Draw(world,                        // World matrix for chunk position
                    GameScene.PlayerCharacter.Camera.View,      // Your camera's view matrix
                    GameScene.PlayerCharacter.Camera.Projection // Your camera's projection matrix
                );
            }
        }
    }
    
    // MeshData generation (thread-safe, no Raylib calls)
    public MeshData GenerateMeshData(Chunk chunk, bool isTransparencyPass)
    {
        var cameraPosition = GameScene.PlayerCharacter.Camera.Position.ToVector3();
        
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
            var pos = new Vector3Int(x, y, z);
            var blockType = chunk.GetTypeAt(x, y, z);
            var blockInfo = chunk.GetInfoAt(x, y, z);
            
            // Opaque mesh pass: skip transparent and air blocks
            if (!isTransparencyPass && (blockInfo.IsTransparent || blockType == BlockType.Air)) continue;

            // Transparent mesh pass: skip opaque and air blocks
            if (isTransparencyPass && (!blockInfo.IsTransparent || blockType == BlockType.Air)) continue;
            
            int vertexOffset = vertices.Count;

            foreach (var (faceIdx, normal, neighborOffset) in FaceUtils.VoxelFaceInfos())
            {
                int nx = x + neighborOffset.X;
                int ny = y + neighborOffset.Y;
                int nz = z + neighborOffset.Z;
        
                long chunkSeed = Seed 
                                 ^ (pos.X * 73428767L)
                                 ^ (pos.Y * 9127841L)
                                 ^ (pos.Z * 192837465L);
        
                var rng = new SeededRandom(chunkSeed);
                
                if (!isVoxelSolid(chunk, nx, ny, nz))
                {
                    var faceVerts = FaceUtils.GetFaceVertices(pos.ToVector3(), faceIdx);
                    var faceUVs = FaceUtils.GetFaceUVs(blockType, faceIdx);
                    
                    float faceLight = FaceUtils.GetFaceLightFlat(chunk, pos, faceIdx); // samples surrounding voxels
                    Color flatColor = FaceUtils.ToColor(faceLight);

                    Color[] faceColors = SmoothLighting
                        ? FaceUtils.GetFaceColors(chunk, pos, faceIdx)
                        : new[] { flatColor, flatColor, flatColor, flatColor };

                    var rndOffset = (int)(rng.NextFloat() * ChunkSize);
                    var worldBlockPosRNG = new Vector3Int(pos.X + rndOffset, pos.Y + rndOffset, pos.Z + rndOffset);
                    
                    // Deterministic random rotation for this block face
                    //int rotation = ((worldBlockPos.X) ^ (worldBlockPos.Y) ^ (worldBlockPos.Z) ^ faceIdx) & 3;
                    //faceUVs = FaceUtils.RotateUVs(faceUVs, rotation);
                    
                    // Deterministic random flipping for this block face
                    int flipRandom = ((worldBlockPosRNG.X) ^ (worldBlockPosRNG.Y) ^ (worldBlockPosRNG.Z) ^ faceIdx) & 3;

                    // For side faces, only allow axes enabled by TOML
                    int flip = 0;
                    
                    if (faceIdx >= 0 && faceIdx <= 3) // Sides
                    {
                        if (blockInfo.AntiTileUVsHorizontally && (flipRandom & 1) != 0)
                            flip |= 1; // horizontal
                        if (blockInfo.AntiTileUVsVertically && (flipRandom & 2) != 0)
                            flip |= 2; // vertical
                    }
                    else // Top/bottom faces: always allow random flip
                        flip = flipRandom;

                    // TODO: AntiTileUVsHorizontally & AntiTileUVsVertically false does not disable flipping for tops and bottoms
                    // See Feeb block
                    
                    // Treat horizontal and vertical being false as disabling uv flipping for entire block
                    if (blockInfo.AntiTileUVsHorizontally || blockInfo.AntiTileUVsVertically)
                        faceUVs = FaceUtils.FlipUVsAtlas(faceUVs, faceIdx, flip);
                    
                    if (isTransparencyPass)
                    {
                        // For transparent faces, store for sorting
                        var center = (faceVerts[0] + faceVerts[1] + faceVerts[2] + faceVerts[3]) / 4f;
                        var dist = Vector3.Distance(cameraPosition, center);
                        
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
                            vertexOffset + 2, vertexOffset + 1, vertexOffset + 0,
                            vertexOffset + 3, vertexOffset + 2, vertexOffset + 0
                        });
        
                        vertexOffset += 4;
                    }
                }
            }
        }
        
        // If transparent: sort faces back-to-front and build arrays
        if (isTransparencyPass)
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
                    vertexOffset + 2, vertexOffset + 1, vertexOffset + 0,
                    vertexOffset + 3, vertexOffset + 2, vertexOffset + 0
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
    
    public void RemeshBorderingChunks(Vector3Int chunkPos, Vector3Int localPos)
    {
        // Local block coordinates within the chunk
        int localX = localPos.X;
        int localY = localPos.Y;
        int localZ = localPos.Z;

        // Possible neighbor chunk directions if at border
        var directions = new List<Vector3Int>();
        
        if (localX == 0) directions.Add(new Vector3Int(-ChunkSize, 0, 0));
        if (localX == ChunkSize - 1) directions.Add(new Vector3Int(ChunkSize, 0, 0));
        if (localY == 0) directions.Add(new Vector3Int(0, -ChunkSize, 0));
        if (localY == ChunkSize - 1) directions.Add(new Vector3Int(0, ChunkSize, 0));
        if (localZ == 0) directions.Add(new Vector3Int(0, 0, -ChunkSize));
        if (localZ == ChunkSize - 1) directions.Add(new Vector3Int(0, 0, ChunkSize));
    
        foreach (var dir in directions)
        {
            var neighborChunkPos = chunkPos + dir;
            if (ChunkBuffer.TryGetValue(neighborChunkPos, out var neighborChunk))
            {
                // Remesh opaque
                var meshData = GenerateMeshData(neighborChunk, false);
                OpaqueModels[neighborChunkPos] = UploadMesh(meshData);

                // Remesh transparent
                var tMeshData = GenerateMeshData(neighborChunk, true);
                TransparentModels[neighborChunkPos] = UploadMesh(tMeshData);
            }
        }
    }
    
    public void RemeshAllTransparentChunks()
    {
        foreach (var chunk in ChunkBuffer.Values)
        {
            var tMeshData = Mesh.GenerateMeshData(chunk, true);
            Mesh.TransparentModels[chunk.Position] = Mesh.UploadMesh(tMeshData);
        }
    }
    
    // Main-thread only: allocates Raylib Mesh, uploads data, returns Model
    public RuntimeModel UploadMesh(MeshData data)
    {
        var graphicsDevice = Engine.Graphics;

        // Don't process empty mesh data
        if (data.VertexCount == 0 || data.Vertices == null || data.Indices == null || data.Indices.Length == 0)
            return null;

        // Pack vertex data
        var verts = new VertexPositionNormalTextureColor[data.VertexCount];
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

            verts[i] = new VertexPositionNormalTextureColor(pos, norm, tex, color);
        }

        // Create buffers
        var vertexBuffer = new VertexBuffer(
            graphicsDevice,
            VertexPositionNormalTextureColor.VertexDeclaration,
            verts.Length,
            BufferUsage.WriteOnly);

        vertexBuffer.SetData(verts);

        var indexBuffer = new IndexBuffer(
            graphicsDevice,
            IndexElementSize.SixteenBits,
            data.Indices.Length,
            BufferUsage.WriteOnly);

        indexBuffer.SetData(data.Indices);

        // Return as RuntimeModel
        return new RuntimeModel
        {
            VertexBuffer = vertexBuffer,
            IndexBuffer = indexBuffer,
            TriangleCount = data.TriangleCount,
            Shader = terrainShader.Clone(),
            Texture = BlockData.TextureAtlas
        };
    }
    
    // Helper to check if neighbor block is solid, including across chunk boundaries
    private static bool isVoxelSolid(Chunk currentChunk, int x, int y, int z)
    {
        // Fast check: within current chunk bounds
        if (x >= 0 && x < ChunkSize &&
            y >= 0 && y < ChunkSize &&
            z >= 0 && z < ChunkSize)
        {
            var info = currentChunk.GetInfoAt(x, y, z);
            return currentChunk.GetTypeAt(x, y, z) != BlockType.Air && !info.IsTransparent;
        }

        // Otherwise, need to look up neighbor chunk
        int worldX = currentChunk.Position.X + x;
        int worldY = currentChunk.Position.Y + y;
        int worldZ = currentChunk.Position.Z + z;

        // Find the neighbor chunk's base position
        int neighborChunkX = (int)Math.Floor((float)worldX / ChunkSize) * ChunkSize;
        int neighborChunkY = (int)Math.Floor((float)worldY / ChunkSize) * ChunkSize;
        int neighborChunkZ = (int)Math.Floor((float)worldZ / ChunkSize) * ChunkSize;

        var neighborChunkPos = new Vector3Int(neighborChunkX, neighborChunkY, neighborChunkZ);

        if (ChunkBuffer.TryGetValue(neighborChunkPos, out var neighborChunk))
        {
            int localX = worldX - neighborChunkX;
            int localY = worldY - neighborChunkY;
            int localZ = worldZ - neighborChunkZ;

            var info = neighborChunk.GetInfoAt(localX, localY, localZ);
            
            return neighborChunk.GetTypeAt(localX, localY, localZ) != BlockType.Air && !info.IsTransparent;
        }

        // If neighbor chunk not loaded, treat as air (or optionally skip meshing)
        return false;
    }
}

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