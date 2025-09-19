using Microsoft.Xna.Framework;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;

namespace DIBBLES.Effects;

public class FogEffect
{
    public const float FogNear = 50.0f;
    public const float FogFar = 150.0f;
    public static Vector4 FogColor = new Vector4(0.4f, 0.7f, 1.0f, 1.0f); // Used in the terrain shader!
}