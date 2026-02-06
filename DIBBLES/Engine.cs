using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Diagnostics;
using DIBBLES.Gameplay;
using DIBBLES.Scenes;
using DIBBLES.Systems;
using DIBBLES.Utils;
using Debug = DIBBLES.Utils.Debug;

namespace DIBBLES;

public class Engine : Game
{
    public const int ScreenWidth = 1600;
    public const int ScreenHeight = 900;
    public const int FPS = 165;
    public const float FrameTimestep = 1.0f / (float)FPS;

    public static Engine Instance { get; private set; }
    
    public static bool IsRunning;
    public static bool IsPaused;
    
    public static GraphicsDevice Graphics;
    public static SpriteBatch Sprites;
    
    public static SpriteFont MainFont;
    public static List<Scene> Scenes = new();
    public static List<AudioPlayer> AudioPlayers = new();

    private static GraphicsDeviceManager GraphicsManager;
    
    private Stopwatch timer = new();
    private long previousTicks;
    
    public Engine()
    {
        Instance = this;
        
        GraphicsManager = new GraphicsDeviceManager(this);
        
        GraphicsManager.PreferredBackBufferWidth = ScreenWidth;
        GraphicsManager.PreferredBackBufferHeight = ScreenHeight;
        GraphicsManager.SynchronizeWithVerticalRetrace = false; // We'll do custom frame cap
        GraphicsManager.GraphicsProfile = GraphicsProfile.HiDef;
        
        Content.RootDirectory = "Assets/MG";
        
        IsMouseVisible = true;
        IsFixedTimeStep = false;

        timer.Start();
        previousTicks = timer.ElapsedTicks;
        
        Debug.Start();
        
        //TargetElapsedTime = TimeSpan.FromSeconds(FrameTimestep);
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
        
        MainFont = Content.Load<SpriteFont>("Fonts/MainFont");
        MainFont = TextureUtils.AlterSpriteFont(MainFont, ' ', 8f);
        
        var voxelScene = new GameScene();
        
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
        
        if ((!Chat.IsOpen && Input.Quit()))
            Exit();
        
        foreach (var scene in Scenes)
            scene.Update();

        foreach (var audioPlayer in AudioPlayers.ToList()) // Must do ToList because it crashes otherwise
            audioPlayer.Update();
        
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

        previousTicks = afterWait; // Update to the end of the frame

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
        
        Debug.Close();
    }
}