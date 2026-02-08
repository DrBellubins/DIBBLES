using Microsoft.Xna.Framework;

namespace DIBBLES.Utils;

public static class ColorExtensions
{
    /// <summary>
    /// Normalize the color to [0..1] range using float-based Color.
    /// </summary>
    /// <param name="color"></param>
    /// <returns></returns>
    public static Color Normalize(this Color color)
    {
        return new Color((float)color.R * GMath.ColorDivisor,
            (float)color.G * GMath.ColorDivisor, 
            (float)color.B * GMath.ColorDivisor, 
            (float)color.A * GMath.ColorDivisor);
    }

    public static Color Darken(this Color color, float amount, float alphaAmount = 0f)
    {
        var colorFloat = ToColorF(color);
        
        return new Color(colorFloat.R - amount,
            colorFloat.G - amount, 
            colorFloat.B - amount, 
            colorFloat.A - alphaAmount);
    }
    
    public static Color Brighten(this Color color, float amount, float alphaAmount = 0f)
    {
        var colorFloat = ToColorF(color);
        
        return new Color(colorFloat.R + amount,
            colorFloat.G + amount, 
            colorFloat.B + amount, 
            colorFloat.A + alphaAmount);
    }

    public static ColorF ToColorF(this Color color)
    {
        color.Deconstruct(out float red, out float green, out float blue, out float alpha);
        return new ColorF(red, green, blue, alpha);
    }
}