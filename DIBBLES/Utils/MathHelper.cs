namespace DIBBLES.Utils;

// Helper class for math functions
public static class MathHelper
{
    public static float ToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180.0f);
    }

    public static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}