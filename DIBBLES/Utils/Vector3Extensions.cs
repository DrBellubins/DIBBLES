using Microsoft.Xna.Framework;

namespace DIBBLES.Utils;

public static class Vector3Extensions
{
    public static GVec3 ToGVec3(this Vector3 vector3)
    {
        return new GVec3(vector3.X, vector3.Y, vector3.Z);
    }
}