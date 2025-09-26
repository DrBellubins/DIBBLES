using DIBBLES.Utils;
using Microsoft.Xna.Framework;

namespace DIBBLES.Systems;

public class Camera3D
{
    public GVec3 Position;
    public Vector3 Target;
    public Vector3 Up = Vector3.Up;

    public float Fov = 90f;
    public float AspectRatio = (float)Engine.ScreenWidth / Engine.ScreenHeight;
    public float NearPlane = 0.01f;
    public float FarPlane = 1000f;

    public Matrix View => Matrix.CreateLookAt(Position.ToVector3(), Target, Up);
    public Matrix Projection { get; private set; }

    public void SetPerspective()
    {
        Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(Fov), AspectRatio, NearPlane, FarPlane);
    }
    
    public void SetOrthographic()
    {
        Projection = Matrix.CreateOrthographic(Fov * AspectRatio, Fov, NearPlane, FarPlane);
    }
}