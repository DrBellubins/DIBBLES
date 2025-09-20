using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using DIBBLES.Gameplay;
using DIBBLES.Scenes;
using DIBBLES.Systems;

namespace DIBBLES;

public class MonoEngine : Game
{
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 900;
    public const int FPS = 165;
    public const float FrameTimestep = 1.0f / (float)FPS;

    public static MonoEngine Instance { get; private set; }
    
    public static bool IsRunning;
    public static bool IsPaused;
    
    public static GraphicsDevice Graphics;
    public static SpriteBatch Sprites;
    
    public static SpriteFont MainFont;
    public static List<Scene> Scenes = new();

    private static GraphicsDeviceManager GraphicsManager;
    
    public MonoEngine()
    {
        Instance = this;
        
        GraphicsManager = new GraphicsDeviceManager(this);
        
        GraphicsManager.PreferredBackBufferWidth = ScreenWidth;
        GraphicsManager.PreferredBackBufferHeight = ScreenHeight;
        GraphicsManager.SynchronizeWithVerticalRetrace = false; // We'll do custom frame cap
        
        Content.RootDirectory = "Content";
        
        IsMouseVisible = true;
        IsFixedTimeStep = false;
    }

    protected override void Initialize()
    {
        base.Initialize();

        IsRunning = true;
    }

    protected override void LoadContent()
    {
        Graphics = GraphicsManager.GraphicsDevice;
        
        Sprites = new SpriteBatch(GraphicsDevice);
        MainFont = Content.Load<SpriteFont>("MainFont");
        
        var voxelScene = new GameSceneMono();
        
        foreach (var scene in Scenes)
            scene.Start();
    }

    protected override void Update(GameTime gameTime)
    {
        if (!IsActive || !IsRunning)
        {
            base.Update(gameTime);
            return;
        }
        
        if ((!Chat.IsOpen && InputMono.Quit()))
            Exit();
        
        Time.DeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Time.time += Time.DeltaTime;
        
        foreach (var scene in Scenes)
            scene.Update();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        foreach (var scene in Scenes)
            scene.Draw();
        
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        // TODO: Port resource cleanup
        base.UnloadContent();
    }
}