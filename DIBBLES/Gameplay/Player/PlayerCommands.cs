using DIBBLES.Gameplay.Terrain;
using DIBBLES.Systems;
using DIBBLES.Utils;

namespace DIBBLES.Gameplay.Player;

public class PlayerCommands
{
    public void Initialize()
    {
        Commands.Register("kill", "Kills the player", killCMD);
        Commands.Register("spawn", "Respawns player at spawn point",  respawnCMD);
        Commands.Register("heal", "Heals the player: /heal for full health", healCMD);
        Commands.Register("tp", "Teleport to a position: /teleport x y z", teleportCMD);
        Commands.Register("gm", "Toggle gamemode between creative and survival", gameModeCMD);
        Commands.Register("col", "Toggle collision debug", collisionDbgCMD);
    }
    
    private void teleportCMD(string[] args)
    {
        if (args.Length == 1 && args[0].Contains(','))
            args = args[0].Split(',');

        if (args.Length != 3 ||
            !double.TryParse(args[0], out var x) ||
            !double.TryParse(args[1], out var y) ||
            !double.TryParse(args[2], out var z))
        {
            Chat.Write("Usage: /teleport x y z", ChatMessageType.Error);
            return;
        }

        PlayerManager.Current.Position = new GVec3(x, y, z);
        Chat.Write($"Teleported to ({x}, {y}, {z})", ChatMessageType.Command);
    }

    private void gameModeCMD(string[] args)
    {
        PlayerManager.Current.IsSurvival = !PlayerManager.Current.IsSurvival;
        
        if (PlayerManager.Current.IsSurvival)
            Chat.Write("Set gamemode to survival",  ChatMessageType.Command);
        else
            Chat.Write("Set gamemode to creative",  ChatMessageType.Command);
    }
    
    private void collisionDbgCMD(string[] args)
    {
        if (args.Length < 1)
        {
            PlayerCharacter.CollisionBoxDebug = !PlayerCharacter.CollisionBoxDebug;
        
            if (PlayerCharacter.CollisionBoxDebug)
                Chat.Write("Enabled collision debug",  ChatMessageType.Command);
            else
                Chat.Write("Disabled collision debug",  ChatMessageType.Command);
        }
        else if (args[0] == "ba")
        {
            TerrainGameplay.BlocksAroundDebug = !TerrainGameplay.BlocksAroundDebug;
            
            if (TerrainGameplay.BlocksAroundDebug)
                Chat.Write("Enabled blocks around player debug",  ChatMessageType.Command);
            else
                Chat.Write("Disabled blocks around player debug",  ChatMessageType.Command);
        }
        else if (args[0] == "help")
        {
            Chat.Write("Arguments:", ChatMessageType.CommandHeader);
            Chat.Write("/col ba - Block around debug", ChatMessageType.Command);
        }
    }
    
    private void killCMD(string[] args)
    {
        PlayerManager.Current.Kill();
        Chat.Write("Killed the player", ChatMessageType.Command);
    }
    
    private void respawnCMD(string[] args)
    {
        PlayerManager.Current.Respawn();
        Chat.Write($"Spawning at {WorldSave.Data.PlayerPosition}", ChatMessageType.Command);
    }
    
    private void healCMD(string[] args)
    {
        int healAmount = 0;

        if (args.Length != 1)
        {
            healAmount = 100;
            Chat.Write("Set player health to full health", ChatMessageType.Command);
        }
        else if (int.TryParse(args[0], out var amount))
        {
            healAmount = amount;
            Chat.Write($"Set player health to: {amount}", ChatMessageType.Command);
        }
        else
            Chat.Write("Usage: /heal amount", ChatMessageType.Error);
            
        PlayerManager.Current.SetHealth(healAmount);
    }
}