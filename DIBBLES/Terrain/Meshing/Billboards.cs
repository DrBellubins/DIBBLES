using DIBBLES.Effects;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Meshing;

public class Billboards
{
    public VertexBuffer BillboardVB;
    public IndexBuffer BillboardIB;
    
    public Dictionary<Vector3Int, Dictionary<BlockType, (VertexBuffer InstanceBuffer, int InstanceCount)>> BillboardBatches = new();
    
    public void Draw()
    {
        Debug.TimerStart("Terrain billboard");
        EnsureBillboardMesh();
    
        var graphics = Engine.Graphics;
    
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.Default;
        graphics.SamplerStates[0] = SamplerState.PointClamp;
        graphics.RasterizerState = RasterizerState.CullNone;
    
        var shader = billboardShader;
        var view = GameScene.PlayerCharacter.Camera.View;
        var projection = GameScene.PlayerCharacter.Camera.Projection;
    
        EffectParams.SetTexture(shader, "AtlasTex", BlockData.TextureAtlas);
        
        EffectParams.SetMatrix(shader, "View", view);
        EffectParams.SetMatrix(shader, "Projection", projection);
                
        EffectParams.SetVector3(shader, "CameraPos",GameScene.PlayerCharacter.Camera.Position.ToVector3());
        EffectParams.SetFloat(shader, "CameraNear", GameScene.PlayerCharacter.Camera.NearPlane);
        EffectParams.SetFloat(shader, "CameraFar", GameScene.PlayerCharacter.Camera.FarPlane);
                
        EffectParams.SetFloat(shader, "FogNear", FogEffect.FogNear);
        EffectParams.SetFloat(shader, "FogFar", FogEffect.FogFar);
        EffectParams.SetVector4(shader, "FogColor", FogEffect.FogColor());
    
        EffectParams.SetFloat(shader, "Time", Time.time);
        
        foreach (var kv in BillboardBatches)
        {
            // kv.Key is the chunk position
            Vector3 center = kv.Key.ToVector3() + new Vector3(ChunkSize * 0.5f);

            // Skip if chunk is outside view frustum
            if (!GameScene.PlayerCharacter.Camera.InFrustum(center, TerrainMesh.FrustumCullRadius))
                continue;
            
            var batchesByType = kv.Value;
    
            foreach (var typeBatch in batchesByType)
            {
                var type = typeBatch.Key;
                var batch = typeBatch.Value;
    
                if (batch.InstanceBuffer == null || batch.InstanceCount <= 0)
                    continue;
    
                var rect = BlockData.AtlasUVs[(type, 0)];
                
                EffectParams.SetVector4(billboardShader, "UVRect",
                    new Vector4(rect.X, rect.Y, rect.Width, rect.Height));
    
                graphics.SetVertexBuffers
                (
                    new VertexBufferBinding(BillboardVB, 0, 0),
                    new VertexBufferBinding(batch.InstanceBuffer, 0, 1)
                );
    
                graphics.Indices = BillboardIB;
    
                foreach (var pass in shader.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    graphics.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 8, 0, 4, batch.InstanceCount);
                }
            }
        }
    
        graphics.SetVertexBuffer(null);
        graphics.Indices = null;
        
        Debug.TimerStop();
    }
    
    // Generates per-chunk billboard instance data
    public Dictionary<BlockType, VertexBillboardInstance[]> Generate(Chunk chunk)
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

            long seed = Seed
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
            result[kv.Key] = kv.Value.ToArray();

        return result;
    }
    
    // Create shared crossed-quad mesh (two quads, 8 verts, 12 indices)
    public void EnsureBillboardMesh()
    {
        if (BillboardVB != null)
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

        BillboardVB = new VertexBuffer(gd, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
        BillboardVB.SetData(verts);

        var indices = new short[]
        {
            // Quad A
            0, 1, 2, 0, 2, 3,
            // Quad B
            4, 5, 6, 4, 6, 7
        };

        BillboardIB = new IndexBuffer(gd, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        BillboardIB.SetData(indices);
    }
}