using Microsoft.Xna.Framework;

namespace DIBBLES.Systems;

public struct Camera3D
{
    public Vector3 Position;
    public Vector3 Target;
    public Vector3 Up = Vector3.Up;

    public float Fov = MathHelper.ToRadians(90f);
    public float AspectRatio;
    public float NearPlane = 0.01f;
    public float FarPlane = 1000f;

    public Matrix View => Matrix.CreateLookAt(Position, Target, Up);
    public Matrix Projection => Matrix.CreatePerspectiveFieldOfView(Fov, AspectRatio, NearPlane, FarPlane);

    public Camera3D(Vector3 position, Vector3 target, Vector3 up)
    {
        Position = position;
        Target = target;
        Up = up;
    }
}