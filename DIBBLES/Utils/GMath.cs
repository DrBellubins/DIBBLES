using System.Numerics;

namespace DIBBLES.Utils;

// Helper class for math functions
public static class GMath
{
    // XorWow
    private static uint x, y, z, w, v;
    private static uint d;

    public static void Init()
    {
        var seed = (uint)(new Random().Next(int.MinValue, int.MaxValue));
        
        // Initialize the state with the seed
        x = seed ^ 0x6B5FCA7D; // XOR with a constant to avoid zero state
        y = seed + 0x9E3779B9;
        z = seed ^ 0xDEADBEEF;
        w = seed + 0x12345678;
        v = seed ^ 0xCAFEBABE;
        d = 362437;

        // Run a few iterations to mix the seed
        for (int i = 0; i < 10; i++)
        {
            Next();
        }
    }

    // Returns a float between 0 (inclusive) and 1 (exclusive)
    public static float NextFloat()
    {
        return (float)(Next() / 4294967296.0); // Divide by 2^32
    }

    // Returns an integer in the range [min, max) (max exclusive)
    public static int NextInt(int min, int max)
    {
        if (min >= max)
            throw new ArgumentException("min must be less than max");
        
        uint range = (uint)(max - min);
        return (int)(min + (Next() % range));
    }

    // Returns a float in the range [min, max) (max exclusive)
    public static float NextFloat(float min, float max)
    {
        if (min >= max)
            throw new ArgumentException("min must be less than max");
        
        return min + (max - min) * NextFloat();
    }
    
    private static uint Next()
    {
        uint t = (x ^ (x >> 2));
        x = y; y = z; z = w; w = v;
        v = (v ^ (v << 4)) ^ (t ^ (t << 1));
        d += 362437;
        return v + d;
    }
    
    // Other math functions
    public static float ToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180.0f);
    }
    
    public static double ToRadians(double degrees)
    {
        return degrees * (MathF.PI / 180.0d);
    }

    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}