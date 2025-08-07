namespace DIBBLES.Utils;

public struct Vector2Int
{
    public int X, Y;

    public Vector2Int(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }
    
    public override bool Equals(object obj) => obj is Vector2Int other && X == other.X && Y == other.Y;
    public override int GetHashCode() => (X, Y).GetHashCode();
    public static bool operator ==(Vector2Int a, Vector2Int b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
}