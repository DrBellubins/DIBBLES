using DIBBLES.Gameplay;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;

using static DIBBLES.Terrain.TerrainGeneration;

namespace DIBBLES.Terrain;

public class TerrainCommands
{
    public void Register()
    {
        Commands.RegisterCommand("cyl", "Creates a cylinder beneath the player", cylinderCMD);
        Commands.RegisterCommand("hcyl", "Creates a hollow cylinder beneath the player", hollowCylinderCMD);
        Commands.RegisterCommand("seed", "Displays seed in chat, and saves to txt file.", seedCmd);
    }
    
    private void cylinderCMD(string[] args)
    {
        if (args.Length < 1)
        {
            Chat.Write("Usage: /cyl [type] [radius] [height]", ChatMessageType.Error);
            return;
        }

        // Parse block type
        var typeStr = args[0];
        if (!Enum.TryParse<BlockType>(typeStr, true, out var blockType))
        {
            Chat.Write($"Unknown block type: '{typeStr}'", ChatMessageType.Error);
            return;
        }

        // Parse radius (optional, default 5)
        int radius = 5;
        if (args.Length >= 2 && !int.TryParse(args[1], out radius))
        {
            Chat.Write($"Invalid radius: '{args[1]}'", ChatMessageType.Error);
            return;
        }

        // Parse height (optional, default 1)
        int height = 1;
        if (args.Length >= 3 && !int.TryParse(args[2], out height))
        {
            Chat.Write($"Invalid height: '{args[2]}'", ChatMessageType.Error);
            return;
        }

        // Create cylinder
        createCylinder(blockType, radius, height);
        Chat.Write($"Created cylinder of {blockType} (radius {radius}, height {height})", ChatMessageType.Command);
    }
    
    private void hollowCylinderCMD(string[] args)
    {
        if (args.Length < 1)
        {
            Chat.Write("Usage: /hcyl [type] [radius] [height]", ChatMessageType.Error);
            return;
        }

        // Parse block type
        var typeStr = args[0];
        if (!Enum.TryParse<BlockType>(typeStr, true, out var blockType))
        {
            Chat.Write($"Unknown block type: '{typeStr}'", ChatMessageType.Error);
            return;
        }

        // Parse radius (optional, default 5)
        int radius = 5;
        if (args.Length >= 2 && !int.TryParse(args[1], out radius))
        {
            Chat.Write($"Invalid radius: '{args[1]}'", ChatMessageType.Error);
            return;
        }

        // Parse height (optional, default 1)
        int height = 1;
        if (args.Length >= 3 && !int.TryParse(args[2], out height))
        {
            Chat.Write($"Invalid height: '{args[2]}'", ChatMessageType.Error);
            return;
        }

        // Create cylinder
        createHollowCylinder(blockType, radius, height);
        Chat.Write($"Created hollow cylinder of {blockType} (radius {radius}, height {height})", ChatMessageType.Command);
    }
    
    private void createCylinder(BlockType blockType, int radius, int height = 1)
    {
        var centerX = (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.X);
        var centerY = (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.Y - 2);
        var centerZ = (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.Z);
    
        HashSet<Vector3Int> affectedChunks = new();
    
        for (int h = 0; h < height; h++)
        {
            int y = centerY + h;
            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                if (dx * dx + dz * dz <= radius * radius)
                {
                    var blockPos = new Vector3Int(centerX + dx, y, centerZ + dz);
                    Chunk.SetBlockTypeGlobal(blockPos, blockType);
    
                    // Track affected chunk
                    int chunkX = (int)Math.Floor((float)blockPos.X / ChunkSize) * ChunkSize;
                    int chunkY = (int)Math.Floor((float)blockPos.Y / ChunkSize) * ChunkSize;
                    int chunkZ = (int)Math.Floor((float)blockPos.Z / ChunkSize) * ChunkSize;
                    
                    affectedChunks.Add(new Vector3Int(chunkX, chunkY, chunkZ));
                }
            }
        }
    
        // Remesh affected chunks
        foreach (var chunkCoord in affectedChunks)
        {
            if (ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            {
                // Opaque
                var meshData = Mesh.GenerateMeshData(chunk, false);
                Mesh.OpaqueModels[chunkCoord] = Mesh.UploadMesh(meshData);
    
                // Transparent
                var tMeshData = Mesh.GenerateMeshData(chunk, true);
                Mesh.TransparentModels[chunkCoord] = Mesh.UploadMesh(tMeshData);

                // Add to save
                if (WorldSave.Data.ModifiedChunks.All(c => c.Key != chunk.Position))
                    WorldSave.Data.ModifiedChunks.Add(chunk.Position, chunk);
            }
        }
    }
    
    private void createHollowCylinder(BlockType blockType, int outerRadius, int height = 1, int wallThickness = 1)
    {
        var centerX = (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.X);
        var centerY = (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.Y - 2);
        var centerZ = (int)MathF.Floor((float)GameScene.PlayerCharacter.Position.Z);
    
        // Clamp thickness and compute inner radius
        wallThickness = Math.Max(1, wallThickness);
        int innerRadius = Math.Max(0, outerRadius - wallThickness);
    
        int outerR2 = outerRadius * outerRadius;
        int innerR2 = innerRadius * innerRadius;
    
        HashSet<Vector3Int> affectedChunks = new();
    
        for (int h = 0; h < height; h++)
        {
            int y = centerY + h;
    
            for (int dx = -outerRadius; dx <= outerRadius; dx++)
            for (int dz = -outerRadius; dz <= outerRadius; dz++)
            {
                int r2 = dx * dx + dz * dz;
    
                // Ring condition: inside outer circle, outside inner circle
                if (r2 <= outerR2 && r2 >= innerR2)
                {
                    var blockPos = new Vector3Int(centerX + dx, y, centerZ + dz);
                    Chunk.SetBlockTypeGlobal(blockPos, blockType);
    
                    // Track affected chunk
                    int chunkX = (int)Math.Floor((float)blockPos.X / ChunkSize) * ChunkSize;
                    int chunkY = (int)Math.Floor((float)blockPos.Y / ChunkSize) * ChunkSize;
                    int chunkZ = (int)Math.Floor((float)blockPos.Z / ChunkSize) * ChunkSize;
    
                    affectedChunks.Add(new Vector3Int(chunkX, chunkY, chunkZ));
                }
            }
        }
    
        // Remesh affected chunks
        foreach (var chunkCoord in affectedChunks)
        {
            if (ChunkBuffer.TryGetValue(chunkCoord, out var chunk))
            {
                // Opaque
                var meshData = Mesh.GenerateMeshData(chunk, false);
                Mesh.OpaqueModels[chunkCoord] = Mesh.UploadMesh(meshData);
    
                // Transparent
                var tMeshData = Mesh.GenerateMeshData(chunk, true);
                Mesh.TransparentModels[chunkCoord] = Mesh.UploadMesh(tMeshData);
    
                // Add to save
                if (WorldSave.Data.ModifiedChunks.All(c => c.Key != chunk.Position))
                    WorldSave.Data.ModifiedChunks.Add(chunk.Position, chunk);
            }
        }
    }

    private void seedCmd(string[] args)
    {
        Chat.Write($"Current seed: {Seed}", ChatMessageType.Command);

        var filename = Path.Combine(AppContext.BaseDirectory, $"Seed_{Seed}.txt");

        if (!File.Exists(filename))
        {
            File.Create(filename);
            Chat.Write($"Wrote to file: Seed_{Seed}.txt", ChatMessageType.Command);
        }
        else
            Chat.Write($"Seed file 'Seed_{Seed}.txt' already exists.", ChatMessageType.Warning);
    }
}