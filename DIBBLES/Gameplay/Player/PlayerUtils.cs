using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework;

namespace DIBBLES.Gameplay.Player;

using static TerrainGeneration;

public class PlayerUtils
{
    public static byte ClosestLightLevel = 0;
    
    public static void SetCameraDirection(PlayerCharacter player, Vector3 direction)
    {
        if (direction == Vector3.Zero)
            direction = new Vector3(0f, 0f, 1f); // Fallback to default forward if zero
        
        direction = Vector3.Normalize(direction);

        player.CameraYaw = MathF.Atan2(direction.X, direction.Z); // Or whatever your yaw convention is
        player.CameraPitch = -MathF.Asin(direction.Y); // Negative sign for proper pitch direction

        // Now construct CameraRotation as usual
        Quaternion rotYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.CameraYaw);
        Quaternion rotPitch = Quaternion.CreateFromAxisAngle(Vector3.UnitX, player.CameraPitch);

        player.CameraRotation = Quaternion.Normalize(rotYaw * rotPitch);
        
        // Calculate camera direction
        player.CameraForward = Vector3.Transform(Vector3.UnitZ,
            player.CameraRotation); // Forward
        
        player.CameraUp = Vector3.Transform(Vector3.UnitY,
            player.CameraRotation);
        
        player.CameraRight = Vector3.Transform(-Vector3.UnitX,
            player.CameraRotation); // This has to be flipped for some reason...
    }
    
    public static void CheckCollisions()
    {
        var moveDelta = PlayerManager.Current.Velocity * (float)Time.DeltaTime;
        var newPosition = PlayerManager.Current.Position;

        // Call once per frame before axis checks!
        PlayerManager.Current.CollisionBoxes = GetBlockBoxes(PlayerManager.Current.Position.ToVector3(), 10f);

        // X axis
        newPosition.X += moveDelta.X;
        
        var playerBoxX = GetBoundingBox(newPosition, PlayerManager.Current.CurrentHeight);
        var collidedX = PlayerManager.Current.CollisionBoxes.Any(box => box.Intersects(playerBoxX));
        
        if (collidedX)
        {
            newPosition.X -= moveDelta.X;
            PlayerManager.Current.Velocity.X = 0f;
            
            PlayerManager.Current.CollisionBox = playerBoxX;
        }

        // Y axis
        newPosition.Y += moveDelta.Y;
        
        var playerBoxY = GetBoundingBox(newPosition, PlayerManager.Current.CurrentHeight);
        var collidedY = PlayerManager.Current.CollisionBoxes.Any(box => box.Intersects(playerBoxY));
        
        if (collidedY)
        {
            if (PlayerManager.Current.Velocity.Y < 0f)
                PlayerManager.Current.IsGrounded = true;
            
            newPosition.Y -= moveDelta.Y;
            PlayerManager.Current.Velocity.Y = 0f;
            
            PlayerManager.Current.CollisionBox = playerBoxY;
        }

        // Z axis
        newPosition.Z += moveDelta.Z;
        
        var playerBoxZ = GetBoundingBox(newPosition, PlayerManager.Current.CurrentHeight);
        var collidedZ = PlayerManager.Current.CollisionBoxes.Any(box => box.Intersects(playerBoxZ));
        
        if (collidedZ)
        {
            newPosition.Z -= moveDelta.Z;
            PlayerManager.Current.Velocity.Z = 0f;

            PlayerManager.Current.CollisionBox = playerBoxZ;
        }
        
        PlayerManager.Current.Position = newPosition;
    }
    
    public static void UpdateClosestLightLevel(Vector3 pos)
    {
        var blockPos = new Vector3Int(
            (int)MathF.Floor(pos.X),
            (int)MathF.Floor(pos.Y),
            (int)MathF.Floor(pos.Z)
        );

        // Get the light level at this block (use global helper)
        byte lightLevel = Chunk.GetLightLevelGlobal(blockPos);

        ClosestLightLevel = lightLevel;
    }
    
    // Player box size: width and depth ≈ 0.5m (Source player is 32 units wide ≈ 0.81m, but keep hitbox thin for simplicity)
    public static BoundingBox GetBoundingBox(GVec3 position, float height)
    {
        GVec3 min = new GVec3(
            position.X - 0.25d,
            position.Y - height * 0.5d,
            position.Z - 0.25d
        );
        GVec3 max = new GVec3(
            position.X + 0.25d,
            position.Y + height * 0.5d,
            position.Z + 0.25d
        );
        
        return new BoundingBox(min.ToVector3(), max.ToVector3());
    }
    
    public static List<BoundingBox> GetBlockBoxes(Vector3 center, float radius)
    {
        var result = new List<BoundingBox>();
        
        int minX = (int)MathF.Floor(center.X - radius);
        int maxX = (int)MathF.Floor(center.X + radius);
        int minY = (int)MathF.Floor(center.Y - radius);
        int maxY = (int)MathF.Floor(center.Y + radius);
        int minZ = (int)MathF.Floor(center.Z - radius);
        int maxZ = (int)MathF.Floor(center.Z + radius);

        float radiusSquared = radius * radius;

        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        for (int z = minZ; z <= maxZ; z++)
        {
            var blockCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
            
            if (Vector3.DistanceSquared(center, blockCenter) > radiusSquared)
                continue;

            // Find which chunk this block belongs to
            int chunkX = (int)Math.Floor((float)x / ChunkSize) * ChunkSize;
            int chunkY = (int)Math.Floor((float)y / ChunkSize) * ChunkSize;
            int chunkZ = (int)Math.Floor((float)z / ChunkSize) * ChunkSize;
            
            var chunkCoord = new Vector3Int(chunkX, chunkY, chunkZ);

            if (!ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
                continue;
            
            int localX = x - chunkX;
            int localY = y - chunkY;
            int localZ = z - chunkZ;

            // Bounds check
            if (localX < 0 || localX >= ChunkSize ||
                localY < 0 || localY >= ChunkSize ||
                localZ < 0 || localZ >= ChunkSize)
                continue;

            var blockType = chunk.GetTypeAt(localX, localY, localZ);
            
            // Only add solid blocks
            if (blockType != BlockType.Air &&
                chunk.GetInfoAt(localX, localY, localZ).IsCollidable)
            {
                var blockMin = new Vector3(x, y, z);
                var blockMax = blockMin + Vector3.One;
                
                result.Add(new BoundingBox(blockMin, blockMax));
            }
        }
        
        return result;
    }
}