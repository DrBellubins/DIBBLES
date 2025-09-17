using System.Net.Mime;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Terrain;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay;

public class Chat
{
    private Rectangle chatBox = new Rectangle(0f, 0f, 800f, 400f);
    private bool isChatOpen = false;
    
    private TextBox textBox = new TextBox(new Rectangle(0f, 0f, 800f, 40f));
    
    public void Start()
    {
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
            foreach (var command in Commands.Registry)
            {
                if (textBox.Text == command.Key)
                    command.Value();
                else
                    Console.WriteLine($"Command '{textBox.Text}' not found."); // TODO: Write to chat
            }
            
            textBox.Text = string.Empty;
        }
        
        if (TerrainGeneration.DoneLoading)
            GameScene.PlayerCharacter.IsFrozen = isChatOpen;
    }

    public void Draw()
    {
        if (isChatOpen)
        {
            Raylib.DrawRectangleRec(chatBox, UI.MainColor);
            textBox.Draw();
        }
    }
}