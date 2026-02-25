using Microsoft.Xna.Framework;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using static DIBBLES.Gameplay.Inventory.InventorySystem;

namespace DIBBLES.Gameplay;

public enum ChatMessageType
{
    Message,
    Command,
    CommandHeader,
    Warning,
    Error
}

public struct ChatMessage(string message, ChatMessageType type)
{
    public ChatMessageType Type = type;
    public string Message = message;
}

// TODO: Implement scrolling.

// TODO: Implement text wrapping.
public class Chat
{
    public const float Width = 800f;
    public const float Height = 400f;
    public const float FontSize = 24f;
    
    public static bool IsOpen {get; private set;}
    public static bool IsClosedButShown {get; private set;}
    
    public static List<ChatMessage> ChatMessages = new();
    
    private static List<string> prevChatMessages = new();
    
    public RenderTarget2D ChatTexture;
    
    private RectangleF chatBox = new RectangleF(0f, 0f, Width, Height);
    private TextBox textBox = new TextBox(new RectangleF(0f, 0f, Width, 40f));
    
    public float heightPos = UI.LeftCenterPivot.Y - (Height / 2f);
    
    // Chat disappear timer
    private float elapsed = 0f;
    private const float disappearTime = 2.5f;
    
    // Previous message traversal
    private int prevMsgTraversalIndex = 0;
    
    // Chat text/scrolling checks
    private static float scrollOffset = 0;
    private bool isUserScrolling = false;
    
    public void Start()
    {
        ChatTexture = new RenderTarget2D(
            Engine.Graphics,
            (int)Width,
            (int)Height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents // Allows transparency
        );
        
        textBox.Bounds.X = (int)UI.LeftCenterPivot.X;
        textBox.Bounds.Y = (int)(UI.LeftCenterPivot.Y + (Height / 2f));
    }

    public void Update()
    {
        if (IsClosedButShown)
        {
            IsClosedButShown = !StateMachine.IsAnyOtherInventoryOpen(UIState.Chat);
            elapsed += Time.DeltaTime;
        }

        if (elapsed >= disappearTime)
        {
            IsClosedButShown = false;
            elapsed -= disappearTime;
        }
        
        int linesThatFit = (int)(Height / FontSize);
        int maxScroll = Math.Max(0, ChatMessages.Count - linesThatFit);
        
        if (IsOpen)
        {
            textBox.Update();
            
            float wheel = Input.ScrollDelta();
            
            if (wheel != 0)
            {
                scrollOffset += wheel;
                scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);
                
                isUserScrolling = scrollOffset > 0;
            }
        }
        
        // Send msg/cmd to chat
        if (Input.SendChat() && textBox.Text != string.Empty)
        {
            if (textBox.Text.StartsWith("/"))
            {
                var input = textBox.Text[1..];
                var split = input.Split(' ', 2);
                var cmdName = split[0].ToLower();
                var args = split.Length > 1 ? split[1].Split(' ') : Array.Empty<string>();

                if (Commands.Registry.TryGetValue(cmdName, out var cmd))
                {
                    cmd.Handler(args);
                    Debug.Info($"Player executed command '{textBox.Text}'.");
                }
                else
                {
                    Write($"Unknown command: {cmdName}", ChatMessageType.Error);
                    Debug.Warning($"Player attempted to execute nonexistent command '{textBox.Text}'");
                }
            }
            else
            {
                Write(textBox.Text, ChatMessageType.Message);
                Debug.Info($"Player typed: '{textBox.Text}'");
            }

            if (!isUserScrolling)
                scrollOffset = 0;
            
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
        
        // Up key: go to previous message
        if (Input.IsKeyPressed(Keys.Up) && prevChatMessages.Count > 0)
        {
            if (prevMsgTraversalIndex > 0)
                prevMsgTraversalIndex--;
            
            textBox.Text = prevChatMessages[prevMsgTraversalIndex];
        }

        // Down key: go to next message (or empty if at end)
        if (Input.IsKeyPressed(Keys.Down) && prevChatMessages.Count > 0)
        {
            if (prevMsgTraversalIndex < prevChatMessages.Count - 1)
            {
                prevMsgTraversalIndex++;
                textBox.Text = prevChatMessages[prevMsgTraversalIndex];
            }
            else
            {
                prevMsgTraversalIndex = prevChatMessages.Count;
                textBox.Text = "";
            }
        }
    }

    public void DrawBG()
    {
        chatBox.X = (int)UI.LeftCenterPivot.X;
        chatBox.Y = (int)heightPos;
        
        if (IsOpen || IsClosedButShown)
            UIBatch.DrawRect(chatBox, UI.MainColor);
    }
    
    public void Draw()
    {
        if (IsOpen || IsClosedButShown)
        {
            // Draw messages
            int linesThatFit = (int)(Height / FontSize);
            int maxScroll = Math.Max(0, ChatMessages.Count - linesThatFit);
            int start = Math.Max(0, ChatMessages.Count - linesThatFit - (int)scrollOffset);

            if (start < 0)
                start = 0;
            
            if (start > maxScroll)
                start = maxScroll;
            
            var toDisplay = ChatMessages.Skip(start).Take(linesThatFit);
            
            int index = 0;
            foreach (var msg in toDisplay)
            {
                var color = GetMsgColor(msg.Type);
                var pos = new Vector2(0f, heightPos + (index * FontSize));
            
                UIBatch.DrawString(Engine.MainFont, msg.Message, pos, color);
            
                index++;
            }
            
            textBox.Draw();
        }
    }

    // Utility: get color by message type
    private Color GetMsgColor(ChatMessageType type)
    {
        return type switch
        {
            ChatMessageType.Message => Color.White,
            ChatMessageType.Command => Color.SkyBlue,
            ChatMessageType.CommandHeader => Color.Purple,
            ChatMessageType.Warning => Color.Yellow,
            ChatMessageType.Error => Color.Red,
            _ => Color.White
        };
    }
    
    public void OpenChat()
    {
        if (!IsOpen && StateMachine.Open(UIState.Chat))
        {
            textBox.Text = string.Empty;
            textBox.IsFocused = true;
            IsOpen = true;
        }
    }
    
    public void OpenChatCmd()
    {
        if (!IsOpen && StateMachine.Open(UIState.Chat))
        {
            textBox.Text = "/";
            textBox.IsFocused = true;
            IsOpen = true;
        }
    }

    public void CloseChat()
    {
        StateMachine.Close(UIState.Chat);
        textBox.Clear();
        textBox.IsFocused = false;
        IsOpen = false;
    }
    
    public static void Write(string message, ChatMessageType type)
    {
        IsClosedButShown = true;
        
        var msg = new ChatMessage(message, type);
        ChatMessages.Add(msg);
    }

    public static void WriteHelp(string[] args)
    {
        foreach (var cmd in Commands.Registry)
            ChatMessages.Add(new ChatMessage($"/{cmd.Value.Name}: {cmd.Value.Description}", ChatMessageType.Command));
    }
}