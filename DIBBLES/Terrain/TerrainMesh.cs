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
    public const bool SmoothLighting = true;
    
    public static float FrustumCullRadius = (float)(MathF.Sqrt(3f) * 0.5f * ChunkSize);

    public Dictionary<Vector3Int, RuntimeModel> OpaqueModels = new();
    public Dictionary<Vector3Int, RuntimeModel> TransparentModels = new();
    
    public Dictionary<Vector3Int, Dictionary<BlockType, (VertexBuffer InstanceBuffer, int InstanceCount)>> BillboardBatches = new();

    // Main-thread mesh upload queue
    public readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> MeshUploadQueue = new(); // Opaque
    public readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> TMeshUploadQueue = new(); // Transparent
    public readonly ConcurrentQueue<(Vector3Int chunkPos, BlockType type, VertexBillboardInstance[] instances)> BillboardUploadQueue = new(); // Billboards
    
    private VertexBuffer _billboardVB;
    private IndexBuffer _billboardIB;
    
    public void Generate(Chunk chunk)
    {
        var meshData = Mesh.GenerateMeshData(chunk, false);
        MeshUploadQueue.Enqueue((chunk.Position, meshData));

        if (chunkContainsTransparent(chunk))
        {
            var tMeshData = Mesh.GenerateMeshData(chunk, true);
            TMeshUploadQueue.Enqueue((chunk.Position, tMeshData));
        }
        
        var billboardInstancesByType = GenerateBillboardInstances(chunk);

        foreach (var kv in billboardInstancesByType)
            BillboardUploadQueue.Enqueue((chunk.Position, kv.Key, kv.Value));
    }
    
    public void DrawOpaque()
    {
        var graphics = Engine.Graphics;
        
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.RasterizerState = RasterizerState.CullCounterClockwise;
        graphics.SamplerStates[0] = SamplerState.PointClamp;
        
        foreach (var oModel in Mesh.OpaqueModels)
        {
            // Chunk center in world space
            Vector3 center = oModel.Key.ToVector3() + new Vector3(ChunkSize * 0.5f);

            // Skip if chunk is outside view frustum
            if (!GameScene.PlayerCharacter.Camera.InFrustum(center, FrustumCullRadius))
                continue;
            
            // oModel.Value is a RuntimeModel
            if (oModel.Value != null)
            {
                var world = Matrix.CreateTranslation(oModel.Key.ToVector3());
                var shader = oModel.Value.Shader;
                
                shader.Parameters["AtlasTex"].SetValue(BlockData.TextureAtlas);
                
                shader.Parameters["World"].SetValue(world);
                shader.Parameters["View"].SetValue(GameScene.PlayerCharacter.Camera.View);
                shader.Parameters["Projection"].SetValue(GameScene.PlayerCharacter.Camera.Projection);
                
                shader.Parameters["CameraPos"]?.SetValue(GameScene.PlayerCharacter.Camera.Position.ToVector3());
                shader.Parameters["CameraNear"]?.SetValue(GameScene.PlayerCharacter.Camera.NearPlane);
                shader.Parameters["CameraFar"]?.SetValue(GameScene.PlayerCharacter.Camera.FarPlane);
                    
                shader.Parameters["FogNear"]?.SetValue(FogEffect.FogNear);
                shader.Parameters["FogFar"]?.SetValue(FogEffect.FogFar);
                shader.Parameters["FogColor"]?.SetValue(FogEffect.FogColor());

                var view = GameScene.PlayerCharacter.Camera.View;
                var projection = GameScene.PlayerCharacter.Camera.Projection;
                
                foreach (var pass in shader.CurrentTechnique.Passes)
                    pass.Apply();
                
                oModel.Value.Draw(world, view, projection);
            }
        }
    }

    public void DrawTransparent()
    {
        var graphics = Engine.Graphics;
        
        graphics.BlendState = BlendState.NonPremultiplied;
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.SamplerStates[0] = SamplerState.PointClamp;
        
        // Disable culling so billboards render double-sided
        graphics.RasterizerState = RasterizerState.CullNone;
        
        foreach (var tModel in Mesh.TransparentModels)
        {
            // Chunk center in world space
            Vector3 center = tModel.Key.ToVector3() + new Vector3(ChunkSize * 0.5f);

            // Skip if chunk is outside view frustum
            if (!GameScene.PlayerCharacter.Camera.InFrustum(center, FrustumCullRadius))
                continue;
            
            // oModel.Value is a RuntimeModel
            if (tModel.Value != null)
            {
                var world = Matrix.CreateTranslation(tModel.Key.ToVector3());
                var shader = tModel.Value.Shader;
                
                shader.Parameters["AtlasTex"].SetValue(BlockData.TextureAtlas);
                
                shader.Parameters["World"].SetValue(world);
                shader.Parameters["View"].SetValue(GameScene.PlayerCharacter.Camera.View);
                shader.Parameters["Projection"].SetValue(GameScene.PlayerCharacter.Camera.Projection);
                
                shader.Parameters["CameraPos"]?.SetValue(GameScene.PlayerCharacter.Camera.Position.ToVector3());
                shader.Parameters["CameraNear"]?.SetValue(GameScene.PlayerCharacter.Camera.NearPlane);
                shader.Parameters["CameraFar"]?.SetValue(GameScene.PlayerCharacter.Camera.FarPlane);
                
                shader.Parameters["FogNear"]?.SetValue(FogEffect.FogNear);
                shader.Parameters["FogFar"]?.SetValue(FogEffect.FogFar);
                shader.Parameters["FogColor"]?.SetValue(FogEffect.FogColor());
    
                var view = GameScene.PlayerCharacter.Camera.View;
                var projection = GameScene.PlayerCharacter.Camera.Projection;
                
                foreach (var pass in shader.CurrentTechnique.Passes)
                    pass.Apply();
                
                tModel.Value.Draw(world, view, projection);
            }
        }
    }
    
    public void DrawBillboards()
    {
        EnsureBillboardMesh();
    
        var graphics = Engine.Graphics;
    
        graphics.BlendState = BlendState.NonPremultiplied;
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.RasterizerState = RasterizerState.CullNone;
        graphics.SamplerStates[0] = SamplerState.PointClamp;
    
        var shader = billboardShader;
        var view = GameScene.PlayerCharacter.Camera.View;
        var proj = GameScene.PlayerCharacter.Camera.Projection;
    
        shader.Parameters["AtlasTex"]?.SetValue(BlockData.TextureAtlas);
        shader.Parameters["View"]?.SetValue(view);
        shader.Parameters["Projection"]?.SetValue(proj);
    
        shader.Parameters["CameraPos"]?.SetValue(GameScene.PlayerCharacter.Camera.Position.ToVector3());
        shader.Parameters["CameraNear"]?.SetValue(GameScene.PlayerCharacter.Camera.NearPlane);
        shader.Parameters["CameraFar"]?.SetValue(GameScene.PlayerCharacter.Camera.FarPlane);
    
        shader.Parameters["FogNear"]?.SetValue(FogEffect.FogNear);
        shader.Parameters["FogFar"]?.SetValue(FogEffect.FogFar);
        shader.Parameters["FogColor"]?.SetValue(FogEffect.FogColor());
    
        shader.Parameters["Time"]?.SetValue(Time.time);
        
        foreach (var kv in BillboardBatches)
        {
            // kv.Key is the chunk position
            Vector3 center = kv.Key.ToVector3() + new Vector3(ChunkSize * 0.5f);

            // Skip if chunk is outside view frustum
            if (!GameScene.PlayerCharacter.Camera.InFrustum(center, FrustumCullRadius))
                continue;
            
            var batchesByType = kv.Value;
    
            foreach (var typeBatch in batchesByType)
            {
                var type = typeBatch.Key;
                var batch = typeBatch.Value;
    
                if (batch.InstanceBuffer == null || batch.InstanceCount <= 0)
                    continue;
    
                var rect = BlockData.AtlasUVs[(type, 0)];
                shader.Parameters["UVRect"]?.SetValue(new Vector4(rect.X, rect.Y, rect.Width, rect.Height));
    
                graphics.SetVertexBuffers
                (
                    new VertexBufferBinding(_billboardVB, 0, 0),
                    new VertexBufferBinding(batch.InstanceBuffer, 0, 1)
                );
    
                graphics.Indices = _billboardIB;
    
                foreach (var pass in shader.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphics.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 8, 0, 4, batch.InstanceCount);
                }
            }
        }
    
        graphics.SetVertexBuffer(null);
        graphics.Indices = null;
    }
    
    // MeshData generation (thread-safe, no Raylib calls)
    public MeshData GenerateMeshData(Chunk chunk, bool isTransparencyPass)
    {
        var cameraPosition = GameScene.PlayerCharacter.Camera.Position.ToVector3();
        
        var neighborCache = NeighborCache.Build(chunk);
        
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
            
            // Billboard path for transparent mesh
            if (isTransparencyPass && blockInfo.IsBillboard && blockType != BlockType.Air)
            {
                // Billboards are handled by instanced pipeline; skip adding faces here
                continue;
            }
            
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
                
                if (!isVoxelSolid(chunk, nx, ny, nz, neighborCache))
                {
                    var faceVerts = FaceUtils.GetFaceVertices(pos.ToVector3(), faceIdx);
                    var faceUVs = FaceUtils.GetFaceUVs(blockType, faceIdx);
                    
                    float faceLight = FaceUtils.GetFaceLightFlat(chunk, pos, faceIdx); // samples surrounding voxels
                    Color flatColor = FaceUtils.ToColor(faceLight);

                    Color[] faceColors = SmoothLighting
                        ? FaceUtils.GetFaceColors(chunk, pos, faceIdx)
                        : new[] { flatColor, flatColor, flatColor, flatColor };
                    
                    // Ambient occlusion - Disabled because it looks to "grungey"
                    /*float[] ao = FaceUtils.GetFaceAmbientOcclusion(chunk, neighborCache, pos, faceIdx); // AO factors [0..1]
                    
                    // Multiply AO into vertex colors
                    for (int vi = 0; vi < 4; vi++)
                    {
                        var c = faceColors[vi];
                        float f = ao[vi];

                        faceColors[vi] = new Color(
                            (byte)(c.R * f),
                            (byte)(c.G * f),
                            (byte)(c.B * f),
                            c.A
                        );
                    }*/

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
            meshData.Indices[i] = (ushort)indices[i];

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
    private static bool isVoxelSolid(Chunk currentChunk, int x, int y, int z, NeighborCache cache)
    {
        // Fast in-chunk path
        if (x >= 0 && x < ChunkSize &&
            y >= 0 && y < ChunkSize &&
            z >= 0 && z < ChunkSize)
        {
            var info = currentChunk.GetInfoAt(x, y, z);
            return currentChunk.GetTypeAt(x, y, z) != BlockType.Air && !info.IsTransparent;
        }

        // Only axial neighbors are queried by meshing; if more than one axis is out, treat as non-solid
        int crosses =
            (x < 0 || x >= ChunkSize ? 1 : 0) +
            (y < 0 || y >= ChunkSize ? 1 : 0) +
            (z < 0 || z >= ChunkSize ? 1 : 0);

        if (crosses > 1)
            return false;

        Chunk target = currentChunk;
        int lx = x, ly = y, lz = z;

        // X axis wrap
        if (lx < 0)
        {
            if (cache.NegX == null)
                return false;

            target = cache.NegX;
            lx += ChunkSize;
        }
        else if (lx >= ChunkSize)
        {
            if (cache.PosX == null)
                return false;

            target = cache.PosX;
            lx -= ChunkSize;
        }

        // Y axis wrap
        if (ly < 0)
        {
            if (cache.NegY == null)
                return false;

            target = cache.NegY;
            ly += ChunkSize;
        }
        else if (ly >= ChunkSize)
        {
            if (cache.PosY == null)
                return false;

            target = cache.PosY;
            ly -= ChunkSize;
        }

        // Z axis wrap
        if (lz < 0)
        {
            if (cache.NegZ == null)
                return false;

            target = cache.NegZ;
            lz += ChunkSize;
        }
        else if (lz >= ChunkSize)
        {
            if (cache.PosZ == null)
                return false;

            target = cache.PosZ;
            lz -= ChunkSize;
        }

        var nInfo = target.GetInfoAt(lx, ly, lz);
        return target.GetTypeAt(lx, ly, lz) != BlockType.Air && !nInfo.IsTransparent;
    }
    
    // Per-chunk billboard instance data
    public Dictionary<BlockType, VertexBillboardInstance[]> GenerateBillboardInstances(Chunk chunk)
    {
        var grouped = new Dictionary<BlockType, List<VertexBillboardInstance>>();

        for (int x = 0; x < ChunkSize; x++)
        for (int y = 0; y < ChunkSize; y++)
        for (int z = 0; z < ChunkSize; z++)
        {
            var type = chunk.GetTypeAt(x, y, z);
            var info = chunk.GetInfoAt(x, y, z);

            if (type == BlockType.Air || !info.IsBillboard)
                continue;

            var localCenter = new Vector3(x + 0.5f, y + 0.0f, z + 0.5f);
            var worldCenter = chunk.Position.ToVector3() + localCenter;

            long seed = TerrainGeneration.Seed
                        ^ (x * 73428767L)
                        ^ (y * 9127841L)
                        ^ (z * 192837465L);

            var rng = new SeededRandom(seed);
            float angle = rng.NextFloat() * MathF.PI;

            float light = FaceUtils.GetFaceLightFlat(chunk, new Vector3Int(x, y, z), 5);
            var color = FaceUtils.ToColor(light);

            // Optional: different size per type (keep same if unsure)
            var size = type == BlockType.GrassBlades
                ? new Vector2(0.5f, 1.0f)
                : new Vector2(0.5f, 0.8f);

            var inst = new VertexBillboardInstance
            {
                Center = worldCenter,
                Angle = angle,
                Size = size,
                Color = color
            };

            if (!grouped.TryGetValue(type, out var list))
            {
                list = new List<VertexBillboardInstance>();
                grouped[type] = list;
            }

            list.Add(inst);
        }

        var result = new Dictionary<BlockType, VertexBillboardInstance[]>();

        foreach (var kv in grouped)
        {
            result[kv.Key] = kv.Value.ToArray();
        }

        return result;
    }
    
    // Create shared crossed-quad mesh (two quads, 8 verts, 12 indices)
    private void EnsureBillboardMesh()
    {
        if (_billboardVB != null)
            return;

        var gd = Engine.Graphics;

        var verts = new VertexPositionTexture[8];

        float halfW = 0.5f;
        float height = 1.0f;

        // Quad A aligned to X axis (local space)
        verts[0] = new VertexPositionTexture(new Vector3(-halfW, 0f, 0f),    new Vector2(0f, 1f));
        verts[1] = new VertexPositionTexture(new Vector3(-halfW, height, 0f),new Vector2(0f, 0f));
        verts[2] = new VertexPositionTexture(new Vector3( halfW, height, 0f),new Vector2(1f, 0f));
        verts[3] = new VertexPositionTexture(new Vector3( halfW, 0f, 0f),    new Vector2(1f, 1f));

        // Quad B aligned to Z axis (local space)
        verts[4] = new VertexPositionTexture(new Vector3(0f, 0f, -halfW),    new Vector2(0f, 1f));
        verts[5] = new VertexPositionTexture(new Vector3(0f, height, -halfW),new Vector2(0f, 0f));
        verts[6] = new VertexPositionTexture(new Vector3(0f, height,  halfW),new Vector2(1f, 0f));
        verts[7] = new VertexPositionTexture(new Vector3(0f, 0f,  halfW),    new Vector2(1f, 1f));

        _billboardVB = new VertexBuffer(gd, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
        _billboardVB.SetData(verts);

        var indices = new short[]
        {
            // Quad A
            0, 1, 2, 0, 2, 3,
            // Quad B
            4, 5, 6, 4, 6, 7
        };

        _billboardIB = new IndexBuffer(gd, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _billboardIB.SetData(indices);
    }
    
    // Fast scan of chunk's block types to see if any are transparent
    private static bool chunkContainsTransparent(Chunk chunk)
    {
        var types = chunk.BlockTypes;

        for (int i = 0; i < types.Length; i++)
        {
            var type = (BlockType)types[i];
            var info = BlockData.Prefabs[type];

            if (info.IsTransparent && type != BlockType.Air)
                return true;
        }

        return false;
    }
    
    private static Vector3[] makeBillboardQuad(Vector3 center, Vector3 right, float halfWidth, float height)
    {
        Vector3 up = Vector3.Up * height;

        Vector3 pL = center - (right * halfWidth);
        Vector3 pR = center + (right * halfWidth);

        // Order: bottom-left, top-left, top-right, bottom-right
        return new Vector3[]
        {
            pL,
            pL + up,
            pR + up,
            pR
        };
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

[StructLayout(LayoutKind.Sequential)]
public struct VertexBillboardInstance : IVertexType
{
    public Vector3 Center;
    public float Angle;
    public Vector2 Size;   // X = half width, Y = height
    public Color Color;

    public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration
    (
        new VertexElement(0,  VertexElementFormat.Vector3, VertexElementUsage.Position,          1), // POSITION1
        new VertexElement(12, VertexElementFormat.Single,  VertexElementUsage.TextureCoordinate, 1), // TEXCOORD1
        new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 2), // TEXCOORD2
        new VertexElement(24, VertexElementFormat.Color,   VertexElementUsage.Color,             1)  // COLOR1
    );

    VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
}