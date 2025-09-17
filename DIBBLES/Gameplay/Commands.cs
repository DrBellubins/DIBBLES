namespace DIBBLES.Gameplay;

public static class Commands
{
    public static Dictionary<string, Action> Registry = new();

    public static void RegisterCommand(string command, Action action)
    {
        Registry.Add($"/{command}", action);
    }
}