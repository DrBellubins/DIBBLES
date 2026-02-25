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

        public Color HSV(float hue, float saturation, float value)
        {
            var hsv = RGBToHSV(color);
            hsv[0] *= hue;
            hsv[1] *= saturation;
            hsv[2] *= value;

            return HSVToRGB(hsv);
        }

        public Color SetHue(float hue)
        {
            var hsv = RGBToHSV(color);
            hsv[0] = hue;
            
            return HSVToRGB(hsv);
        }
        
        public Color SetSaturation(float saturation)
        {
            var hsv = RGBToHSV(color);
            hsv[1] = saturation;
            
            return HSVToRGB(hsv);
        }
        
        public Color SetValue(float value)
        {
            var hsv = RGBToHSV(color);
            hsv[2] = value;
            
            return HSVToRGB(hsv);
        }
        
        public Color MultiplyHue(float hue)
        {
            var hsv = RGBToHSV(color);
            hsv[0] *= hue;
            
            return HSVToRGB(hsv);
        }
        
        public Color MultiplySaturation(float saturation)
        {
            var hsv = RGBToHSV(color);
            hsv[1] *= saturation;
            
            return HSVToRGB(hsv);
        }
        
        public Color MultiplyValue(float value)
        {
            var hsv = RGBToHSV(color);
            hsv[2] *= value;
            
            return HSVToRGB(hsv);
        }

        public ColorF ToColorF()
        {
            color.Deconstruct(out float red, out float green, out float blue, out float alpha);
            return new ColorF(red, green, blue, alpha);
        }
    }

    public static Color HSVLerp(Color a, Color b, float t)
    {
        var hsvA = RGBToHSV(a);
        var hsvB = RGBToHSV(b);

        // Shortest hue path
        float dh = hsvB[0] - hsvA[0];
        if (dh > 180f) dh -= 360f;
        else if (dh < -180f) dh += 360f;

        float h = hsvA[0] + dh * t;
        if (h < 0f) h += 360f;
        else if (h >= 360f) h -= 360f;

        float s = MathHelper.Lerp(hsvA[1], hsvB[1], t);
        float v = MathHelper.Lerp(hsvA[2], hsvB[2], t);

        return HSVToRGB(h, s, v);
    }

    private static float[] RGBToHSV(Color c)
    {
        float r = c.R / 255f;
        float g = c.G / 255f;
        float b = c.B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        float h = 0f;
        if (delta != 0f)
        {
            if (max == r)
                h = (((g - b) / delta) % 6f + 6f) % 6f;
            else if (max == g)
                h = (b - r) / delta + 2f;
            else
                h = (r - g) / delta + 4f;
            h *= 60f;
        }

        float s = (max == 0f) ? 0f : (delta / max);
        float v = max;

        return new[] { h, s, v };
    }

    private static Color HSVToRGB(float h, float s, float v)
    {
        h = ((h % 360f) + 360f) % 360f;
        int sector = (int)(h / 60f);
        float f = (h / 60f) - sector;

        float p = v * (1f - s);
        float q = v * (1f - s * f);
        float t = v * (1f - s * (1f - f));

        float rr, gg, bb;
        switch (sector)
        {
            case 0: rr = v; gg = t; bb = p; break;
            case 1: rr = q; gg = v; bb = p; break;
            case 2: rr = p; gg = v; bb = t; break;
            case 3: rr = p; gg = q; bb = v; break;
            case 4: rr = t; gg = p; bb = v; break;
            default: rr = v; gg = p; bb = q; break;
        }

        return new Color((byte)(rr * 255f), (byte)(gg * 255f), (byte)(bb * 255f));
    }

    private static Color HSVToRGB(float[] hsv)
    {
        return HSVToRGB(hsv[0], hsv[1], hsv[2]);
    }
}