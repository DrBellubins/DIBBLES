using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Systems;

// Add this class to represent a runtime mesh/model in MonoGame
public class RuntimeModel : IDisposable
{
    public VertexBuffer VertexBuffer;
    public IndexBuffer IndexBuffer;
    public int TriangleCount;
    public Effect Effect;
    public Texture2D Texture;
    private bool disposed = false;

    public void Draw(Matrix world, Matrix view, Matrix projection)
    {
        var gd = Engine.Graphics;
        
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

    public void Dispose()
    {
        if (!disposed)
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
            //Texture?.Dispose();
            Effect?.Dispose(); // Optional, only if it's not shared
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}