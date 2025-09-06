using System.Numerics;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay.Player;

public class HandModel
{
    private Model handBlockModel;
    
    public void Start()
    {
        handBlockModel = MeshUtils.GenTexturedCube(BlockData.Textures[BlockType.Dirt]);
    }

    public void Draw(Camera3D camera, Vector3 cameraForward, Vector3 cameraRight, Vector3 cameraUp, Quaternion cameraRotation, ItemSlot? selectedItem = null)
    {
        if (selectedItem != null)
        {
            MeshUtils.SetModelTexture(handBlockModel, BlockData.Textures[selectedItem.Type]);

            // Adjust these distances for the best effect
            float forwardDistance = 0.5f; // In front of camera
            float rightDistance = 0.5f;   // To the right
            float upDistance = -0.3f;     // Down a bit (optional)

            Vector3 handPos = camera.Position 
                              + cameraForward * forwardDistance
                              + cameraRight * rightDistance
                              + cameraUp * upDistance;

            var rot = Quaternion.Inverse(cameraRotation);
            Matrix4x4 rotationMat = Matrix4x4.CreateFromQuaternion(rot);
            
            Vector3 axis;
            float angleDeg;
            GMath.MatrixToAxisAngle(rotationMat, out axis, out angleDeg);
            
            Raylib.DrawModelEx(handBlockModel, handPos, axis, angleDeg, new Vector3(0.25f), Color.White);
        }
    }
}