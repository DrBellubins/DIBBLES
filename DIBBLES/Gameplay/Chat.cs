using Microsoft.Xna.Framework;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Microsoft.Xna.Framework.Graphics;

namespace DIBBLES.Gameplay;

public enum ChatMessageType
{
    Message,
    Command,
    CommandHeader,
    Warning,
    Error
}

public struct ChatMessage(ChatMessageType type, string message)
{
    public ChatMessageType Type = type;
    public string Message = message;
}

// TODO: Text box sometimes disappears
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
    
    public RenderTarget2D ChatTexture;
    
    private Rectangle chatBox = new Rectangle(0, 0, Width, Height);
    private TextBoxMono textBox = new TextBoxMono(new Rectangle(0, 0, Width, 40));
    
    public float heightPos = UI.LeftCenterPivot.Y - (Height / 2f);
    
    // Chat disappear timer
    private float elapsed = 0f;
    private const float disappearTime = 2.5f;
    
    // Previous message traversal
    private int prevMsgTraversalIndex = 0;
    
    // Chat text/scrolling checks
    private float scrollOffset = 0;
    private bool isUserScrolling = false;
    
    private static int maxLines = (int)(Height / FontSize);
    private static int maxScroll = Math.Max(0, ChatMessages.Count - maxLines);
    
    public void Start()
    {
        ChatTexture = new RenderTarget2D(
            MonoEngine.Graphics,
            Width,
            Height,
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
        if (IsOpen)
        {
            textBox.Update();
            
            float wheel = InputMono.ScrollDelta();
            
            if (wheel != 0)
            {
                scrollOffset += wheel;
                
                maxScroll = Math.Max(0, ChatMessages.Count - maxLines);
                
                scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);
                isUserScrolling = scrollOffset > 0;
            }
        }
        
        // Send msg/cmd to chat
        if (InputMono.SendChat() && textBox.Text != string.Empty)
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
                    Console.WriteLine($"Player executed command '{textBox.Text}'.");
                }
                else
                {
                    Write(ChatMessageType.Error, $"Unknown command: {cmdName}");
                    Console.WriteLine($"Player attempted to execute nonexistent command '{textBox.Text}'.");
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
        
        if (InputMono.OpenChat())
            OpenChat();
        
        if (InputMono.OpenChatCmd())
            OpenChatCmd();
        
        if (InputMono.Pause())
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
            GameSceneMono.PlayerCharacter.IsFrozen = IsOpen;
    }

    public void DrawBG()
    {
        var sprites = MonoEngine.Sprites;
        
        chatBox.X = (int)UI.LeftCenterPivot.X;
        chatBox.Y = (int)heightPos;
        
        MonoEngine.Sprites.Draw(TextureUtils.GetWhitePixel(), new Vector2(chatBox.X, chatBox.Y), UI.MainColor);
    }
    
    public void Draw()
    {
        var sprites = MonoEngine.Sprites;
        
        // 1. Draw chat background and text to offscreen RenderTarget2D
        //MonoEngine.Graphics.SetRenderTarget(ChatTexture);
        //MonoEngine.Graphics.Clear(Color.Transparent);
        
        // Draw background rectangle (optional, for chat background)
        sprites.Draw(
            TextureUtils.GetWhitePixel(), // 1x1 white pixel
            new Rectangle(0, 0, Width, Height),
            new Color(0, 0, 0, 180) // semi-transparent black
        );

        // Draw messages
        int maxLines = (int)(Height / FontSize);
        int start = Math.Max(0, ChatMessages.Count - maxLines); // auto-scroll
        var toDisplay = ChatMessages.Skip(start).Take(maxLines);

        int index = 0;
        
        foreach (var msg in toDisplay)
        {
            var color = GetMsgColor(msg.Type);
            var pos = new Vector2(0f, index * FontSize);
            
            sprites.DrawString(MonoEngine.MainFont, msg.Message, pos, color);
            
            index++;
        }

        // 2. Reset render target to backbuffer
        //MonoEngine.Graphics.SetRenderTarget(null);

        // 3. Draw the chat texture to the screen (e.g. bottom left)
        sprites.Draw(
            ChatTexture,
            new Vector2(20f, MonoEngine.ScreenHeight - Height - 20f), // adjust as needed
            Color.White
        );
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
            ChatMessages.Add(new ChatMessage(ChatMessageType.Command, $"/{cmd.Value.Name}: {cmd.Value.Description}"));
    }
}