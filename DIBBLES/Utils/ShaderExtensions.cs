using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Utils;

public static class ShaderExtensions
{
    extension(Effect effect)
    {
        public void SetValue(string param, Matrix value)
        {
            EffectParams.SetMatrix(effect, param, value);
        }
        
        public void SetValue(string param, Vector2 value)
        {
            EffectParams.SetVector2(effect, param, value);
        }
        
        public void SetValue(string param, Vector3 value)
        {
            EffectParams.SetVector3(effect, param, value);
        }
        
        public void SetValue(string param, Vector4 value)
        {
            EffectParams.SetVector4(effect, param, value);
        }
        
        public void SetValue(string param, float value)
        {
            EffectParams.SetFloat(effect, param, value);
        }
        
        public void SetValue(string param, int value)
        {
            EffectParams.SetInt(effect, param, value);
        }
        
        public void SetValue(string param, bool value)
        {
            EffectParams.SetBool(effect, param, value);
        }
        
        public void SetValue(string param, Texture2D value)
        {
            EffectParams.SetTexture(effect, param, value);
        }
    }
}