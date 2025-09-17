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
        if (Input.OpenChat())
        {
            isChatOpen = !isChatOpen;
            
            if (isChatOpen)
                CursorManager.ReleaseCursor();
            else
                CursorManager.LockCursor();
        }

        if (TerrainGeneration.DoneLoading)
            GameScene.PlayerCharacter.IsFrozen = isChatOpen;

        if (isChatOpen)
        {
            textBox.Update();
        }
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