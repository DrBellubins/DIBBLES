namespace DIBBLES.Gameplay;

public struct Command(string name, string description)
{
    public string Name = name;
    public string Description = description;
}

public static class Commands
{
    public static Dictionary<Command, Action> Registry = new();

    public static void RegisterCommand(string command, string description, Action action)
    {
        Registry.Add(new Command($"{command}", description), action);
    }
}