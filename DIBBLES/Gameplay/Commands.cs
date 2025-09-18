namespace DIBBLES.Gameplay;

public struct Command(string name, string description, Action<string[]> handler)
{
    public string Name = name;
    public string Description = description;
    public Action<string[]> Handler = handler;
}

public static class Commands
{
    public static Dictionary<string, Command> Registry = new();

    public static void RegisterCommand(string command, string description, Action<string[]> handler)
    {
        Registry[command.ToLower()] = new Command(command, description, handler);
    }
}