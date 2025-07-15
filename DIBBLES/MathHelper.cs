namespace DIBBLES;

// Helper class for math functions
public static class MathHelper
{
    public static float ToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180.0f);
    }
}