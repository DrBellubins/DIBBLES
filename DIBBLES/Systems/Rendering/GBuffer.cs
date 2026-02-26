using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Systems.Rendering;

public struct GBuffer
{
    public RenderTarget2D Color;
    public RenderTarget2D Depth;
    public RenderTarget2D Normal;
    public RenderTarget2D Emissive;
}