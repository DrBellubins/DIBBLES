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

        public override void Start(int width, int height)
        {
            base.Start(width, height);

            _effect = Engine.Instance.Content.Load<Effect>("Shaders/SSAOPostProcess");

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
            Graphics.SetRenderTarget(OutputBuffer);
            Graphics.Clear(Color.Transparent);
        
            Graphics.BlendState = BlendState.Opaque;
            Graphics.DepthStencilState = DepthStencilState.None;
            Graphics.RasterizerState = RasterizerState.CullNone;
        
            // Samplers
            Graphics.SamplerStates[0] = SamplerState.PointClamp;
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
        
            _effect.Parameters["ColorTex"]?.SetValue(ColorBuffer);
            _effect.Parameters["NormalTex"]?.SetValue(NormalBuffer);
            _effect.Parameters["DepthTex"]?.SetValue(DepthBuffer);
            _effect.Parameters["ScreenSize"]?.SetValue(new Vector2(Engine.ScreenWidth, Engine.ScreenHeight));
        
            // Camera params for reconstruction (nvpro-style)
            var cam = Scenes.GameScene.PlayerCharacter.Camera;
            _effect.Parameters["CameraNear"]?.SetValue(cam.NearPlane);
            _effect.Parameters["CameraFar"]?.SetValue(cam.FarPlane);
            _effect.Parameters["CameraAspect"]?.SetValue(cam.AspectRatio);
            _effect.Parameters["TanHalfFov"]?.SetValue((float)Math.Tan(MathHelper.ToRadians(cam.Fov * 0.5f)));
        
            // SSAO tuning (world-space)
            _effect.Parameters["AORadiusPx"]?.SetValue(6.0f);        // kernel size in pixels
            _effect.Parameters["AOBiasZ"]?.SetValue(0.03f);           // prevents self-occlusion at flat surfaces
            _effect.Parameters["DepthThresholdZ"]?.SetValue(2.0f);    // gate large z jumps (kills silhouettes)
            _effect.Parameters["AOIntensity"]?.SetValue(1.2f);        // overall strength
            _effect.Parameters["NormalWeight"]?.SetValue(0.10f);      // subtle influence
        
            Graphics.SetVertexBuffer(_vb);
            Graphics.Indices = _ib;
        
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
            _vb?.Dispose();
            _ib?.Dispose();
            _effect?.Dispose();
            _vb = null;
            _ib = null;
            _effect = null;
        }
    }
}