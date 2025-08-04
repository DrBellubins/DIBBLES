using Raylib_cs;
using System.Numerics;

namespace DIBBLES.Effects;

public class FilmicEffect
{
    public Shader Shader;
    
    // Default values from ReShade FilmicPass Shader
    private float strength = 0.85f;
    private float fade = 0.4f;
    private float contrast = 1.0f;
    private float linearization = 0.5f;
    private float bleach = 0.0f;
    private float saturation = -0.15f;
    private float redCurve = 1.0f;
    private float greenCurve = 1.0f;
    private float blueCurve = 8.0f;
    private float baseCurve = 1.5f;
    private float baseGamma = 1.0f;
    private float effectGamma = 0.65f;
    private float effectGammaR = 1.0f;
    private float effectGammaG = 1.0f;
    private float effectGammaB = 1.0f;
    
    private int strengthLoc;
    private int fadeLoc;
    private int contrastLoc;
    private int linearizationLoc;
    private int bleachLoc;
    private int saturationLoc;
    private int redCurveLoc;
    private int greenCurveLoc;
    private int blueCurveLoc;
    private int baseCurveLoc;
    private int baseGammaLoc;
    private int effectGammaLoc;
    private int effectGammaRLoc;
    private int effectGammaGLoc;
    private int effectGammaBLoc;

    public void Start(RenderTexture2D input)
    {
        // Load the Shader (assumes filmic.fs is in the correct directory, e.g., Assets/Shaders/)
        Shader = Resource.LoadShader(null, "filmic.fs");

        // Get uniform locations
        strengthLoc = Raylib.GetShaderLocation(Shader, "Strength");
        fadeLoc = Raylib.GetShaderLocation(Shader, "Fade");
        contrastLoc = Raylib.GetShaderLocation(Shader, "Contrast");
        linearizationLoc = Raylib.GetShaderLocation(Shader, "Linearization");
        bleachLoc = Raylib.GetShaderLocation(Shader, "Bleach");
        saturationLoc = Raylib.GetShaderLocation(Shader, "Saturation");
        redCurveLoc = Raylib.GetShaderLocation(Shader, "RedCurve");
        greenCurveLoc = Raylib.GetShaderLocation(Shader, "GreenCurve");
        blueCurveLoc = Raylib.GetShaderLocation(Shader, "BlueCurve");
        baseCurveLoc = Raylib.GetShaderLocation(Shader, "BaseCurve");
        baseGammaLoc = Raylib.GetShaderLocation(Shader, "BaseGamma");
        effectGammaLoc = Raylib.GetShaderLocation(Shader, "EffectGamma");
        effectGammaRLoc = Raylib.GetShaderLocation(Shader, "EffectGammaR");
        effectGammaGLoc = Raylib.GetShaderLocation(Shader, "EffectGammaG");
        effectGammaBLoc = Raylib.GetShaderLocation(Shader, "EffectGammaB");

        // Set default uniform values
        Raylib.SetShaderValue(Shader, strengthLoc, strength, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, fadeLoc, fade, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, contrastLoc, contrast, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, linearizationLoc, linearization, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, bleachLoc, bleach, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, saturationLoc, saturation, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, redCurveLoc, redCurve, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, greenCurveLoc, greenCurve, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, blueCurveLoc, blueCurve, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, baseCurveLoc, baseCurve, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, baseGammaLoc, baseGamma, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, effectGammaLoc, effectGamma, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, effectGammaRLoc, effectGammaR, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, effectGammaGLoc, effectGammaG, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(Shader, effectGammaBLoc, effectGammaB, ShaderUniformDataType.Float);
    }

    public void Unload()
    {
        Raylib.UnloadShader(Shader);
        //Raylib.UnloadRenderTexture(target);
    }
}