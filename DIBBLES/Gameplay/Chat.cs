using DIBBLES.Systems;
using DIBBLES.Utils;
using Raylib_cs;

namespace DIBBLES.Gameplay;

public class Chat
{
    private Rectangle chatBox = new Rectangle(0f, 0f, 800f, 400f);
    private bool isChatOpen = false;
    
    public void Start()
    {
        chatBox.X = UI.BottomLeftPivot.X;
        chatBox.Y = UI.BottomLeftPivot.Y - (chatBox.Height + 200f);
    }

    public void Update()
    {
        if (Input.OpenChat())
            isChatOpen = !isChatOpen;

        Engine.IsPaused = isChatOpen;
    }

    public void Draw()
    {
        if (isChatOpen)
        {
            Raylib.DrawRectangleRec(chatBox, new Color(0f,0f,0f, 0.5f));
        }
    }
}