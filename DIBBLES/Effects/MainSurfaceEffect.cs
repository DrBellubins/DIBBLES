using DIBBLES.Gameplay;
using DIBBLES.Gameplay.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using DIBBLES.Systems;

namespace DIBBLES.Effects;

// Wrapper that mimics MonoGame BasicEffect’s convenience API,
// but drives the MRT-writing MainSurfaceEffect.fx.
public class MainSurfaceEffect
{
    public Effect Effect
    {
        get { return _effect; }
    }

    // Flags
    public bool TextureEnabled
    {
        get { return _textureEnabled; }
        set
        {
            if (_textureEnabled != value)
            {
                _textureEnabled = value;
                _dirtyShaderIndex = true;
            }
        }
    }

    public bool VertexColorEnabled
    {
        get { return _vertexColorEnabled; }
        set
        {
            if (_vertexColorEnabled != value)
            {
                _vertexColorEnabled = value;
                _dirtyShaderIndex = true;
            }
        }
    }

    public bool FogEnabled
    {
        get { return _fogEnabled; }
        set
        {
            if (_fogEnabled != value)
            {
                _fogEnabled = value;
                _dirtyShaderIndex = true;
            }
        }
    }

    // Matrices
    public Matrix World
    {
        get { return _world; }
        set
        {
            _world = value;
            _dirtyMatrices = true;
        }
    }

    public Matrix View
    {
        get { return _view; }
        set
        {
            _view = value;
            _dirtyMatrices = true;
        }
    }

    public Matrix Projection
    {
        get { return _projection; }
        set
        {
            _projection = value;
            _dirtyMatrices = true;
        }
    }

    // Colors
    public Vector4 DiffuseColor
    {
        get { return _diffuseColor; }
        set
        {
            _diffuseColor = value;
            _dirtyMaterial = true;
        }
    }

    // Texture
    public Texture2D Texture
    {
        get { return _texture; }
        set
        {
            _texture = value;
            _dirtyMaterial = true;
        }
    }

    // Camera and Fog params
    public Vector3 CameraPos
    {
        get { return _cameraPos; }
        set
        {
            _cameraPos = value;
            _dirtyFogCam = true;
        }
    }

    public float CameraNear
    {
        get { return _cameraNear; }
        set
        {
            _cameraNear = value;
            _dirtyFogCam = true;
        }
    }

    public float CameraFar
    {
        get { return _cameraFar; }
        set
        {
            _cameraFar = value;
            _dirtyFogCam = true;
        }
    }

    public float FogNear
    {
        get { return _fogNear; }
        set
        {
            _fogNear = value;
            _dirtyFogCam = true;
        }
    }

    public float FogFar
    {
        get { return _fogFar; }
        set
        {
            _fogFar = value;
            _dirtyFogCam = true;
        }
    }

    public Vector3 HorizonColor
    {
        get { return _horizonColor; }
        set
        {
            _horizonColor = value;
            _dirtyFogCam = true;
        }
    }
    
    public Vector3 ZenithZolor
    {
        get { return _zenithColor; }
        set
        {
            _zenithColor = value;
            _dirtyFogCam = true;
        }
    }

    // Ctor and parameter cache
    public MainSurfaceEffect(GraphicsDevice device)
    {
        _effect = Engine.Instance.Content.Load<Effect>("Shaders/MainSurfaceEffect");

        _pWorld       = _effect.Parameters["World"];
        _pView        = _effect.Parameters["View"];
        _pProjection  = _effect.Parameters["Projection"];

        _pDiffuseTex  = _effect.Parameters["DiffuseTex"];
        _pDiffuseCol  = _effect.Parameters["DiffuseColor"];

        _pCameraPos   = _effect.Parameters["CameraPos"];
        _pCameraNear  = _effect.Parameters["CameraNear"];
        _pCameraFar   = _effect.Parameters["CameraFar"];

        _pFogNear     = _effect.Parameters["FogNear"];
        _pFogFar      = _effect.Parameters["FogFar"];
        
        _pHorizonColor    = _effect.Parameters["SkyHorizonColor"];
        _pZenithColor     = _effect.Parameters["SkyZenithColor"];

        // Defaults
        _world = Matrix.Identity;
        _view = Matrix.Identity;
        _projection = Matrix.Identity;

        _diffuseColor = Color.White.ToVector4();
        
        _horizonColor = new Color(0, 0, 0).ToVector3();
        _zenithColor = new Color(0, 0, 0).ToVector3();

        _cameraNear = 0.01f;
        _cameraFar  = 1000.0f;

        _fogNear = 0.0f;
        _fogFar  = 0.0f;

        _textureEnabled = false;
        _vertexColorEnabled = false;
        _fogEnabled = true;

        _dirtyMatrices = true;
        _dirtyMaterial = true;
        _dirtyFogCam   = true;
        _dirtyShaderIndex = true;
    }

    // Call before drawing; sets parameters and selects technique based on flags
    public void Apply()
    {
        if (_dirtyShaderIndex)
        {
            selectTechnique();
            _dirtyShaderIndex = false;
        }

        if (_dirtyMatrices)
        {
            _pWorld?.SetValue(_world);
            _pView?.SetValue(_view);
            _pProjection?.SetValue(_projection);
            _dirtyMatrices = false;
        }

        if (_dirtyMaterial)
        {
            _pDiffuseCol?.SetValue(_diffuseColor);
            _pDiffuseTex?.SetValue(_texture);
            _dirtyMaterial = false;
        }

        if (_dirtyFogCam)
        {
            _pCameraPos?.SetValue(_cameraPos);
            _pCameraNear?.SetValue(_cameraNear);
            _pCameraFar?.SetValue(_cameraFar);

            _pFogNear?.SetValue(_fogNear);
            _pFogFar?.SetValue(_fogFar);
            
            _pHorizonColor?.SetValue(_horizonColor);
            _pZenithColor?.SetValue(_zenithColor);
            
            _dirtyFogCam = false;
        }
    }

    // Convenience to set camera/fog from current scene
    public void SetSceneCameraAndFog()
    {
        var cam = PlayerManager.Current.Camera;

        CameraPos = cam.Position.ToVector3();
        CameraNear = cam.NearPlane;
        CameraFar  = cam.FarPlane;

        FogNear = FogEffect.FogNear;
        FogFar  = FogEffect.FogFar;
        
        HorizonColor = DayNightCycle.HorizonColor.ToVector3();
        ZenithZolor = DayNightCycle.ZenithColor.ToVector3();
    }

    // Internals
    private void selectTechnique()
    {
        // Index mapping matching the technique table:
        // base = MainSurfaceEffect
        // +1 = NoFog
        // +2 = VertexColor
        // +4 = Texture
        int idx = 0;

        if (!_fogEnabled) idx += 1;
        if (_vertexColorEnabled) idx += 2;
        if (_textureEnabled) idx += 4;

        string[] names =
        {
            "MainSurfaceEffect",
            "MainSurfaceEffect_NoFog",
            "MainSurfaceEffect_VertexColor",
            "MainSurfaceEffect_VertexColor_NoFog",
            "MainSurfaceEffect_Texture",
            "MainSurfaceEffect_Texture_NoFog",
            "MainSurfaceEffect_Texture_VertexColor",
            "MainSurfaceEffect_Texture_VertexColor_NoFog"
        };

        // Clamp and select
        if (idx < 0 || idx >= names.Length)
            idx = 0;

        var tech = _effect.Techniques[names[idx]];

        if (tech != null)
            _effect.CurrentTechnique = tech;
    }
    
    public EffectTechnique CurrentTechnique
    {
        get
        {
            return _effect.CurrentTechnique;
        }
        set
        {
            _effect.CurrentTechnique = value;
        }
    }

    public EffectTechniqueCollection Techniques
    {
        get
        {
            return _effect.Techniques;
        }
    }

    private readonly Effect _effect;

    private EffectParameter _pWorld;
    private EffectParameter _pView;
    private EffectParameter _pProjection;

    private EffectParameter _pDiffuseTex;
    private EffectParameter _pDiffuseCol;

    private EffectParameter _pCameraPos;
    private EffectParameter _pCameraNear;
    private EffectParameter _pCameraFar;

    private EffectParameter _pFogNear;
    private EffectParameter _pFogFar;
    
    private EffectParameter _pHorizonColor;
    private EffectParameter _pZenithColor;

    private bool _textureEnabled;
    private bool _vertexColorEnabled;
    private bool _fogEnabled;

    private Matrix _world;
    private Matrix _view;
    private Matrix _projection;

    private Vector4 _diffuseColor;
    private Texture2D _texture;

    private Vector3 _cameraPos;
    private float _cameraNear;
    private float _cameraFar;

    private float _fogNear;
    private float _fogFar;
    
    private Vector3 _horizonColor;
    private Vector3 _zenithColor;

    private bool _dirtyMatrices;
    private bool _dirtyMaterial;
    private bool _dirtyFogCam;
    private bool _dirtyShaderIndex;
}