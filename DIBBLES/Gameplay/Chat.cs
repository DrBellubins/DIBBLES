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
    
    public void Start()
    {
        chatTexture = Raylib.LoadRenderTexture(Width, Height);
        
        textBox.Bounds.X = UI.LeftCenterPivot.X;
        textBox.Bounds.Y = UI.LeftCenterPivot.Y + (Height / 2f);
    }

    public void Update()
    {
        if (Input.Pause())
        {
            CursorManager.LockCursor();
            textBox.IsFocused = false;
            isChatOpen = false;
        }
        
        if (Input.OpenChat())
        {
            CursorManager.ReleaseCursor();
            textBox.Text = string.Empty;
            textBox.IsFocused = true;
            isChatOpen = true;
        }
        
        if (isChatOpen)
            textBox.Update();
        
        if (Input.SendChat() && textBox.Text != string.Empty)
        {
            if (textBox.Text[0] == '/')
            {
                if (Commands.Registry.TryGetValue(textBox.Text, out var command))
                    command();
                else
                    Write(ChatMessageType.Error, $"Command '{textBox.Text}' not found.");
            }
            else
                Write(ChatMessageType.Message, textBox.Text);
            
            textBox.Text = string.Empty;
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

            int index = 0;
            
            foreach (var msg in ChatMessages)
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

    public static void Write(ChatMessageType type, string message)
    {
        var msg = new ChatMessage(type, message);
        ChatMessages.Add(msg);
    }
}