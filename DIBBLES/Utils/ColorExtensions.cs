using Microsoft.Xna.Framework;

namespace DIBBLES.Utils;

public static class ColorExtensions
{
    /// <param name="color"></param>
    extension(Color color)
    {
        /// <summary>
        /// Normalize the color to [0..1] range using float-based Color.
        /// </summary>
        /// <returns></returns>
        public Color Normalize()
        {
            return new Color((float)color.R * GMath.ColorDivisor,
                (float)color.G * GMath.ColorDivisor, 
                (float)color.B * GMath.ColorDivisor, 
                (float)color.A * GMath.ColorDivisor);
        }

        public Color Darken(float amount, float alphaAmount = 0f)
        {
            var colorFloat = ToColorF(color);
        
            return new Color(colorFloat.R - amount,
                colorFloat.G - amount, 
                colorFloat.B - amount, 
                colorFloat.A - alphaAmount);
        }

        public Color Multiply(float amount, float alphaAmount = 1f)
        {
            var colorFloat = ToColorF(color);
        
            return new Color(colorFloat.R * amount,
                colorFloat.G * amount, 
                colorFloat.B * amount, 
                colorFloat.A * alphaAmount);
        }

        public Color Brighten(float amount, float alphaAmount = 0f)
        {
            var colorFloat = ToColorF(color);
        
            return new Color(colorFloat.R + amount,
                colorFloat.G + amount, 
                colorFloat.B + amount, 
                colorFloat.A + alphaAmount);
        }

        public ColorF ToColorF()
        {
            color.Deconstruct(out float red, out float green, out float blue, out float alpha);
            return new ColorF(red, green, blue, alpha);
        }
    }
}