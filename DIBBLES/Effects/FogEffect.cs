using System.Numerics;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using Raylib_cs;

namespace DIBBLES.Effects;

public class FogEffect
{
    private Shader fogShader;
    private RenderTexture2D target;
    private int sceneTexLoc;
    private int depthTexLoc;
    private int zNearLoc, zFarLoc, fogNearLoc, fogFarLoc, fogColorLoc;
    private int invProjLoc, invViewLoc, cameraPosLoc;
    
    public const float FogNear = 50.0f;
    public const float FogFar = 150.0f;
    public static Vector4 FogColor = new Vector4(0.4f, 0.7f, 1.0f, 1.0f); // Used in the terrain shader!
}