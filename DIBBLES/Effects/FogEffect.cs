using System.Numerics;
using DIBBLES.Gameplay.Player;
using DIBBLES.Scenes;
using Raylib_cs;

namespace DIBBLES.Effects;

public class FogEffect
{
    private Shader fogShader;
    private RenderTexture2D target;
    private int sceneTexLoc;
    private int depthTexLoc;
    private int zNearLoc, zFarLoc, fogNearLoc, fogFarLoc, fogColorLoc;
    private int invProjLoc, invViewLoc, cameraPosLoc;
    
    public const float FogNear = 50.0f;
    public const float FogFar = 150.0f;
    public static Vector4 FogColor = new Vector4(0.4f, 0.7f, 1.0f, 1.0f);
    
    //private Vector4 fogColor = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);

    public void Start()
    {
        fogShader = Resource.LoadShader(null, "fog.fs");
        
        target = loadRenderTextureWithDepth(Engine.ScreenWidth, Engine.ScreenHeight);

        sceneTexLoc = Raylib.GetShaderLocation(fogShader, "sceneTex");
        depthTexLoc = Raylib.GetShaderLocation(fogShader, "depthTex");
        
        zNearLoc = Raylib.GetShaderLocation(fogShader, "zNear");
        zFarLoc = Raylib.GetShaderLocation(fogShader, "zFar");
        
        fogNearLoc = Raylib.GetShaderLocation(fogShader, "fogNear");
        fogFarLoc = Raylib.GetShaderLocation(fogShader, "fogFar");
        fogColorLoc = Raylib.GetShaderLocation(fogShader, "fogColor");
        
        invProjLoc = Raylib.GetShaderLocation(fogShader, "invProj");
        invViewLoc = Raylib.GetShaderLocation(fogShader, "invView");
        cameraPosLoc = Raylib.GetShaderLocation(fogShader, "cameraPos");
        
        Console.WriteLine($"Cull plane: {(float)Rlgl.GetCullDistanceNear()}, {(float)Rlgl.GetCullDistanceFar()}");
        
        Raylib.SetShaderValue(fogShader, zNearLoc, (float)Rlgl.GetCullDistanceNear(), ShaderUniformDataType.Float);
        Raylib.SetShaderValue(fogShader, zFarLoc, (float)Rlgl.GetCullDistanceFar(), ShaderUniformDataType.Float);
        
        Raylib.SetShaderValue(fogShader, fogNearLoc, FogNear, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(fogShader, fogFarLoc, FogFar, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(fogShader, fogColorLoc, FogColor, ShaderUniformDataType.Vec4);
    }

    public void DrawStart()
    {
        Rlgl.EnableDepthMask();
        Raylib.BeginTextureMode(target);
    }

    public void DrawEnd()
    {
        Raylib.EndTextureMode();
        Rlgl.DisableDepthMask();

        Raylib.BeginShaderMode(fogShader);

        // Set scene color tex (color buffer)
        Raylib.SetShaderValueTexture(fogShader, sceneTexLoc, target.Texture);

        // Attach the depth texture
        Raylib.SetShaderValueTexture(fogShader, depthTexLoc, target.Depth);
        
        // Pass matrices and camera position
        var aspect = (float)Engine.ScreenWidth / (float)Engine.ScreenHeight;
        var proj = Raylib.GetCameraProjectionMatrix(ref GameScene.PlayerCharacter.Camera, aspect);
        var view = Raylib.GetCameraViewMatrix(ref GameScene.PlayerCharacter.Camera);
        var invProj = Matrix4x4.Invert(proj, out var iproj) ? iproj : Matrix4x4.Identity;
        var invView = Matrix4x4.Invert(view, out var iview) ? iview : Matrix4x4.Identity;
        var camPos = GameScene.PlayerCharacter.Camera.Position;

        Raylib.SetShaderValueMatrix(fogShader, invProjLoc, invProj);
        Raylib.SetShaderValueMatrix(fogShader, invViewLoc, invView);
        Raylib.SetShaderValue(fogShader, cameraPosLoc, camPos, ShaderUniformDataType.Vec3);

        // Draw fullscreen quad with shader
        Raylib.DrawTextureRec(target.Texture, new Rectangle(0, 0, Engine.ScreenWidth, -Engine.ScreenHeight), Vector2.Zero, Color.White);

        Rlgl.DisableTexture();
        Raylib.EndShaderMode();
    }

    private RenderTexture2D loadRenderTextureWithDepth(int width, int height)
    {
        RenderTexture2D target = new RenderTexture2D();

        target.Id = Rlgl.LoadFramebuffer(); // Load an empty framebuffer

        if (target.Id > 0)
        {
            Rlgl.EnableFramebuffer(target.Id);

            // Create color texture (default to RGBA)
            unsafe
            {
                target.Texture.Id = Rlgl.LoadTexture((void*)0, width, height, PixelFormat.UncompressedR8G8B8A8, 1);
            }
            
            target.Texture.Width = width;
            target.Texture.Height = height;
            target.Texture.Format = PixelFormat.UncompressedR8G8B8A8;
            target.Texture.Mipmaps = 1;

            // Create depth texture
            target.Depth.Id = Rlgl.LoadTextureDepth(width, height, false);
            target.Depth.Width = width;
            target.Depth.Height = height;
            target.Depth.Format = PixelFormat.UncompressedR32; // Closest available in raylib-cs
            target.Depth.Mipmaps = 1;

            // Attach color texture and depth texture to FBO
            Rlgl.FramebufferAttach(target.Id, target.Texture.Id,
                FramebufferAttachType.ColorChannel0,
                FramebufferAttachTextureType.Texture2D, 0);

            Rlgl.FramebufferAttach(target.Id, target.Depth.Id,
                FramebufferAttachType.Depth,
                FramebufferAttachTextureType.Texture2D, 0);

            // Check if fbo is complete with attachments (valid)
            if (Rlgl.FramebufferComplete(target.Id))
                Raylib.TraceLog(TraceLogLevel.Info, $"FBO: [ID {target.Id}] Framebuffer object created successfully");

            Rlgl.DisableFramebuffer();
        }
        else
        {
            Raylib.TraceLog(TraceLogLevel.Warning, "FBO: Framebuffer object can not be created");
        }

        return target;
    }
    
    public void Unload()
    {
        Raylib.UnloadShader(fogShader);
        Raylib.UnloadRenderTexture(target);
    }
}