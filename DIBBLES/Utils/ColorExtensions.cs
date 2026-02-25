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

    /// <summary>
    /// Returns HSV components from the Color.
    /// Hue range: [0, 360], Saturation/Value: [0, 1]
    /// </summary>
    public static void ToHSV(this Color inColor, out float h, out float s, out float v)
    {
        float r = inColor.R / 255f;
        float g = inColor.G / 255f;
        float b = inColor.B / 255f;
    
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;
    
        // Hue
        if (delta == 0f)
            h = 0f;
        else if (max == r)
            h = 60f * (((g - b) / delta) % 6f);
        else if (max == g)
            h = 60f * (((b - r) / delta) + 2f);
        else
            h = 60f * (((r - g) / delta) + 4f);
        
        if (h < 0f)
            h += 360f;
    
        // Saturation
        s = (max == 0f) ? 0f : delta / max;
    
        // Value
        v = max;
    }
    
    /// <summary>
    /// Constructs a Color from HSV components.
    /// h: [0,360], s: [0,1], v: [0,1]
    /// </summary>
    public static Color FromHSV(float h, float s, float v)
    {
        h = h % 360f;
        
        if (h < 0f)
            h += 360f;
    
        float r, g, b;
    
        if (s == 0f)
            r = g = b = v;
        else
        {
            float c = v * s;
            float x = c * (1 - MathF.Abs((h / 60f) % 2 - 1));
            float m = v - c;
    
            if (h < 60f)
            {
                r = c;
                g = x;
                b = 0;
            }
            else if (h < 120f)
            {
                r = x;
                g = c;
                b = 0;
            }
            else if (h < 180f)
            {
                r = 0;
                g = c;
                b = x;
            }
            else if (h < 240f)
            {
                r = 0;
                g = x;
                b = c;
            }
            else if (h < 300f)
            {
                r = x;
                g = 0;
                b = c;
            }
            else
            {
                r = c;
                g = 0;
                b = x;
            }
            
            r += m;
            g += m;
            b += m;
        }
        
        return new Color(r, g, b, 1f);
    }
    
    public static Color HueLerp(Color from, Color to, float t)
    {
        // Extract HSV from both colors
        from.ToHSV(out float h1, out float s1, out float v1);
        to.ToHSV(out float h2, out float s2, out float v2);
    
        // Interpolate hue (wraps around 360)
        float dh = h2 - h1;
        
        if (dh > 180.0f)
            h1 += 360.0f;
        else if (dh < -180.0f)
            h2 += 360.0f;
    
        float h = GMath.Lerp(h1, h2, t);
        h = h % 360.0f;
    
        // Optionally, interpolate s/v independently, or just use from's s/v
        // Best visual: average s/v as well (for full control, you may interpolate or keep one as fixed)
        float s = GMath.Lerp(s1, s2, t);
        float v = GMath.Lerp(v1, v2, t);
    
        // Build color
        return FromHSV(h, s, v);
    }
}