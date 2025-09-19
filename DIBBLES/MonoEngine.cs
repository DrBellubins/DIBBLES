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

    public static bool IsRunning;
    public static bool IsPaused;

    public static GraphicsDeviceManager GraphicsManager;
    public static GraphicsDevice Graphics;
    
    public static SpriteBatch Sprites;
    
    public static SpriteFont MainFont;
    
    // Custom deltaTime logic
    private Stopwatch timer = new();
    private long previousTicks = 0;

    public static List<Scene> Scenes = new();

    public MonoEngine()
    {
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

        timer.Start();
        previousTicks = timer.ElapsedTicks;

        IsRunning = true;
    }

    protected override void LoadContent()
    {
        Graphics = GraphicsManager.GraphicsDevice;
        
        Graphics.SamplerStates[0] = SamplerState.PointClamp;
        
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
        
        foreach (var scene in Scenes)
            scene.Update();

        // Cap frame rate with optimized spin-wait
        long targetTicks = (long)(FrameTimestep * (double)Stopwatch.Frequency); // Use double for precision
        long beforeWait = timer.ElapsedTicks;
        long elapsedTicks = beforeWait - previousTicks;
        int spinCount = 0;
            
        while (elapsedTicks < targetTicks)
        {
            Thread.SpinWait(100); // Brief spin-wait to reduce CPU usage
            elapsedTicks = timer.ElapsedTicks - previousTicks;
            spinCount++;
        }
            
        long afterWait = timer.ElapsedTicks;
            
        // Calculate DeltaTime after spin-wait to include wait time
        Time.DeltaTime = (afterWait - previousTicks) / (float)Stopwatch.Frequency;
        Time.time += Time.DeltaTime;

        // TODO: Port input and game logic update

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