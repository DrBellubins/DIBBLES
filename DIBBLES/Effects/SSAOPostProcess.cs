using DIBBLES.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects
{
    public class SSAOPostProcess : PostProcessingEffect
    {
        private Effect effect;

        private Texture2D blueNoiseTex;
        
        private VertexBuffer vertexBuffer;
        private IndexBuffer indexBuffer;
        
        public RenderTarget2D SSAOTarget;
        public RenderTarget2D SSAOBlurTarget;

        public override void Start(int width, int height)
        {
            base.Start(width, height);

            effect = Engine.Instance.Content.Load<Effect>("Shaders/SSAOPostProcess");
            blueNoiseTex = Engine.Instance.Content.Load<Texture2D>("Textures/BlueNoise");

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
            // State
            Graphics.BlendState = BlendState.Opaque;
            Graphics.DepthStencilState = DepthStencilState.None;
            Graphics.RasterizerState = RasterizerState.CullNone;
        
            // Set G-buffer textures
            //effect.Parameters["ColorTex"]?.SetValue(GameScene.BackBuffer);
            effect.Parameters["DepthTex"]?.SetValue(GameScene.DepthBuffer);
            effect.Parameters["NormalTex"]?.SetValue(GameScene.NormalBuffer);
            effect.Parameters["RandomTex"]?.SetValue(blueNoiseTex);
        
            // Camera params
            var proj = GameScene.PlayerCharacter.Camera.Projection;
            var invProj = Matrix.Invert(proj);
        
            effect.Parameters["Projection"]?.SetValue(proj);
            effect.Parameters["InvProjection"]?.SetValue(invProj);
            effect.Parameters["CameraNear"]?.SetValue(GameScene.PlayerCharacter.Camera.NearPlane);
            effect.Parameters["CameraFar"]?.SetValue(GameScene.PlayerCharacter.Camera.FarPlane);
            effect.Parameters["ScreenSize"]?.SetValue(new Vector2(Engine.ScreenWidth, Engine.ScreenHeight));
        
            float fovRadians = MathHelper.ToRadians(GameScene.PlayerCharacter.Camera. Fov);
            float tanHalfFov = (float)Math.Tan(fovRadians * 0.5f);
            float aspectRatio = (float)Engine.ScreenWidth / Engine. ScreenHeight;

            effect.Parameters["TanHalfFovY"]?.SetValue(tanHalfFov);
            effect.Parameters["AspectRatio"]?.SetValue(aspectRatio);
            
            // Tile blue-noise by pixel size
            var noiseScale = new Vector2(
                (float)Engine.ScreenWidth / blueNoiseTex.Width,
                (float)Engine.ScreenHeight / blueNoiseTex.Height
            );
            
            effect.Parameters["NoiseScale"]?.SetValue(noiseScale);
        
            // SSAO tuning (view-space)
            effect.Parameters["radius"]?.SetValue(0.75f);       // try 0.5–1.0
            effect.Parameters["bias"]?.SetValue(0.05f);         // try 0.02–0.08
            effect.Parameters["total_strength"]?.SetValue(1.0f);
            effect.Parameters["base_ao"]?.SetValue(0.0f);
        
            // Fullscreen quad
            Graphics.SetVertexBuffer(vertexBuffer);
            Graphics.Indices = indexBuffer;
        
            // Pass 1: SSAO
            Graphics.SetRenderTarget(SSAOTarget);
            Graphics.Clear(Color.White);
            effect.CurrentTechnique = effect.Techniques["SSAO"];
        
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        
            // Pass 2: BlurH
            Graphics.SetRenderTarget(SSAOBlurTarget);
            Graphics.Clear(Color.White);
            effect.Parameters["AOTex"]?.SetValue(SSAOTarget);
            effect.CurrentTechnique = effect.Techniques["BlurH"];
        
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        
            // Pass 3: BlurV + composite
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