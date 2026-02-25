using DIBBLES.Gameplay;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Effects;

public class Skybox
{
    public Texture2D SunTexture;
    public Texture2D MoonTexture;
    private Effect skyboxShader;
    private VertexBuffer vb;
    private IndexBuffer ib;

    public void Initialize(Texture2D sunTex, Texture2D moonTex, Effect shader)
    {
        SunTexture = sunTex;
        MoonTexture = moonTex;
        skyboxShader = shader;
        createDomeMesh();
    }

    public void Draw()
    {
        var gd = Engine.Graphics;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.DepthRead;
        gd.RasterizerState = RasterizerState.CullNone;

        gd.SetVertexBuffer(vb);
        gd.Indices = ib;

        skyboxShader.SetValue("View", GameScene.PlayerCharacter.Camera.View);
        skyboxShader.SetValue("Projection", GameScene.PlayerCharacter.Camera.Projection);

        skyboxShader.SetValue("SkyColor", RenderEngine.CurrentSkyColor.ToVector3());
        skyboxShader.SetValue("DaySkyColor", RenderEngine.DaySkyColor.ToVector3());
        skyboxShader.SetValue("DawnDuskSkyColor", RenderEngine.DawnDuskPeakColor.ToVector3());
        skyboxShader.SetValue("NightSkyColor", RenderEngine.NightSkyColor.ToVector3());

        skyboxShader.SetValue("SunTexture", SunTexture);
        skyboxShader.SetValue("MoonTexture", MoonTexture);
        
        skyboxShader.SetValue("TimeOfDay", GameScene.DayNightCycle.TimeOfDay);

        // Sun: simple path (overhead at noon, below at midnight)
        float sunAngle = MathHelper.TwoPi * (GameScene.DayNightCycle.TimeOfDay - 6f) / 24f;
        Vector3 sunDir = Vector3.Transform(Vector3.Down, Matrix.CreateFromAxisAngle(Vector3.Forward, sunAngle));

        skyboxShader.SetValue("SunDirection", sunDir);

        float moonAngle = sunAngle + MathF.PI;
        Vector3 moonDir = Vector3.Transform(Vector3.Down, Matrix.CreateFromAxisAngle(Vector3.Forward, moonAngle));

        skyboxShader.SetValue("MoonDirection", moonDir);

        foreach (var pass in skyboxShader.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vb.VertexCount, 0, ib.IndexCount / 3);
        }
    }

    private void createDomeMesh(int slices = 32, int stacks = 16, float radius = 80f)
    {
        List<Vector3> verts = new();
        List<ushort> inds = new();

        for (int stack = 0; stack <= stacks; stack++)
        {
            float phi = MathHelper.PiOver2 * (stack / (float)stacks); // 0..Pi/2 quarter dome
            float y = MathF.Sin(phi);
            float r = MathF.Cos(phi);

            for (int slice = 0; slice <= slices; slice++)
            {
                float theta = MathHelper.TwoPi * (slice / (float)slices);
                float x = r * MathF.Cos(theta);
                float z = r * MathF.Sin(theta);
                verts.Add(new Vector3(x * radius, y * radius, z * radius));
            }
        }

        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                int baseIdx = stack * (slices + 1) + slice;
                inds.Add((ushort)baseIdx);
                inds.Add((ushort)(baseIdx + slices + 1));
                inds.Add((ushort)(baseIdx + 1));
                inds.Add((ushort)(baseIdx + 1));
                inds.Add((ushort)(baseIdx + slices + 1));
                inds.Add((ushort)(baseIdx + slices + 2));
            }
        }

        vb = new VertexBuffer(Engine.Graphics, typeof(VertexPosition), verts.Count, BufferUsage.WriteOnly);
        vb.SetData(verts.Select(v => new VertexPosition(v)).ToArray());
        ib = new IndexBuffer(Engine.Graphics, IndexElementSize.SixteenBits, inds.Count, BufferUsage.WriteOnly);
        ib.SetData(inds.ToArray());
    }
}