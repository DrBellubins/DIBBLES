using DIBBLES.Gameplay;
using Microsoft.Xna.Framework;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Effects;

public class FogEffect
{
    public const float FogNear = 50.0f;
    public const float FogFar = 150.0f;
    
    // Used in the terrain shader!
    //public static Vector4 FogColor = new Vector4(GameScene.SkyColor.R, GameScene.SkyColor.G, GameScene.SkyColor.B, 1.0f);

    /*public static Color FogColor()
    {
        var color = new Color(DayNightCycle.HorizonColor.R / 255f,
            DayNightCycle.HorizonColor.G / 255f, DayNightCycle.HorizonColor.B / 255f, 1.0f);

        return color.HSV(1.0f, 1.0f, 0.5f);
    }

    public static Vector4 FogColor()
    {
        var colorVec = new Vector4(DayNightCycle.HorizonColor.R / 255f,
            DayNightCycle.HorizonColor.G / 255f, DayNightCycle.HorizonColor.B / 255f, 1.0f);

        colorVec.X *= 0.5f;
        colorVec.Y *= 0.5f;
        colorVec.X *= 0.5f;

        return colorVec;
    }*/
}