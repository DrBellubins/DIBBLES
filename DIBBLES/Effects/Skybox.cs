using DIBBLES.Gameplay;
using DIBBLES.Scenes;
using DIBBLES.Systems;
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
   
           skyboxShader.Parameters["View"].SetValue(GameScene.PlayerCharacter.Camera.View);
           skyboxShader.Parameters["Projection"].SetValue(GameScene.PlayerCharacter.Camera.Projection);
   
           skyboxShader.Parameters["SkyColor"].SetValue(RenderEngine.CurrentSkyColor.ToVector4());
           skyboxShader.Parameters["DaySkyColor"].SetValue(RenderEngine.DaySkyColor.ToVector4());
           skyboxShader.Parameters["DawnDuskSkyColor"].SetValue(RenderEngine.DawnDuskPeakColor.ToVector4());
           skyboxShader.Parameters["NightSkyColor"].SetValue(RenderEngine.NightSkyColor.ToVector4());
   
           skyboxShader.Parameters["SunTexture"].SetValue(SunTexture);
           skyboxShader.Parameters["MoonTexture"].SetValue(MoonTexture);
           skyboxShader.Parameters["TimeOfDay"].SetValue(GameScene.DayNightCycle.TimeOfDay);
   
           // Sun: simple path (overhead at noon, below at midnight)
           float sunAngle = MathHelper.TwoPi * (GameScene.DayNightCycle.TimeOfDay - 6f) / 24f;
           Vector3 sunDir = Vector3.Transform(Vector3.Down, Matrix.CreateFromAxisAngle(Vector3.Forward, sunAngle));
   
           skyboxShader.Parameters["SunDirection"].SetValue(sunDir);
   
           float moonAngle = sunAngle + MathF.PI;
           Vector3 moonDir = Vector3.Transform(Vector3.Down, Matrix.CreateFromAxisAngle(Vector3.Forward, moonAngle));
   
           skyboxShader.Parameters["MoonDirection"].SetValue(moonDir);
   
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