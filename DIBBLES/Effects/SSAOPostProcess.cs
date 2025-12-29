using DIBBLES.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects
{
    public class SSAOPostProcess : PostProcessingEffect
    {
        private Effect effect;
        private VertexBuffer vertexBuffer;
        private IndexBuffer indexBuffer;
        
        public RenderTarget2D SSAOTarget;
        public RenderTarget2D SSAOBlurTarget;

        public override void Start(int width, int height)
        {
            base.Start(width, height);

            effect = Engine.Instance.Content.Load<Effect>("Shaders/SSAOPostProcess");

            // Allocate intermediate AO buffers
            SSAOTarget = new RenderTarget2D(Graphics, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            SSAOBlurTarget = new RenderTarget2D(Graphics, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            
            var verts = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1f, -1f, 0f), new Vector2(0f, 1f)),
                new VertexPositionTexture(new Vector3(-1f,  1f, 0f), new Vector2(0f, 0f)),
                new VertexPositionTexture(new Vector3( 1f,  1f, 0f), new Vector2(1f, 0f)),
                new VertexPositionTexture(new Vector3( 1f, -1f, 0f), new Vector2(1f, 1f))
            };

            vertexBuffer = new VertexBuffer(Graphics, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
            vertexBuffer.SetData(verts);

            var indices = new short[] { 0, 1, 2, 0, 2, 3 };

            indexBuffer = new IndexBuffer(Graphics, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            indexBuffer.SetData(indices);
        }

        public override void DrawStart()
        {
            // Minimal state
            Graphics.BlendState = BlendState.Opaque;
            Graphics.DepthStencilState = DepthStencilState.None;
            Graphics.RasterizerState = RasterizerState.CullNone;
            Graphics.SamplerStates[0] = SamplerState.PointClamp;

            // Required params for pure depth SSAO
            effect.Parameters["ScreenSize"]?.SetValue(new Vector2(Engine.ScreenWidth, Engine.ScreenHeight));
            
            effect.Parameters["ColorTex"]?.SetValue(GameScene.BackBuffer);
            effect.Parameters["DepthTex"]?.SetValue(GameScene.DepthBuffer);

            // Fullscreen quad
            Graphics.SetVertexBuffer(vertexBuffer);
            Graphics.Indices = indexBuffer;

            // Pass 1: SSAO into _aoRT1
            Graphics.SetRenderTarget(SSAOTarget);
            Graphics.Clear(Color.White); // AO=1 baseline
            effect.CurrentTechnique = effect.Techniques["SSAO"];

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            // Pass 2: Horizontal bilateral blur into _aoRT2
            Graphics.SetRenderTarget(SSAOBlurTarget);
            Graphics.Clear(Color.White);
            effect.Parameters["AOTex"]?.SetValue(SSAOTarget);
            effect.CurrentTechnique = effect.Techniques["BlurH"];

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }

            // Pass 3: Vertical bilateral blur into OutputBuffer
            Graphics.SetRenderTarget(OutputBuffer);
            Graphics.Clear(Color.Transparent);
            effect.Parameters["AOTex"]?.SetValue(SSAOBlurTarget);
            effect.CurrentTechnique = effect.Techniques["BlurV"];

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        }

        public override void DrawEnd()
        {
            Graphics.SetRenderTarget(null);
        }

        public override void Dispose()
        {
            base.Dispose();
            SSAOTarget?.Dispose();
            SSAOBlurTarget?.Dispose();
            SSAOTarget = null;
            SSAOBlurTarget = null;
        }
    }
}