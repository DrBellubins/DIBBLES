namespace DIBBLES.Utils;

public struct Vector3Int
{
    public int X, Y, Z;

    public Vector3Int(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public override string ToString()
    {
        return $"({X}, {Y}, {Z})";
    }
    
    public override bool Equals(object obj) => obj is Vector3Int other && X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => (X, Y).GetHashCode();
    public static bool operator ==(Vector3Int a, Vector3Int b) => a.X == b.X && a.Y == b.Y &&  a.Z == b.Z;
    public static bool operator !=(Vector3Int a, Vector3Int b) => !(a == b);
}