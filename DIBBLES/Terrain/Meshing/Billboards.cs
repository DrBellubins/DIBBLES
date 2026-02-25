using DIBBLES.Effects;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain.Meshing;

public struct Billboard
{
    public VertexBuffer VB;
    public BlockType Type;

    public Billboard(VertexBuffer vb, BlockType type)
    {
        VB = vb;
        Type = type;
    }
}

public class Billboards
{
    public VertexBuffer BillboardVB;
    public IndexBuffer BillboardIB;
    
    //public Dictionary<Vector3Int, Dictionary<BlockType, (VertexBuffer InstanceBuffer, int InstanceCount)>> BillboardBatches = new();
    
    public Dictionary<Vector3Int, Billboard> BillboardBatches = new();
    
    public void Draw()
    {
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
    
        EffectParams.SetVector3(shader, "CameraPos", GameScene.PlayerCharacter.Camera.Position.ToVector3());
        EffectParams.SetFloat(shader, "CameraNear", GameScene.PlayerCharacter.Camera.NearPlane);
        EffectParams.SetFloat(shader, "CameraFar", GameScene.PlayerCharacter.Camera.FarPlane);
    
        EffectParams.SetFloat(shader, "FogNear", FogEffect.FogNear);
        EffectParams.SetFloat(shader, "FogFar", FogEffect.FogFar);
        EffectParams.SetVector4(shader, "FogColor", FogEffect.FogColor());

        EffectParams.SetVector3(shader, "AmbientLightColor", RenderEngine.AmbientLightColor.ToVector3());
        
        EffectParams.SetFloat(shader, "Time", Time.time);
    
        foreach (var kv in BillboardBatches)
        {
            var chunkPos = kv.Key;
            var type = kv.Value.Type;
            var vertexBuffer = kv.Value.VB;
            
            if (!ActiveViewChunks.Contains(chunkPos))
                continue;

            Vector3 center = chunkPos.ToVector3() + new Vector3(ChunkSize * 0.5f);

            if (!GameScene.PlayerCharacter.Camera.InFrustum(center, TerrainMesh.FrustumCullRadius))
                continue;

            VertexBuffer instanceBuffer = vertexBuffer;

            if (instanceBuffer == null || instanceBuffer.VertexCount == 0)
                continue;

            EffectParams.SetVector4(shader, "UVRect", getBillboardUVRect(type));

            graphics.SetVertexBuffers(
                new VertexBufferBinding(BillboardVB, 0, 0),
                new VertexBufferBinding(instanceBuffer, 0, 1));

            graphics.Indices = BillboardIB;

            var billboardTech = shader.Techniques["BillboardInstanced"];

            if (billboardTech != null && shader.CurrentTechnique != billboardTech)
                shader.CurrentTechnique = billboardTech;

            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphics.DrawInstancedPrimitives(
                    PrimitiveType.TriangleList, 0, 0, 8, 0, 4, instanceBuffer.VertexCount);
            }
        }
        
        graphics.SetVertexBuffer(null);
        graphics.Indices = null;
    }
    
    // Generates per-chunk billboard instance data
    public Dictionary<Vector3Int, Dictionary<BlockType, VertexBillboardInstance[]>> Generate(Chunk chunk)
    {
        Dictionary<BlockType, List<VertexBillboardInstance>> grouped = new();

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

            var inst = new VertexBillboardInstance
            {
                Center = worldCenter,
                Angle = angle,
                Color = color
            };

            if (!grouped.TryGetValue(type, out var list))
            {
                list = new List<VertexBillboardInstance>();
                grouped[type] = list;
            }

            list.Add(inst);
        }

        var perChunk = new Dictionary<BlockType, VertexBillboardInstance[]>();

        foreach (var kv in grouped)
            perChunk[kv.Key] = kv.Value.ToArray();

        var result = new Dictionary<Vector3Int, Dictionary<BlockType, VertexBillboardInstance[]>>();
        result[chunk.Position] = perChunk;

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
    
    private static Vector4 getBillboardUVRect(BlockType type)
    {
        // Choose faceIdx=0 (front) since billboard textures aren’t per-face,
        // but your atlas generator stored entries for each face.
        RectangleF rect;
    
        if (!BlockData.AtlasUVs.TryGetValue((type, 0), out rect))
            rect = new RectangleF(0f, 0f, 1f, 1f);

        return new Vector4(rect.X, rect.Y, rect.Width, rect.Height);
    }
}