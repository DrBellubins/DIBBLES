using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Utils;

// An abstraction layer for setting shader parameters that caches changes.
public class EffectParams
{
    private class Cache
    {
        public Dictionary<string, Matrix> Matrices = new();
        public Dictionary<string, Vector2> Vec2 = new();
        public Dictionary<string, Vector3> Vec3 = new();
        public Dictionary<string, Vector4> Vec4 = new();
        public Dictionary<string, Color> Colors = new();
        public Dictionary<string, float> Floats = new();
        public Dictionary<string, int> Ints = new();
        public Dictionary<string, bool> Bools = new();
        public Dictionary<string, Texture2D> Textures = new();
    }
    
    private static readonly Dictionary<Effect, Cache> _caches = new();
    
    private static Cache Get(Effect effect)
    {
        if (!_caches.TryGetValue(effect, out var cache))
        {
            cache = new Cache();
            _caches[effect] = cache;
        }
    
        return cache;
    }
    
    public static bool SetMatrix(Effect effect, string name, Matrix value, float eps = 1e-5f)
    {
        var cache = Get(effect);
    
        if (cache.Matrices.TryGetValue(name, out var old) && MatrixNearEqual(old, value, eps))
            return false;
    
        cache.Matrices[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    public static bool SetVector2(Effect effect, string name, Vector2 value, float eps = 1e-6f)
    {
        var cache = Get(effect);
    
        if (cache.Vec2.TryGetValue(name, out var old) && Vector2NearEqual(old, value, eps))
            return false;
    
        cache.Vec2[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    public static bool SetVector3(Effect effect, string name, Vector3 value, float eps = 1e-6f)
    {
        var cache = Get(effect);
    
        if (cache.Vec3.TryGetValue(name, out var old) && Vector3NearEqual(old, value, eps))
            return false;
    
        cache.Vec3[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    public static bool SetVector4(Effect effect, string name, Vector4 value, float eps = 1e-6f)
    {
        var cache = Get(effect);
    
        if (cache.Vec4.TryGetValue(name, out var old) && Vector4NearEqual(old, value, eps))
            return false;
    
        cache.Vec4[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    /*public static bool SetColor(Effect effect, string name, Color value)
    {
        var cache = Get(effect);
    
        if (cache.Colors.TryGetValue(name, out var old) && old.Equals(value))
            return false;
    
        cache.Colors[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }*/
    
    public static bool SetFloat(Effect effect, string name, float value, float eps = 1e-6f)
    {
        var cache = Get(effect);
    
        if (cache.Floats.TryGetValue(name, out var old) && Math.Abs(old - value) <= eps)
            return false;
    
        cache.Floats[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    public static bool SetInt(Effect effect, string name, int value)
    {
        var cache = Get(effect);
    
        if (cache.Ints.TryGetValue(name, out var old) && old == value)
            return false;
    
        cache.Ints[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    public static bool SetBool(Effect effect, string name, bool value)
    {
        var cache = Get(effect);
    
        if (cache.Bools.TryGetValue(name, out var old) && old == value)
            return false;
    
        cache.Bools[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    public static bool SetTexture(Effect effect, string name, Texture2D value)
    {
        var cache = Get(effect);
    
        if (cache.Textures.TryGetValue(name, out var old) && ReferenceEquals(old, value))
            return false;
    
        cache.Textures[name] = value;
        effect.Parameters[name]?.SetValue(value);
        return true;
    }
    
    private static bool MatrixNearEqual(Matrix a, Matrix b, float eps)
    {
        return Math.Abs(a.M11 - b.M11) <= eps &&
               Math.Abs(a.M12 - b.M12) <= eps &&
               Math.Abs(a.M13 - b.M13) <= eps &&
               Math.Abs(a.M14 - b.M14) <= eps &&
               Math.Abs(a.M21 - b.M21) <= eps &&
               Math.Abs(a.M22 - b.M22) <= eps &&
               Math.Abs(a.M23 - b.M23) <= eps &&
               Math.Abs(a.M24 - b.M24) <= eps &&
               Math.Abs(a.M31 - b.M31) <= eps &&
               Math.Abs(a.M32 - b.M32) <= eps &&
               Math.Abs(a.M33 - b.M33) <= eps &&
               Math.Abs(a.M34 - b.M34) <= eps &&
               Math.Abs(a.M41 - b.M41) <= eps &&
               Math.Abs(a.M42 - b.M42) <= eps &&
               Math.Abs(a.M43 - b.M43) <= eps &&
               Math.Abs(a.M44 - b.M44) <= eps;
    }
    
    private static bool Vector2NearEqual(Vector2 a, Vector2 b, float eps)
    {
        return Math.Abs(a.X - b.X) <= eps &&
               Math.Abs(a.Y - b.Y) <= eps;
    }
    
    private static bool Vector3NearEqual(Vector3 a, Vector3 b, float eps)
    {
        return Math.Abs(a.X - b.X) <= eps &&
               Math.Abs(a.Y - b.Y) <= eps &&
               Math.Abs(a.Z - b.Z) <= eps;
    }
    
    private static bool Vector4NearEqual(Vector4 a, Vector4 b, float eps)
    {
        return Math.Abs(a.X - b.X) <= eps &&
               Math.Abs(a.Y - b.Y) <= eps &&
               Math.Abs(a.Z - b.Z) <= eps &&
               Math.Abs(a.W - b.W) <= eps;
    }
}