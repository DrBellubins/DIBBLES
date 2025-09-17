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
    Warning,
    Error
}

public struct ChatMessage(ChatMessageType type, string message)
{
    public ChatMessageType Type = type;
    public string Message = message;
}

public class Chat
{
    public const int Width = 800;
    public const int Height = 400;
    public const float FontSize = 24f;
    
    public static List<ChatMessage> ChatMessages = new();
    
    private Rectangle chatBox = new Rectangle(0f, 0f, Width, Height);
    private bool isChatOpen = false;
    
    private TextBox textBox = new TextBox(new Rectangle(0f, 0f, Width, 40f));

    private RenderTexture2D chatTexture;

    private float heightPos = UI.LeftCenterPivot.Y - (Height / 2f);
    
    private float scrollOffset = 0;
    private bool isUserScrolling = false;
    
    private static int maxLines = (int)(Height / FontSize);
    private static int maxScroll = Math.Max(0, ChatMessages.Count - maxLines);
    
    public void Start()
    {
        chatTexture = Raylib.LoadRenderTexture(Width, Height);
        
        textBox.Bounds.X = UI.LeftCenterPivot.X;
        textBox.Bounds.Y = UI.LeftCenterPivot.Y + (Height / 2f);
    }

    public void Update()
    {
        if (Input.OpenChat())
            OpenChat();
        
        if (Input.Pause())
            CloseChat();

        if (isChatOpen)
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
        
        if (Input.SendChat() && textBox.Text != string.Empty)
        {
            if (textBox.Text[0] == '/')
            {
                if (Commands.Registry.TryGetValue(textBox.Text, out var command))
                    command();
                else
                {
                    Write(ChatMessageType.Error, $"Command '{textBox.Text}' not found.");
                    
                    if (!isUserScrolling)
                        scrollOffset = 0;
                }
            }
            else
            {
                Write(ChatMessageType.Message, textBox.Text);
                
                if (!isUserScrolling)
                    scrollOffset = 0;
            }
            
            textBox.Text = string.Empty;
            textBox.IsFocused = false;
        }
        
        if (TerrainGeneration.DoneLoading)
            GameScene.PlayerCharacter.IsFrozen = isChatOpen;
    }

    public void Draw()
    {
        if (isChatOpen)
        {
            Raylib.BeginTextureMode(chatTexture);
            
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
            
            Raylib.DrawTextureRec(
                chatTexture.Texture,
                new Rectangle(0, 0, chatTexture.Texture.Width, -chatTexture.Texture.Height),
                new Vector2(0f, heightPos),
                Color.White
            );
            
            textBox.Draw();
        }
    }

    public void OpenChat()
    {
        CursorManager.ReleaseCursor();
        textBox.IsFocused = true;
        isChatOpen = true;
    }

    public void CloseChat()
    {
        CursorManager.LockCursor();
        textBox.Text = string.Empty;
        textBox.IsFocused = false;
        isChatOpen = false;
    }
    
    public static void Write(ChatMessageType type, string message)
    {
        var msg = new ChatMessage(type, message);
        ChatMessages.Add(msg);
    }
}