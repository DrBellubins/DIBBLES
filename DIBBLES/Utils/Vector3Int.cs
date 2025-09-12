using System.Numerics;

namespace DIBBLES.Utils;

public struct Vector3Int
{
    public int X, Y, Z;

    public static readonly Vector3Int Zero = new Vector3Int(0, 0, 0);
    public static readonly Vector3Int One = new Vector3Int(1, 1, 1);
    
    public Vector3Int(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
    
    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }

    public string ToStringUnderscore()
    {
        return $"{X}_{Y}_{Z}";
    }
    
    // Addition
    public static Vector3Int operator +(Vector3Int a, Vector3Int b)
        => new Vector3Int(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    // Subtraction
    public static Vector3Int operator -(Vector3Int a, Vector3Int b)
        => new Vector3Int(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    // Multiplication (by scalar)
    public static Vector3Int operator *(Vector3Int a, int scalar)
        => new Vector3Int(a.X * scalar, a.Y * scalar, a.Z * scalar);

    public static Vector3Int operator *(int scalar, Vector3Int a)
        => new Vector3Int(a.X * scalar, a.Y * scalar, a.Z * scalar);

    // Component-wise multiplication
    public static Vector3Int operator *(Vector3Int a, Vector3Int b)
        => new Vector3Int(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    // Division (by scalar)
    public static Vector3Int operator /(Vector3Int a, int scalar)
        => new Vector3Int(a.X / scalar, a.Y / scalar, a.Z / scalar);

    // Component-wise division
    public static Vector3Int operator /(Vector3Int a, Vector3Int b)
        => new Vector3Int(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
    
    public static bool operator ==(Vector3Int a, Vector3Int b) => a.X == b.X && a.Y == b.Y &&  a.Z == b.Z;
    public static bool operator !=(Vector3Int a, Vector3Int b) => !(a == b);
    
    public override bool Equals(object obj) => obj is Vector3Int other && X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => (X, Y).GetHashCode();
}