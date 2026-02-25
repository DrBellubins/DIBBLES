using DIBBLES.Gameplay;
using Microsoft.Xna.Framework;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using DIBBLES.Systems;

namespace DIBBLES.Effects;

public class FogEffect
{
    public const float FogNear = 50.0f;
    public const float FogFar = 150.0f;
    
    // Used in the terrain shader!
    //public static Vector4 FogColor = new Vector4(GameScene.SkyColor.R, GameScene.SkyColor.G, GameScene.SkyColor.B, 1.0f);

    public static Vector4 FogColor()
    {
        var colorVec = new Vector4(DayNightCycle.CurrentSkyColor.R / 255f,
            DayNightCycle.CurrentSkyColor.G / 255f, DayNightCycle.CurrentSkyColor.B / 255f, 1.0f);
        
        return colorVec;
    }
}