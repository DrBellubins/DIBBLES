using DIBBLES.Systems;
using Microsoft.Xna.Framework;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;
namespace DIBBLES.Gameplay.Player;

public class HandModel
{
    private MonoCubeMesh handBlockModel;
    
    public void Start()
    {
        handBlockModel = MeshUtilsMonoGame.GenTexturedCube(MonoEngine.Graphics, BlockData.Textures[BlockType.Dirt]);
    }

    public void Draw(
        Camera3D camera, 
        Vector3 cameraForward, 
        Vector3 cameraRight, 
        Vector3 cameraUp, 
        Quaternion cameraRotation, 
        ItemSlot? selectedItem = null)
    {
        if (selectedItem == null)
            return;

        /*handBlockModel.Effect.LightingEnabled = true;
        handBlockModel.Effect.AmbientLightColor = new Vector3(0.5f, 0.5f, 0.5f);
        
        handBlockModel.Effect.DirectionalLight0.Enabled = true;
        handBlockModel.Effect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(-1.0f, -1.0f, -1.0f));
        handBlockModel.Effect.DirectionalLight0.DiffuseColor = new Vector3(1.0f, 1.0f, 1.0f);
        handBlockModel.Effect.DirectionalLight0.SpecularColor = new Vector3(0f, 0f, 0f);*/
        
        // Set the hand model texture
        Texture2D texture = BlockData.Textures[selectedItem.Type];
        MeshUtilsMonoGame.SetCubeMeshTexture(handBlockModel, texture);

        // Position relative to camera
        float forwardDistance = 0.5f;
        float rightDistance = 0.5f;
        float upDistance = -0.3f;

        Vector3 handPos = camera.Position
                          + cameraForward * forwardDistance
                          + cameraRight * rightDistance
                          + cameraUp * upDistance;

        // Rotation
        Quaternion rotation = cameraRotation;

        // Scale (adjust as desired)
        Vector3 scale = new Vector3(0.5f);

        // Build world matrix: Scale * Rotation * Translation
        Matrix world =
            Matrix.CreateScale(scale)
            * Matrix.CreateFromQuaternion(rotation)
            * Matrix.CreateTranslation(handPos);

        // Set camera matrices
        Matrix view = camera.View;
        Matrix projection = camera.Projection;

        // Draw the model
        handBlockModel.Draw(world, view, projection);
    }
}