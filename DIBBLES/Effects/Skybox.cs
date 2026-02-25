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
        var graphics = Engine.Graphics;
        graphics.BlendState = BlendState.Opaque;
        graphics.DepthStencilState = DepthStencilState.DepthRead;
        graphics.RasterizerState = RasterizerState.CullNone;

        graphics.SetVertexBuffer(vb);
        graphics.Indices = ib;
        
        skyboxShader.SetValue("World", Matrix.Identity);
        
        var view = GameScene.PlayerCharacter.Camera.View;

        // Remove translation: use only rotation for the skybox view!
        view.Translation = Vector3.Zero;
        
        skyboxShader.SetValue("View", view);
        skyboxShader.SetValue("Projection", GameScene.PlayerCharacter.Camera.Projection);

        skyboxShader.SetValue("SkyZenithColor", DayNightCycle.ZenithColor.ToVector3());
        skyboxShader.SetValue("SkyHorizonColor", DayNightCycle.HorizonColor.ToVector3());

        skyboxShader.SetValue("SunTexture", SunTexture);
        skyboxShader.SetValue("MoonTexture", MoonTexture);
        
        skyboxShader.SetValue("TimeOfDay", GameScene.TimeCycle.TimeOfDay);

        // Sun: simple path (overhead at noon, below at midnight)
        float sunAngle = MathHelper.TwoPi * (-(GameScene.TimeCycle.TimeOfDay - 6f)) / 24f;
        
        // Instead of rotating around Forward (Z), rotate around Right (X)
        Vector3 sunDir = Vector3.Transform(Vector3.Forward, Matrix.CreateFromAxisAngle(Vector3.Right, sunAngle));

        skyboxShader.SetValue("SunDirection", sunDir);

        float moonAngle = sunAngle + MathF.PI;
        Vector3 moonDir = Vector3.Transform(Vector3.Forward, Matrix.CreateFromAxisAngle(Vector3.Right, moonAngle));

        skyboxShader.SetValue("MoonDirection", moonDir);

        foreach (var pass in skyboxShader.CurrentTechnique.Passes)
        {
            pass.Apply();
            graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vb.VertexCount, 0, ib.IndexCount / 3);
        }
    }

    private void createDomeMesh(int slices = 32, int stacks = 16, float radius = 80f)
    {
        List<Vector3> verts = new();
        List<ushort> inds = new();

        // Change: Full sphere (phi = 0..Pi), not just Pi/2 (quarter dome)
        for (int stack = 0; stack <= stacks; stack++)
        {
            // Old (dome): float phi = MathHelper.PiOver2 * (stack / (float)stacks);
            // New (sphere): phi = 0..Pi
            float phi = MathHelper.Pi * (stack / (float)stacks); // 0..Pi

            float y = MathF.Cos(phi);   // y up: cos(phi)
            float r = MathF.Sin(phi);   // radius in XZ: sin(phi)

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
                // Two triangles per quad face of the sphere
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