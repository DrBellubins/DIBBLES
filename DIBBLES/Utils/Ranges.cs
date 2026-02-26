namespace DIBBLES.Utils;

public struct RangeF
{
    public float Min;
    public float Max;

    public RangeF(float min, float max)
    {
        Min = min;
        Max = max;
    }
    
    // Addition
    public static RangeF operator +(RangeF a, RangeF b)
        => new RangeF(a.Min + b.Min, a.Max + b.Max);

    // Subtraction
    public static RangeF operator -(RangeF a, RangeF b)
        => new RangeF(a.Min - b.Min, a.Max - b.Max);

    // Multiplication (by scalar)
    public static RangeF operator *(RangeF a, int scalar)
        => new RangeF(a.Min * scalar, a.Max * scalar);
    
    public static RangeF operator *(RangeF a, float scalar)
        => new RangeF(a.Min * scalar, a.Max * scalar);

    public static RangeF operator *(int scalar, RangeF a)
        => new RangeF(a.Min * scalar, a.Max * scalar);
    
    public static RangeF operator *(float scalar, RangeF a)
        => new RangeF(a.Min * scalar, a.Max * scalar);

    // Component-wise multiplication
    public static RangeF operator *(RangeF a, RangeF b)
        => new RangeF(a.Min * b.Min, a.Max * b.Max);

    // Division (by scalar)
    public static RangeF operator /(RangeF a, int scalar)
        => new RangeF(a.Min / scalar, a.Max / scalar);

    // Component-wise division
    public static RangeF operator /(RangeF a, RangeF b)
        => new RangeF(a.Min / b.Min, a.Max / b.Max);
    
    // Inversion
    public static RangeF operator -(RangeF a)
        => new RangeF(-a.Min, -a.Max);

    public static bool operator ==(RangeF a, RangeF b) => a.Min == b.Min && a.Max == b.Max;
    public static bool operator !=(RangeF a, RangeF b) => !(a == b);
    
    public override string ToString() => $"{{Min:{Min:G9} Max:{Max:G9}";
    public override bool Equals(object? obj) => obj is RangeF other && Min == other.Min && Max == other.Max;
    public override int GetHashCode() => (Min, Max).GetHashCode();
}

public struct RangeInt
{
    public int Min;
    public int Max;

    public RangeInt(int min, int max)
    {
        Min = min;
        Max = max;
    }
    
    // Addition
    public static RangeInt operator +(RangeInt a, RangeInt b)
        => new RangeInt(a.Min + b.Min, a.Max + b.Max);

    // Subtraction
    public static RangeInt operator -(RangeInt a, RangeInt b)
        => new RangeInt(a.Min - b.Min, a.Max - b.Max);

    // Multiplication (by scalar)
    public static RangeInt operator *(RangeInt a, int scalar)
        => new RangeInt(a.Min * scalar, a.Max * scalar);

    public static RangeInt operator *(int scalar, RangeInt a)
        => new RangeInt(a.Min * scalar, a.Max * scalar);

    // Component-wise multiplication
    public static RangeInt operator *(RangeInt a, RangeInt b)
        => new RangeInt(a.Min * b.Min, a.Max * b.Max);

    // Division (by scalar)
    public static RangeInt operator /(RangeInt a, int scalar)
        => new RangeInt(a.Min / scalar, a.Max / scalar);

    // Component-wise division
    public static RangeInt operator /(RangeInt a, RangeInt b)
        => new RangeInt(a.Min / b.Min, a.Max / b.Max);
    
    // Inversion
    public static RangeInt operator -(RangeInt a)
        => new RangeInt(-a.Min, -a.Max);

    public static bool operator ==(RangeInt a, RangeInt b) => a.Min == b.Min && a.Max == b.Max;
    public static bool operator !=(RangeInt a, RangeInt b) => !(a == b);
    
    public override string ToString() => $"{{Min:{Min:G9} Max:{Max:G9}";
    public override bool Equals(object? obj) => obj is RangeInt other && Min == other.Min && Max == other.Max;
    public override int GetHashCode() => (Min, Max).GetHashCode();
}