using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using DIBBLES.Effects;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Terrain.Meshing;

using static DIBBLES.Terrain.TerrainGeneration;
using static DIBBLES.Terrain.Meshing.Helpers;

namespace DIBBLES.Terrain;

public class TerrainMesh
{
    public const bool SmoothLighting = true;
    public const bool UseGreedyMeshing = false;

    public const bool Fullbright = true;
    
    public const bool GreedyRespectAntiTileFlips = false;
    public const bool NonGreedyRespectAntiTileFlips = true;
    
    public const float EmissiveStrength = 5.0f;
    
    public static float FrustumCullRadius = (float)(MathF.Sqrt(3f) * 0.5f * ChunkSize);

    public Dictionary<Vector3Int, RuntimeModel> OpaqueModels = new();
    public Dictionary<Vector3Int, RuntimeModel> TransparentModels = new();

    // Meshing extension classes
    public MeshDataGeneration MeshDataGen = new();
    public Billboards BillboardGen = new();
    
    // Main-thread mesh upload queue
    public readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> MeshUploadQueue = new(); // Opaque
    public readonly ConcurrentQueue<(Vector3Int chunkPos, MeshData meshData)> TMeshUploadQueue = new(); // Transparent
    
    public readonly ConcurrentQueue<(Vector3Int chunkPos, 
        Dictionary<BlockType, VertexBillboardInstance[]> instancesByType)> BillboardUploadQueue = new(); // Billboards
    
    public void Generate(Chunk chunk)
    {
        var meshData = MeshDataGen.Generate(chunk, false);
        MeshUploadQueue.Enqueue((chunk.Position, meshData));

        if (ChunkContainsTransparent(chunk))
        {
            var tMeshData = MeshDataGen.Generate(chunk, true);
            TMeshUploadQueue.Enqueue((chunk.Position, tMeshData));
        }
        
        var billboardInstances = BillboardGen.Generate(chunk);

        foreach (var kv in billboardInstances)
            BillboardUploadQueue.Enqueue((kv.Key, kv.Value));
    }
    
    public void DrawOpaque(HashSet<Vector3Int> renderSet)
    {
        foreach (var chunkPos in renderSet)
        {
            if (!OpaqueModels.TryGetValue(chunkPos, out var oModel) || oModel == null)
                continue;
            
            // Chunk center in world space
            Vector3 center = chunkPos.ToVector3() + new Vector3(ChunkSize * 0.5f);

            // Skip if chunk is outside view frustum
            if (!GameScene.PlayerCharacter.Camera.InFrustum(center, FrustumCullRadius))
                continue;
            
            // oModel.Value is a RuntimeModel
            if (oModel != null)
            {
                var world = Matrix.CreateTranslation(chunkPos.ToVector3());
                var shader = terrainShader;
                
                var view = GameScene.PlayerCharacter.Camera.View;
                var projection = GameScene.PlayerCharacter.Camera.Projection;
                
                EffectParams.SetTexture(shader, "AtlasTex", BlockData.TextureAtlas);
                EffectParams.SetTexture(shader, "EmissiveAtlasTex", BlockData.EmissiveTextureAtlas);
                
                EffectParams.SetInt(shader, "UseGreedyMeshing", UseGreedyMeshing ? 1 : 0);
                
                EffectParams.SetMatrix(shader, "World", world);
                EffectParams.SetMatrix(shader, "View", view);
                EffectParams.SetMatrix(shader, "Projection", projection);
                
                EffectParams.SetVector3(shader, "CameraPos",GameScene.PlayerCharacter.Camera.Position.ToVector3());
                EffectParams.SetFloat(shader, "CameraNear", GameScene.PlayerCharacter.Camera.NearPlane);
                EffectParams.SetFloat(shader, "CameraFar", GameScene.PlayerCharacter.Camera.FarPlane);
                
                EffectParams.SetFloat(shader, "EmissiveStrength", EmissiveStrength);
                EffectParams.SetFloat(shader, "FogNear", FogEffect.FogNear);
                EffectParams.SetFloat(shader, "FogFar", FogEffect.FogFar);
                EffectParams.SetVector4(shader, "FogColor", FogEffect.FogColor());
                
                // Ensure technique is set
                var terrainTech = shader.Techniques["Terrain"];
                
                if (terrainTech != null && shader.CurrentTechnique != terrainTech)
                    shader.CurrentTechnique = terrainTech;
                
                foreach (var pass in shader.CurrentTechnique.Passes)
                    pass.Apply();
                
                oModel.DrawRaw();
            }
        }
    }

    public void DrawTransparent(HashSet<Vector3Int> renderSet)
    {
        var graphics = Engine.Graphics;
        
        graphics.BlendState = BlendState.NonPremultiplied;
        graphics.DepthStencilState = DepthStencilState.DepthRead;
        
        graphics.SamplerStates[0] = SamplerState.PointClamp; // Base atlas
        graphics.SamplerStates[1] = SamplerState.PointClamp; // Emissive atlas
        
        // Disable culling so billboards render double-sided
        graphics.RasterizerState = RasterizerState.CullCounterClockwise;
        
        foreach (var chunkPos in renderSet)
        {
            if (!OpaqueModels.TryGetValue(chunkPos, out var model) || model == null)
                continue;

            var world = Matrix.CreateTranslation(chunkPos.ToVector3());
            var shader = terrainShader;

            var view = GameScene.PlayerCharacter.Camera.View;
            var projection = GameScene.PlayerCharacter.Camera.Projection;

            EffectParams.SetTexture(shader, "AtlasTex", BlockData.TextureAtlas);
            EffectParams.SetTexture(shader, "EmissiveAtlasTex", BlockData.EmissiveTextureAtlas);

            EffectParams.SetInt(shader, "UseGreedyMeshing", UseGreedyMeshing ? 1 : 0);

            EffectParams.SetMatrix(shader, "World", world);
            EffectParams.SetMatrix(shader, "View", view);
            EffectParams.SetMatrix(shader, "Projection", projection);

            EffectParams.SetVector3(shader, "CameraPos", GameScene.PlayerCharacter.Camera.Position.ToVector3());
            EffectParams.SetFloat(shader, "CameraNear", GameScene.PlayerCharacter.Camera.NearPlane);
            EffectParams.SetFloat(shader, "CameraFar", GameScene.PlayerCharacter.Camera.FarPlane);

            EffectParams.SetFloat(shader, "EmissiveStrength", EmissiveStrength);
            EffectParams.SetFloat(shader, "FogNear", FogEffect.FogNear);
            EffectParams.SetFloat(shader, "FogFar", FogEffect.FogFar);
            EffectParams.SetVector4(shader, "FogColor", FogEffect.FogColor());

            var terrainTech = shader.Techniques["Terrain"];

            if (terrainTech != null && shader.CurrentTechnique != terrainTech)
                shader.CurrentTechnique = terrainTech;

            foreach (var pass in shader.CurrentTechnique.Passes)
                pass.Apply();

            model.DrawRaw();
        }
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
                var meshData = MeshDataGen.Generate(neighborChunk, false);
                OpaqueModels[neighborChunkPos] = UploadMesh(meshData);

                // Remesh transparent
                var tMeshData = MeshDataGen.Generate(neighborChunk, true);
                TransparentModels[neighborChunkPos] = UploadMesh(tMeshData);
            }
        }
    }
    
    // Main-thread only: allocates Raylib Mesh, uploads data, returns Model
    public RuntimeModel UploadMesh(MeshData data)
    {
        var graphicsDevice = Engine.Graphics;

        if (data.VertexCount == 0 || data.Vertices == null || data.Indices == null || data.Indices.Length == 0)
            return null;

        var verts = new VertexPositionNormalTextureColor[data.VertexCount];

        for (int i = 0; i < data.VertexCount; i++)
        {
            var pos = new Vector3(data.Vertices[i * 3 + 0], data.Vertices[i * 3 + 1], data.Vertices[i * 3 + 2]);
            var norm = new Vector3(data.Normals[i * 3 + 0], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]);
            var tex = new Vector2(data.TexCoords[i * 2 + 0], data.TexCoords[i * 2 + 1]);
            var color = new Color(data.Colors[i * 4 + 0], data.Colors[i * 4 + 1], data.Colors[i * 4 + 2], data.Colors[i * 4 + 3]);

            Vector4 uvRect = Vector4.Zero;
            
            if (data.UVRects != null && data.UVRects.Length >= (i + 1) * 4)
            {
                uvRect = new Vector4(
                    data.UVRects[i * 4 + 0],
                    data.UVRects[i * 4 + 1],
                    data.UVRects[i * 4 + 2],
                    data.UVRects[i * 4 + 3]);
            }

            Vector4 uvBasis = Vector4.Zero;
            if (data.UVBasis != null && data.UVBasis.Length >= (i + 1) * 4)
            {
                uvBasis = new Vector4(
                    data.UVBasis[i * 4 + 0],
                    data.UVBasis[i * 4 + 1],
                    data.UVBasis[i * 4 + 2],
                    data.UVBasis[i * 4 + 3]);
            }
            
            Vector4 emisRect = Vector4.Zero;
            Vector4 emisBasis = Vector4.Zero;

            if (data.EmissiveUVRects != null && data.EmissiveUVRects.Length >= (i + 1) * 4)
            {
                emisRect = new Vector4(
                    data.EmissiveUVRects[i * 4 + 0],
                    data.EmissiveUVRects[i * 4 + 1],
                    data.EmissiveUVRects[i * 4 + 2],
                    data.EmissiveUVRects[i * 4 + 3]);
            }

            if (data.EmissiveUVBasis != null && data.EmissiveUVBasis.Length >= (i + 1) * 4)
            {
                emisBasis = new Vector4(
                    data.EmissiveUVBasis[i * 4 + 0],
                    data.EmissiveUVBasis[i * 4 + 1],
                    data.EmissiveUVBasis[i * 4 + 2],
                    data.EmissiveUVBasis[i * 4 + 3]);
            }

            if (emisRect == Vector4.Zero && uvRect != Vector4.Zero)
            {
                emisRect = uvRect;
                emisBasis = uvBasis;
            }

            verts[i] = new VertexPositionNormalTextureColor(
                pos, norm, tex, color,
                uvRect, uvBasis,
                emisRect, emisBasis
            );
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
            Shader = null, // We reference the global TerrainGeneration.terrainShader instead!
            Texture = BlockData.TextureAtlas
        };
    }
}