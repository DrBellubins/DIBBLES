using System.Net.Mime;
using System.Numerics;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay;

public enum ChatMessageType
{
    Message,
    Command,
    Warning,
    Error
}

public struct ChatMessage(ChatMessageType type, string message)
{
    public ChatMessageType Type = type;
    public string Message = message;
}

// TODO: Implement text wrapping.
public class Chat
{
    public const int Width = 800;
    public const int Height = 400;
    public const float FontSize = 24f;
    
    public static bool IsOpen {get; private set;}
    public static bool IsClosedButShown {get; private set;}
    
    public static List<ChatMessage> ChatMessages = new();
    
    private static List<string> prevChatMessages = new();
    
    public RenderTexture2D ChatTexture;
    
    private Rectangle chatBox = new Rectangle(0f, 0f, Width, Height);
    private TextBox textBox = new TextBox(new Rectangle(0f, 0f, Width, 40f));
    
    public float heightPos = UI.LeftCenterPivot.Y - (Height / 2f);
    
    // Chat disappear timer
    private float elapsed = 0f;
    private const float disappearTime = 5f;
    
    // Previous message traversal
    private int prevMsgTraversalIndex = 0;
    
    // Chat text/scrolling checks
    private float scrollOffset = 0;
    private bool isUserScrolling = false;
    
    private static int maxLines = (int)(Height / FontSize);
    private static int maxScroll = Math.Max(0, ChatMessages.Count - maxLines);
    
    public void Start()
    {
        ChatTexture = Raylib.LoadRenderTexture(Width, Height);
        
        textBox.Bounds.X = UI.LeftCenterPivot.X;
        textBox.Bounds.Y = UI.LeftCenterPivot.Y + (Height / 2f);
    }

    public void Update()
    {
        if (IsOpen)
        {
            textBox.Update();
            
            float wheel = Input.ScrollDelta();
            
            if (wheel != 0)
            {
                scrollOffset += wheel;
                
                maxScroll = Math.Max(0, ChatMessages.Count - maxLines);
                
                scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);
                isUserScrolling = scrollOffset > 0;
            }
        }
        
        // Send msg/cmd to chat
        if (Input.SendChat() && textBox.Text != string.Empty)
        {
            if (textBox.Text[0] == '/')
            {
                var cmdEntry = Commands.Registry
                    .FirstOrDefault(pair => pair.Key.Name == textBox.Text);
                
                if (!cmdEntry.Equals(default(KeyValuePair<Command, Action>)))
                {
                    cmdEntry.Value();
                    Write(ChatMessageType.Command, $"Executed '{textBox.Text}' command.");
                    
                    Console.WriteLine($"Player executed command '{textBox.Text}'.");
                }
                else
                {
                    Write(ChatMessageType.Error, $"Command '{textBox.Text}' not found.");
                    
                    Console.WriteLine($"Player attempted to execute nonexistent command '{textBox.Text}'.");
                    
                    if (!isUserScrolling)
                        scrollOffset = 0;
                }
            }
            else
            {
                Write(ChatMessageType.Message, textBox.Text);
                Console.WriteLine($"Player typed: '{textBox.Text}'");
                
                if (!isUserScrolling)
                    scrollOffset = 0;
            }
            
            prevChatMessages.Add(textBox.Text);
            prevMsgTraversalIndex = prevChatMessages.Count;
            
            CloseChat();
            
            IsClosedButShown = true;
            elapsed = 0f;
        }
        
        if (Input.OpenChat())
            OpenChat();
        
        if (Input.OpenChatCmd())
            OpenChatCmd();
        
        if (Input.Pause())
            CloseChat();
        
        /*if (Input.PreviousMessage() && prevMsgTraversalIndex > 0 && prevChatMessages.Count > 0)
        {
            textBox.Text = prevChatMessages[prevMsgTraversalIndex];
            prevMsgTraversalIndex--;
        }
        
        if (Input.NewerMessage() && prevMsgTraversalIndex < prevChatMessages.Count)
        {
            textBox.Text = prevChatMessages[prevMsgTraversalIndex];
            prevMsgTraversalIndex++;
        }*/
        
        if (TerrainGeneration.DoneLoading)
            GameScene.PlayerCharacter.IsFrozen = IsOpen;
    }
    
    public void Draw()
    {
        if (IsClosedButShown)
            elapsed += Time.DeltaTime;

        if (elapsed >= disappearTime)
        {
            IsClosedButShown = false;
            elapsed -= disappearTime;
        }
        
        if (IsOpen || IsClosedButShown)
        {
            Raylib.BeginTextureMode(ChatTexture);
            Raylib.ClearBackground(new Color(0, 0, 0, 0));
            
            Raylib.DrawRectangleRec(chatBox, UI.MainColor);

            if (!isUserScrolling) scrollOffset = 0; // Auto-scroll if not user scrolling
            
            int start = Math.Max(0, ChatMessages.Count - maxLines - (int)scrollOffset);
            var toDisplay = ChatMessages.Skip(start).Take(maxLines);
            
            int index = 0;
            
            foreach (var msg in toDisplay)
            {
                Color msgColor = Color.Black;
                
                switch (msg.Type)
                {
                    case ChatMessageType.Message:
                        msgColor = Color.White;
                        break;
                    case ChatMessageType.Command:
                        msgColor = Color.SkyBlue;
                        break;
                    case ChatMessageType.Warning:
                        msgColor = Color.Yellow;
                        break;
                    case ChatMessageType.Error:
                        msgColor = Color.Red;
                        break;
                }
                
                Raylib.DrawTextEx(Engine.MainFont, msg.Message, new Vector2(0f, index * FontSize), FontSize, 1f, msgColor);

                index++;
            }
            
            Raylib.EndTextureMode();
            
            if (!IsClosedButShown)
                textBox.Draw();
        }
    }

    public void OpenChat()
    {
        if (!IsOpen)
        {
            CursorManager.ReleaseCursor();
            textBox.Text = string.Empty;
            textBox.IsFocused = true;
            IsOpen = true;
        }
    }
    
    public void OpenChatCmd()
    {
        if (!IsOpen)
        {
            CursorManager.ReleaseCursor();
            textBox.Text = "/";
            textBox.IsFocused = true;
            IsOpen = true;
        }
    }

    public void CloseChat()
    {
        CursorManager.LockCursor();
        textBox.Text = string.Empty;
        textBox.IsFocused = false;
        IsOpen = false;
    }
    
    public static void Write(ChatMessageType type, string message)
    {
        var msg = new ChatMessage(type, message);
        ChatMessages.Add(msg);
    }

    public static void WriteHelp()
    {
        foreach (var cmd in Commands.Registry)
            ChatMessages.Add(new ChatMessage(ChatMessageType.Command, $"{cmd.Key.Name}: {cmd.Key.Description}"));
    }
}