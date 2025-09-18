using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Systems;

// Add this class to represent a runtime mesh/model in MonoGame
public class RuntimeModel
{
    public VertexBuffer VertexBuffer;
    public IndexBuffer IndexBuffer;
    public int TriangleCount;
    public Effect Effect;
    public Texture2D Texture;

    public void Draw(GraphicsDevice gd, Matrix world, Matrix view, Matrix projection)
    {
        if (Effect is BasicEffect basic)
        {
            basic.World = world;
            basic.View = view;
            basic.Projection = projection;
            basic.TextureEnabled = Texture != null;
            basic.Texture = Texture;
        }
        else
        {
            Effect.Parameters["World"]?.SetValue(world);
            Effect.Parameters["View"]?.SetValue(view);
            Effect.Parameters["Projection"]?.SetValue(projection);
            if (Texture != null)
                Effect.Parameters["Texture"]?.SetValue(Texture);
        }

        foreach (var pass in Effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.SetVertexBuffer(VertexBuffer);
            gd.Indices = IndexBuffer;
            gd.DrawIndexedPrimitives(
                PrimitiveType.TriangleList,
                0,
                0,
                TriangleCount
            );
        }
    }
}