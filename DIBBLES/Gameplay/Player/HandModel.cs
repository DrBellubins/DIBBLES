using DIBBLES.Effects;
using DIBBLES.Gameplay.Inventory;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using Microsoft.Xna.Framework;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Gameplay.Player;

// TODO: Hand model does not use adjusted UVs generated in TerrainData
public class HandModel
{
    private CubeMesh handBlockModel;
    private Camera3D handCamera;
    private MainSurfaceEffect _effect;
    
    private float currentLightLevel = 0.0f;
    private float previousLightLevel = 0.0f;
    
    public void Start()
    {
        handBlockModel = MeshUtils.GenTexturedCube(Engine.Graphics, BlockData.Textures[(BlockType.Dirt, 0)]);
        
        handCamera = new Camera3D();
        handCamera.Position = GVec3.Zero;
        handCamera.Target = Vector3.Zero;
        handCamera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        handCamera.Fov = 60.0f;
        handCamera.SetPerspective();
        
        _effect = new MainSurfaceEffect(Engine.Graphics)
        {
            TextureEnabled = true,
            VertexColorEnabled = true,
            FogEnabled = true,
            DiffuseColor = Color.White.ToVector4()
        };
        
        handBlockModel.Effect =  _effect;
    }

    public void Draw(
        Camera3D camera,
        Vector3 cameraForward,
        Vector3 cameraRight,
        Vector3 cameraUp,
        Quaternion cameraRotation,
        byte lightLevel,
        ItemSlot? selectedItem = null)
    {
        if (selectedItem != null && selectedItem.Type == BlockType.Air)
            selectedItem = null;
        
        if (selectedItem == null)
            return;

        handCamera.Position = camera.Position;
        handCamera.Target = camera.Target;
        handCamera.Up = camera.Up;
        
        /*handBlockModel.Effect.LightingEnabled = true;
        handBlockModel.Effect.AmbientLightColor = new Vector3(0.9f, 0.9f, 0.9f);
        
        handBlockModel.Effect.DirectionalLight0.Enabled = true;
        handBlockModel.Effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-1.0f, -1.0f, -1.0f));
        handBlockModel.Effect.DirectionalLight0.DiffuseColor = new Vector3(1.0f, 1.0f, 1.0f);
        handBlockModel.Effect.DirectionalLight0.SpecularColor = new Vector3(0f, 0f, 0f);*/

        currentLightLevel = MathF.Max(0.1f, lightLevel * 0.06f); // Prevent fully dark, this matches FaceUtils.ToColor
        
        // Framerate-independent exponential smoothing; higher rate = snappier response
        float rate = 8.0f;
        float deltaTimeExp = 1f - MathF.Exp(-rate * Time.DeltaTime);
        
        var lightLevelLerped = GMath.Lerp(previousLightLevel, currentLightLevel, deltaTimeExp);
        
        // IMPORTANT: carry the smoothed result forward (not the target)
        previousLightLevel = lightLevelLerped;
        
        handBlockModel.Effect.DiffuseColor = new Vector4(lightLevelLerped, lightLevelLerped, lightLevelLerped, 1.0f);

        // Position relative to camera
        float forwardDistance = 0.7f;
        float rightDistance = 0.5f;
        float upDistance = -0.3f;

        //TODO: Single floating point precision issues
        Vector3 handPos = handCamera.Position.ToVector3()
                          + cameraForward * forwardDistance
                          + cameraRight * rightDistance
                          + cameraUp * upDistance;

        // Rotation
        var rotOffset = Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), MathHelper.ToRadians(-45f));
        Quaternion rotation = cameraRotation * rotOffset;

        // Scale (adjust as desired)
        Vector3 scale = new Vector3(0.5f);

        // Build world matrix: Scale * Rotation * Translation
        Matrix world =
            Matrix.CreateScale(scale)
            * Matrix.CreateFromQuaternion(rotation)
            * Matrix.CreateTranslation(handPos);

        // Set camera matrices
        Matrix view = handCamera.View;
        Matrix projection = handCamera.Projection;

        // If you set a hand texture, assign it:
        _effect.Texture = BlockData.Textures[(selectedItem.Type, 0)];

        // In the hand draw path, set matrices and apply before drawing passes
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = projection;

        _effect.Apply();
        
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            
            // Draw the model
            handBlockModel.Draw(world, view, projection);
        }
    }
}