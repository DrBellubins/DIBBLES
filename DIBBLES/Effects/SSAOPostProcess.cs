using DIBBLES.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects
{
    public class SSAOPostProcess : PostProcessingEffect
    {
        private Effect _effect;
        private VertexBuffer _vb;
        private IndexBuffer _ib;
        
        private RenderTarget2D _aoRT1;
        private RenderTarget2D _aoRT2;

        public override void Start(int width, int height)
        {
            base.Start(width, height);

            _effect = Engine.Instance.Content.Load<Effect>("Shaders/SSAOPostProcess");

            // Allocate intermediate AO buffers
            _aoRT1 = new RenderTarget2D(Graphics, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            _aoRT2 = new RenderTarget2D(Graphics, width, height, false, SurfaceFormat.Color, DepthFormat.None);
            
            var verts = new VertexPositionTexture[]
            {
                new VertexPositionTexture(new Vector3(-1f, -1f, 0f), new Vector2(0f, 1f)),
                new VertexPositionTexture(new Vector3(-1f,  1f, 0f), new Vector2(0f, 0f)),
                new VertexPositionTexture(new Vector3( 1f,  1f, 0f), new Vector2(1f, 0f)),
                new VertexPositionTexture(new Vector3( 1f, -1f, 0f), new Vector2(1f, 1f))
            };

            _vb = new VertexBuffer(Graphics, typeof(VertexPositionTexture), verts.Length, BufferUsage.WriteOnly);
            _vb.SetData(verts);

            var indices = new short[] { 0, 1, 2, 0, 2, 3 };

            _ib = new IndexBuffer(Graphics, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _ib.SetData(indices);
        }

        public override void DrawStart()
        {
            // Common state
            Graphics.BlendState = BlendState.Opaque;
            Graphics.DepthStencilState = DepthStencilState.None;
            Graphics.RasterizerState = RasterizerState.CullNone;
        
            // Samplers
            Graphics.SamplerStates[0] = SamplerState.PointClamp; // not used here
            Graphics.SamplerStates[1] = new SamplerState
            {
                Filter = TextureFilter.Point,
                AddressU = TextureAddressMode.Border,
                AddressV = TextureAddressMode.Border,
                BorderColor = Color.Black
            };
            
            Graphics.SamplerStates[2] = new SamplerState
            {
                Filter = TextureFilter.Point,
                AddressU = TextureAddressMode.Border,
                AddressV = TextureAddressMode.Border,
                BorderColor = Color.White
            };
        
            _effect.Parameters["ScreenSize"]?.SetValue(new Vector2(Engine.ScreenWidth, Engine.ScreenHeight));
        
            _effect.Parameters["NormalTex"]?.SetValue(NormalBuffer);
            _effect.Parameters["DepthTex"]?.SetValue(DepthBuffer);
        
            // Camera params for reconstruction
            var cam = Scenes.GameScene.PlayerCharacter.Camera;
            _effect.Parameters["CameraNear"]?.SetValue(cam.NearPlane);
            _effect.Parameters["CameraFar"]?.SetValue(cam.FarPlane);
            _effect.Parameters["CameraAspect"]?.SetValue(cam.AspectRatio);
            _effect.Parameters["TanHalfFov"]?.SetValue((float)Math.Tan(MathHelper.ToRadians(cam.Fov * 0.5f)));
        
            // SSAO tuning
            _effect.Parameters["AORadiusPx"]?.SetValue(6.0f);    // kernel size in pixels
            _effect.Parameters["AOBiasZ"]?.SetValue(0.02f);      // meters
            _effect.Parameters["DepthThresholdZ"]?.SetValue(2.0f);
            _effect.Parameters["AOIntensity"]?.SetValue(1.2f);
            _effect.Parameters["NormalWeight"]?.SetValue(0.10f);
        
            // Blur tuning
            _effect.Parameters["BlurSigmaPx"]?.SetValue(1.5f);
            _effect.Parameters["DepthSigmaZ"]?.SetValue(2.0f);
            _effect.Parameters["NormalPow"]?.SetValue(4.0f);
        
            Graphics.SetVertexBuffer(_vb);
            Graphics.Indices = _ib;
        
            // Pass 1: SSAO into _aoRT1
            Graphics.SetRenderTarget(_aoRT1);
            Graphics.Clear(Color.White); // AO=1 baseline
            _effect.CurrentTechnique = _effect.Techniques["SSAO"];
            
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        
            // Pass 2: Horizontal bilateral blur into _aoRT2
            Graphics.SetRenderTarget(_aoRT2);
            Graphics.Clear(Color.White);
            _effect.Parameters["AOTex"]?.SetValue(_aoRT1);
            _effect.CurrentTechnique = _effect.Techniques["BlurH"];
            
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                Graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 2);
            }
        
            // Pass 3: Vertical bilateral blur into OutputBuffer
            Graphics.SetRenderTarget(OutputBuffer);
            Graphics.Clear(Color.Transparent);
            _effect.Parameters["AOTex"]?.SetValue(_aoRT2);
            _effect.CurrentTechnique = _effect.Techniques["BlurV"];
            
            foreach (var pass in _effect.CurrentTechnique.Passes)
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
            _aoRT1?.Dispose();
            _aoRT2?.Dispose();
            _aoRT1 = null;
            _aoRT2 = null;
        }
    }
}