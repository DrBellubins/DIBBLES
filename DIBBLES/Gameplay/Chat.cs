using System.Net.Mime;
using System.Numerics;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay;

public class Chat
{
    public const int Width = 800;
    public const int Height = 400;
    
    private Rectangle chatBox = new Rectangle(0f, 0f, Width, Height);
    private bool isChatOpen = false;
    
    private TextBox textBox = new TextBox(new Rectangle(0f, 0f, Width, 40f));
    
    private RenderTexture2D chatTexture = new();
    
    public void Start()
    {
        chatTexture = Raylib.LoadRenderTexture(Width, Height);
        
        chatBox.X = UI.BottomLeftPivot.X;
        chatBox.Y = UI.BottomLeftPivot.Y - (chatBox.Height + 200f);
        
        textBox.Bounds.X = chatBox.X;
        textBox.Bounds.Y = chatBox.Y + 300f;
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
        
        if (Input.SendChat() && textBox.Text[0] == '/')
        {
            if (Commands.Registry.TryGetValue(textBox.Text, out var command))
                command();
            else
                Console.WriteLine($"Command '{textBox.Text}' not found."); // TODO: Write to chat
            
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
            textBox.Draw();
            
            Raylib.EndTextureMode();
            
            Raylib.DrawTextureV(chatTexture.Texture, new Vector2(chatBox.X, chatBox.Y), Color.White);
        }
    }
}