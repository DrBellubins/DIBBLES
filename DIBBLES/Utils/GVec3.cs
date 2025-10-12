using Microsoft.Xna.Framework;

namespace DIBBLES.Utils;

public struct GVec3
{
    public double X, Y, Z;
    
    public static GVec3 Zero => new GVec3();
    public static GVec3 Half => new GVec3(0.5d, 0.5d, 0.5d);
    public static GVec3 One => new GVec3(1d, 1d, 1d);
    
    public GVec3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3((float)X, (float)Y, (float)Z);
    }
    
    // Addition
    public static GVec3 operator +(GVec3 a, GVec3 b)
        => new GVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    // Subtraction
    public static GVec3 operator -(GVec3 a, GVec3 b)
        => new GVec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    // Multiplication (by scalar)
    public static GVec3 operator *(GVec3 a, int scalar)
        => new GVec3(a.X * scalar, a.Y * scalar, a.Z * scalar);
    
    public static GVec3 operator *(GVec3 a, float scalar)
        => new GVec3(a.X * scalar, a.Y * scalar, a.Z * scalar);
    
    public static GVec3 operator *(GVec3 a, double scalar)
        => new GVec3(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static GVec3 operator *(int scalar, GVec3 a)
        => new GVec3(a.X * scalar, a.Y * scalar, a.Z * scalar);
    
    public static GVec3 operator *(float scalar, GVec3 a)
        => new GVec3(a.X * scalar, a.Y * scalar, a.Z * scalar);
    
    public static GVec3 operator *(double scalar, GVec3 a)
        => new GVec3(a.X * scalar, a.Y * scalar, a.Z * scalar);

    // Component-wise multiplication
    public static GVec3 operator *(GVec3 a, GVec3 b)
        => new GVec3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    // Division (by scalar)
    public static GVec3 operator /(GVec3 a, int scalar)
        => new GVec3(a.X / scalar, a.Y / scalar, a.Z / scalar);

    // Component-wise division
    public static GVec3 operator /(GVec3 a, GVec3 b)
        => new GVec3(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    
    // Inversion
    public static GVec3 operator -(GVec3 a)
        => new GVec3(-a.X, -a.Y, -a.Z);
    
    public static bool operator ==(GVec3 a, GVec3 b) => a.X == b.X && a.Y == b.Y &&  a.Z == b.Z;
    public static bool operator !=(GVec3 a, GVec3 b) => !(a == b);
    
    public override string ToString() => $"{{X:{X:G9} Y:{Y:G9} Z:{Z:G9}}}";
    public override bool Equals(object obj) => obj is GVec3 other && X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => (X, Y).GetHashCode();
}