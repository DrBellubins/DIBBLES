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
            
            // Create custom sampler states with point filtering and border addressing
            var pointClamp = new SamplerState { Filter = TextureFilter.Point, AddressU = TextureAddressMode.Clamp, AddressV = TextureAddressMode.Clamp }; // Clamp for color
            var pointBorderNormal = new SamplerState { Filter = TextureFilter.Point, AddressU = TextureAddressMode.Border, AddressV = TextureAddressMode.Border, BorderColor = Color.Black }; // Border for normals (neutral normal)
            var pointBorderDepth = new SamplerState { Filter = TextureFilter.Point, AddressU = TextureAddressMode.Border, AddressV = TextureAddressMode.Border, BorderColor = Color.White }; // Border for depth (far depth ~1.0)

            Graphics.SamplerStates[0] = pointClamp; // Color sampler: clamp
            Graphics.SamplerStates[1] = pointBorderNormal; // Normal sampler: border
            Graphics.SamplerStates[2] = pointBorderDepth; // Depth sampler: border

            _effect.Parameters["ColorTex"]?.SetValue(ColorBuffer);
            _effect.Parameters["NormalTex"]?.SetValue(NormalBuffer);
            _effect.Parameters["DepthTex"]?.SetValue(DepthBuffer);
            _effect.Parameters["ScreenSize"]?.SetValue(new Vector2(Engine.ScreenWidth, Engine.ScreenHeight));

            // Tuned for your normalized depth [0..1] (near=0.01, far=1000)
            _effect.Parameters["AORadius"]?.SetValue(6.0f);         // constant screen-space radius
            _effect.Parameters["AOBias"]?.SetValue(0.00015f);       // extremely small bias (normalized depth)
            _effect.Parameters["AOIntensity"]?.SetValue(18.0f);     // strong scale for tiny deltas
            _effect.Parameters["NormalWeight"]?.SetValue(0.10f);    // subtle orientation weight
            _effect.Parameters["AOEdgeStrength"]?.SetValue(0.35f);  // crease edge darkening

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