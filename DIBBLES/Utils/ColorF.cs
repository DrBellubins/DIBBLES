using Microsoft.Xna.Framework;

namespace DIBBLES.Utils;

public class ColorF
{
    public float R, G, B, A;

    public ColorF()
    {
        R = G = B = A = 0.0f;
    }

    public ColorF(ColorF color, float alpha)
    {
        R = color.R;
        G = color.G;
        B = color.B;
        A = alpha;
    }
    
    public ColorF(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public Color ToColor()
    {
        return new Color(R, G, B, A);
    }
    
    private static ColorF packedABGR(uint packed)
    {
        float a = ((packed >> 24) & 0xFF) / 255f;
        float b = ((packed >> 16) & 0xFF) / 255f;
        float g = ((packed >> 8)  & 0xFF) / 255f;
        float r = ( packed        & 0xFF) / 255f;

        return new ColorF(r, g, b, a);
    }
    
    static ColorF()
    {
        Transparent = packedABGR(0U);
        AliceBlue = packedABGR(4294965488U);
        AntiqueWhite = packedABGR(4292340730U);
        Aqua = packedABGR(4294967040U);
        Aquamarine = packedABGR(4292149119U);
        Azure = packedABGR(4294967280U);
        Beige = packedABGR(4292670965U);
        Bisque = packedABGR(4291093759U);
        Black = packedABGR(4278190080U /*0xFF000000*/);
        BlanchedAlmond = packedABGR(4291685375U);
        Blue = packedABGR(4294901760U);
        BlueViolet = packedABGR(4293012362U);
        Brown = packedABGR(4280953509U);
        BurlyWood = packedABGR(4287084766U);
        CadetBlue = packedABGR(4288716383U);
        Chartreuse = packedABGR(4278255487U);
        Chocolate = packedABGR(4280183250U);
        Coral = packedABGR(4283465727U);
        CornflowerBlue = packedABGR(4293760356U);
        Cornsilk = packedABGR(4292671743U);
        Crimson = packedABGR(4282127580U);
        Cyan = packedABGR(4294967040U);
        DarkBlue = packedABGR(4287299584U);
        DarkCyan = packedABGR(4287335168U);
        DarkGoldenrod = packedABGR(4278945464U);
        DarkGray = packedABGR(4289309097U);
        DarkGreen = packedABGR(4278215680U);
        DarkKhaki = packedABGR(4285249469U);
        DarkMagenta = packedABGR(4287299723U);
        DarkOliveGreen = packedABGR(4281297749U);
        DarkOrange = packedABGR(4278226175U);
        DarkOrchid = packedABGR(4291572377U);
        DarkRed = packedABGR(4278190219U);
        DarkSalmon = packedABGR(4286224105U);
        DarkSeaGreen = packedABGR(4287347855U);
        DarkSlateBlue = packedABGR(4287315272U);
        DarkSlateGray = packedABGR(4283387695U);
        DarkTurquoise = packedABGR(4291939840U);
        DarkViolet = packedABGR(4292018324U);
        DeepPink = packedABGR(4287829247U);
        DeepSkyBlue = packedABGR(4294950656U);
        DimGray = packedABGR(4285098345U);
        DodgerBlue = packedABGR(4294938654U);
        Firebrick = packedABGR(4280427186U);
        FloralWhite = packedABGR(4293982975U);
        ForestGreen = packedABGR(4280453922U);
        Fuchsia = packedABGR(4294902015U);
        Gainsboro = packedABGR(4292664540U);
        GhostWhite = packedABGR(4294965496U);
        Gold = packedABGR(4278245375U);
        Goldenrod = packedABGR(4280329690U);
        Gray = packedABGR(4286611584U);
        Green = packedABGR(4278222848U /*0xFF008000*/);
        GreenYellow = packedABGR(4281335725U);
        Honeydew = packedABGR(4293984240U /*0xFFF0FFF0*/);
        HotPink = packedABGR(4290013695U);
        IndianRed = packedABGR(4284243149U);
        Indigo = packedABGR(4286709835U);
        Ivory = packedABGR(4293984255U);
        Khaki = packedABGR(4287424240U);
        Lavender = packedABGR(4294633190U);
        LavenderBlush = packedABGR(4294308095U);
        LawnGreen = packedABGR(4278254716U);
        LemonChiffon = packedABGR(4291689215U);
        LightBlue = packedABGR(4293318829U);
        LightCoral = packedABGR(4286611696U);
        LightCyan = packedABGR(4294967264U);
        LightGoldenrodYellow = packedABGR(4292016890U);
        LightGray = packedABGR(4292072403U);
        LightGreen = packedABGR(4287688336U);
        LightPink = packedABGR(4290885375U);
        LightSalmon = packedABGR(4286226687U);
        LightSeaGreen = packedABGR(4289376800U);
        LightSkyBlue = packedABGR(4294626951U);
        LightSlateGray = packedABGR(4288252023U);
        LightSteelBlue = packedABGR(4292789424U);
        LightYellow = packedABGR(4292935679U);
        Lime = packedABGR(4278255360U /*0xFF00FF00*/);
        LimeGreen = packedABGR(4281519410U);
        Linen = packedABGR(4293325050U);
        Magenta = packedABGR(4294902015U);
        Maroon = packedABGR(4278190208U /*0xFF000080*/);
        MediumAquamarine = packedABGR(4289383782U);
        MediumBlue = packedABGR(4291624960U);
        MediumOrchid = packedABGR(4292040122U);
        MediumPurple = packedABGR(4292571283U);
        MediumSeaGreen = packedABGR(4285641532U);
        MediumSlateBlue = packedABGR(4293814395U);
        MediumSpringGreen = packedABGR(4288346624U);
        MediumTurquoise = packedABGR(4291613000U);
        MediumVioletRed = packedABGR(4286911943U);
        MidnightBlue = packedABGR(4285536537U);
        MintCream = packedABGR(4294639605U);
        MistyRose = packedABGR(4292994303U);
        Moccasin = packedABGR(4290110719U);
        MonoGameOrange = packedABGR(4278205671U);
        NavajoWhite = packedABGR(4289584895U);
        Navy = packedABGR(4286578688U /*0xFF800000*/);
        OldLace = packedABGR(4293326333U);
        Olive = packedABGR(4278222976U);
        OliveDrab = packedABGR(4280520299U);
        Orange = packedABGR(4278232575U);
        OrangeRed = packedABGR(4278207999U);
        Orchid = packedABGR(4292243674U);
        PaleGoldenrod = packedABGR(4289390830U);
        PaleGreen = packedABGR(4288215960U);
        PaleTurquoise = packedABGR(4293848751U);
        PaleVioletRed = packedABGR(4287852763U);
        PapayaWhip = packedABGR(4292210687U);
        PeachPuff = packedABGR(4290370303U);
        Peru = packedABGR(4282353101U);
        Pink = packedABGR(4291543295U);
        Plum = packedABGR(4292714717U);
        PowderBlue = packedABGR(4293320880U);
        Purple = packedABGR(4286578816U);
        Red = packedABGR(4278190335U);
        RosyBrown = packedABGR(4287598524U);
        RoyalBlue = packedABGR(4292962625U);
        SaddleBrown = packedABGR(4279453067U);
        Salmon = packedABGR(4285694202U);
        SandyBrown = packedABGR(4284523764U);
        SeaGreen = packedABGR(4283927342U);
        SeaShell = packedABGR(4293850623U);
        Sienna = packedABGR(4281160352U);
        Silver = packedABGR(4290822336U);
        SkyBlue = packedABGR(4293643911U);
        SlateBlue = packedABGR(4291648106U);
        SlateGray = packedABGR(4287660144U);
        Snow = packedABGR(4294638335U);
        SpringGreen = packedABGR(4286578432U);
        SteelBlue = packedABGR(4290019910U);
        Tan = packedABGR(4287411410U);
        Teal = packedABGR(4286611456U);
        Thistle = packedABGR(4292394968U);
        Tomato = packedABGR(4282868735U);
        Turquoise = packedABGR(4291878976U);
        Violet = packedABGR(4293821166U);
        Wheat = packedABGR(4289978101U);
        White = packedABGR(uint.MaxValue);
        WhiteSmoke = packedABGR(4294309365U);
        Yellow = packedABGR(4278255615U);
        YellowGreen = packedABGR(4281519514U);
    }
    
    #region ColorDefinitions
    
    /// <summary>Transparent color (R:0,G:0,B:0,A:0).</summary>
    public static ColorF Transparent { get; private set; }
    /// <summary>AliceBlue color (R:240,G:248,B:255,A:255).</summary>
    public static ColorF AliceBlue { get; private set; }
    /// <summary>AntiqueWhite color (R:250,G:235,B:215,A:255).</summary>
    public static ColorF AntiqueWhite { get; private set; }
    /// <summary>Aqua color (R:0,G:255,B:255,A:255).</summary>
    public static ColorF Aqua { get; private set; }
    /// <summary>Aquamarine color (R:127,G:255,B:212,A:255).</summary>
    public static ColorF Aquamarine { get; private set; }
    /// <summary>Azure color (R:240,G:255,B:255,A:255).</summary>
    public static ColorF Azure { get; private set; }
    /// <summary>Beige color (R:245,G:245,B:220,A:255).</summary>
    public static ColorF Beige { get; private set; }    
    /// <summary>Bisque color (R:255,G:228,B:196,A:255).</summary>
    public static ColorF Bisque { get; private set; }   
    /// <summary>Black color (R:0,G:0,B:0,A:255).</summary>
    public static ColorF Black { get; private set; }    
    /// <summary>BlanchedAlmond color (R:255,G:235,B:205,A:255).</summary>
    public static ColorF BlanchedAlmond { get; private set; }   
    /// <summary>Blue color (R:0,G:0,B:255,A:255).</summary>
    public static ColorF Blue { get; private set; } 
    /// <summary>BlueViolet color (R:138,G:43,B:226,A:255).</summary>
    public static ColorF BlueViolet { get; private set; }   
    /// <summary>Brown color (R:165,G:42,B:42,A:255).</summary>
    public static ColorF Brown { get; private set; }    
    /// <summary>BurlyWood color (R:222,G:184,B:135,A:255).</summary>
    public static ColorF BurlyWood { get; private set; }    
    /// <summary>CadetBlue color (R:95,G:158,B:160,A:255).</summary>
    public static ColorF CadetBlue { get; private set; }    
    /// <summary>Chartreuse color (R:127,G:255,B:0,A:255).</summary>
    public static ColorF Chartreuse { get; private set; }   
    /// <summary>Chocolate color (R:210,G:105,B:30,A:255).</summary>
    public static ColorF Chocolate { get; private set; }    
    /// <summary>Coral color (R:255,G:127,B:80,A:255).</summary>
    public static ColorF Coral { get; private set; }    
    /// <summary>CornflowerBlue color (R:100,G:149,B:237,A:255).</summary>
    public static ColorF CornflowerBlue { get; private set; }   
    /// <summary>Cornsilk color (R:255,G:248,B:220,A:255).</summary>
    public static ColorF Cornsilk { get; private set; } 
    /// <summary>Crimson color (R:220,G:20,B:60,A:255).</summary>
    public static ColorF Crimson { get; private set; }  
    /// <summary>Cyan color (R:0,G:255,B:255,A:255).</summary>
    public static ColorF Cyan { get; private set; } 
    /// <summary>DarkBlue color (R:0,G:0,B:139,A:255).</summary>
    public static ColorF DarkBlue { get; private set; } 
    /// <summary>DarkCyan color (R:0,G:139,B:139,A:255).</summary>
    public static ColorF DarkCyan { get; private set; } 
    /// <summary>DarkGoldenrod color (R:184,G:134,B:11,A:255).</summary>
    public static ColorF DarkGoldenrod { get; private set; }    
    /// <summary>DarkGray color (R:169,G:169,B:169,A:255).</summary>
    public static ColorF DarkGray { get; private set; } 
    /// <summary>DarkGreen color (R:0,G:100,B:0,A:255).</summary>
    public static ColorF DarkGreen { get; private set; }    
    /// <summary>DarkKhaki color (R:189,G:183,B:107,A:255).</summary>
    public static ColorF DarkKhaki { get; private set; }    
    /// <summary>DarkMagenta color (R:139,G:0,B:139,A:255).</summary>
    public static ColorF DarkMagenta { get; private set; }  
    /// <summary>DarkOliveGreen color (R:85,G:107,B:47,A:255).</summary>
    public static ColorF DarkOliveGreen { get; private set; }   
    /// <summary>DarkOrange color (R:255,G:140,B:0,A:255).</summary>
    public static ColorF DarkOrange { get; private set; }   
    /// <summary>DarkOrchid color (R:153,G:50,B:204,A:255).</summary>
    public static ColorF DarkOrchid { get; private set; }   
    /// <summary>DarkRed color (R:139,G:0,B:0,A:255).</summary>
    public static ColorF DarkRed { get; private set; }  
    /// <summary>DarkSalmon color (R:233,G:150,B:122,A:255).</summary>
    public static ColorF DarkSalmon { get; private set; }   
    /// <summary>DarkSeaGreen color (R:143,G:188,B:139,A:255).</summary>
    public static ColorF DarkSeaGreen { get; private set; } 
    /// <summary>DarkSlateBlue color (R:72,G:61,B:139,A:255).</summary>
    public static ColorF DarkSlateBlue { get; private set; }    
    /// <summary>DarkSlateGray color (R:47,G:79,B:79,A:255).</summary>
    public static ColorF DarkSlateGray { get; private set; }    
    /// <summary>DarkTurquoise color (R:0,G:206,B:209,A:255).</summary>
    public static ColorF DarkTurquoise { get; private set; }    
    /// <summary>DarkViolet color (R:148,G:0,B:211,A:255).</summary>
    public static ColorF DarkViolet { get; private set; }   
    /// <summary>DeepPink color (R:255,G:20,B:147,A:255).</summary>
    public static ColorF DeepPink { get; private set; } 
    /// <summary>DeepSkyBlue color (R:0,G:191,B:255,A:255).</summary>
    public static ColorF DeepSkyBlue { get; private set; }  
    /// <summary>DimGray color (R:105,G:105,B:105,A:255).</summary>
    public static ColorF DimGray { get; private set; }  
    /// <summary>DodgerBlue color (R:30,G:144,B:255,A:255).</summary>
    public static ColorF DodgerBlue { get; private set; }   
    /// <summary>Firebrick color (R:178,G:34,B:34,A:255).</summary>
    public static ColorF Firebrick { get; private set; }    
    /// <summary>FloralWhite color (R:255,G:250,B:240,A:255).</summary>
    public static ColorF FloralWhite { get; private set; }  
    /// <summary>ForestGreen color (R:34,G:139,B:34,A:255).</summary>
    public static ColorF ForestGreen { get; private set; }  
    /// <summary>Fuchsia color (R:255,G:0,B:255,A:255).</summary>
    public static ColorF Fuchsia { get; private set; }  
    /// <summary>Gainsboro color (R:220,G:220,B:220,A:255).</summary>
    public static ColorF Gainsboro { get; private set; }    
    /// <summary>GhostWhite color (R:248,G:248,B:255,A:255).</summary>
    public static ColorF GhostWhite { get; private set; }   
    /// <summary>Gold color (R:255,G:215,B:0,A:255).</summary>
    public static ColorF Gold { get; private set; } 
    /// <summary>Goldenrod color (R:218,G:165,B:32,A:255).</summary>
    public static ColorF Goldenrod { get; private set; }    
    /// <summary>Gray color (R:128,G:128,B:128,A:255).</summary>
    public static ColorF Gray { get; private set; } 
    /// <summary>Green color (R:0,G:128,B:0,A:255).</summary>
    public static ColorF Green { get; private set; }    
    /// <summary>GreenYellow color (R:173,G:255,B:47,A:255).</summary>
    public static ColorF GreenYellow { get; private set; }  
    /// <summary>Honeydew color (R:240,G:255,B:240,A:255).</summary>
    public static ColorF Honeydew { get; private set; } 
    /// <summary>HotPink color (R:255,G:105,B:180,A:255).</summary>
    public static ColorF HotPink { get; private set; }  
    /// <summary>IndianRed color (R:205,G:92,B:92,A:255).</summary>
    public static ColorF IndianRed { get; private set; }    
    /// <summary>Indigo color (R:75,G:0,B:130,A:255).</summary>
    public static ColorF Indigo { get; private set; }   
    /// <summary>Ivory color (R:255,G:255,B:240,A:255).</summary>
    public static ColorF Ivory { get; private set; }    
    /// <summary>Khaki color (R:240,G:230,B:140,A:255).</summary>
    public static ColorF Khaki { get; private set; }    
    /// <summary>Lavender color (R:230,G:230,B:250,A:255).</summary>
    public static ColorF Lavender { get; private set; } 
    /// <summary>LavenderBlush color (R:255,G:240,B:245,A:255).</summary>
    public static ColorF LavenderBlush { get; private set; }    
    /// <summary>LawnGreen color (R:124,G:252,B:0,A:255).</summary>
    public static ColorF LawnGreen { get; private set; }    
    /// <summary>LemonChiffon color (R:255,G:250,B:205,A:255).</summary>
    public static ColorF LemonChiffon { get; private set; } 
    /// <summary>LightBlue color (R:173,G:216,B:230,A:255).</summary>
    public static ColorF LightBlue { get; private set; }    
    /// <summary>LightCoral color (R:240,G:128,B:128,A:255).</summary>
    public static ColorF LightCoral { get; private set; }   
    /// <summary>LightCyan color (R:224,G:255,B:255,A:255).</summary>
    public static ColorF LightCyan { get; private set; }    
    /// <summary>LightGoldenrodYellow color (R:250,G:250,B:210,A:255).</summary>
    public static ColorF LightGoldenrodYellow { get; private set; } 
    /// <summary>LightGray color (R:211,G:211,B:211,A:255).</summary>
    public static ColorF LightGray { get; private set; }    
    /// <summary>LightGreen color (R:144,G:238,B:144,A:255).</summary>
    public static ColorF LightGreen { get; private set; }   
    /// <summary>LightPink color (R:255,G:182,B:193,A:255).</summary>
    public static ColorF LightPink { get; private set; }    
    /// <summary>LightSalmon color (R:255,G:160,B:122,A:255).</summary>
    public static ColorF LightSalmon { get; private set; }  
    /// <summary>LightSeaGreen color (R:32,G:178,B:170,A:255).</summary>
    public static ColorF LightSeaGreen { get; private set; }    
    /// <summary>LightSkyBlue color (R:135,G:206,B:250,A:255).</summary>
    public static ColorF LightSkyBlue { get; private set; } 
    /// <summary>LightSlateGray color (R:119,G:136,B:153,A:255).</summary>
    public static ColorF LightSlateGray { get; private set; }   
    /// <summary>LightSteelBlue color (R:176,G:196,B:222,A:255).</summary>
    public static ColorF LightSteelBlue { get; private set; }   
    /// <summary>LightYellow color (R:255,G:255,B:224,A:255).</summary>
    public static ColorF LightYellow { get; private set; }  
    /// <summary>Lime color (R:0,G:255,B:0,A:255).</summary>
    public static ColorF Lime { get; private set; } 
    /// <summary>LimeGreen color (R:50,G:205,B:50,A:255).</summary>
    public static ColorF LimeGreen { get; private set; }    
    /// <summary>Linen color (R:250,G:240,B:230,A:255).</summary>
    public static ColorF Linen { get; private set; }    
    /// <summary>Magenta color (R:255,G:0,B:255,A:255).</summary>
    public static ColorF Magenta { get; private set; }  
    /// <summary>Maroon color (R:128,G:0,B:0,A:255).</summary>
    public static ColorF Maroon { get; private set; }   
    /// <summary>MediumAquamarine color (R:102,G:205,B:170,A:255).</summary>
    public static ColorF MediumAquamarine { get; private set; } 
    /// <summary>MediumBlue color (R:0,G:0,B:205,A:255).</summary>
    public static ColorF MediumBlue { get; private set; }   
    /// <summary>MediumOrchid color (R:186,G:85,B:211,A:255).</summary>
    public static ColorF MediumOrchid { get; private set; } 
    /// <summary>MediumPurple color (R:147,G:112,B:219,A:255).</summary>
    public static ColorF MediumPurple { get; private set; } 
    /// <summary>MediumSeaGreen color (R:60,G:179,B:113,A:255).</summary>
    public static ColorF MediumSeaGreen { get; private set; }   
    /// <summary>MediumSlateBlue color (R:123,G:104,B:238,A:255).</summary>
    public static ColorF MediumSlateBlue { get; private set; }  
    /// <summary>MediumSpringGreen color (R:0,G:250,B:154,A:255).</summary>
    public static ColorF MediumSpringGreen { get; private set; }    
    /// <summary>MediumTurquoise color (R:72,G:209,B:204,A:255).</summary>
    public static ColorF MediumTurquoise { get; private set; }  
    /// <summary>MediumVioletRed color (R:199,G:21,B:133,A:255).</summary>
    public static ColorF MediumVioletRed { get; private set; }  
    /// <summary>MidnightBlue color (R:25,G:25,B:112,A:255).</summary>
    public static ColorF MidnightBlue { get; private set; } 
    /// <summary>MintCream color (R:245,G:255,B:250,A:255).</summary>
    public static ColorF MintCream { get; private set; }    
    /// <summary>MistyRose color (R:255,G:228,B:225,A:255).</summary>
    public static ColorF MistyRose { get; private set; }    
    /// <summary>Moccasin color (R:255,G:228,B:181,A:255).</summary>
    public static ColorF Moccasin { get; private set; } 
    /// <summary>MonoGame orange theme color (R:231,G:60,B:0,A:255).</summary>
    public static ColorF MonoGameOrange { get; private set; }   
    /// <summary>NavajoWhite color (R:255,G:222,B:173,A:255).</summary>
    public static ColorF NavajoWhite { get; private set; }  
    /// <summary>Navy color (R:0,G:0,B:128,A:255).</summary>
    public static ColorF Navy { get; private set; } 
    /// <summary>OldLace color (R:253,G:245,B:230,A:255).</summary>
    public static ColorF OldLace { get; private set; }  
    /// <summary>Olive color (R:128,G:128,B:0,A:255).</summary>
    public static ColorF Olive { get; private set; }    
    /// <summary>OliveDrab color (R:107,G:142,B:35,A:255).</summary>
    public static ColorF OliveDrab { get; private set; }    
    /// <summary>Orange color (R:255,G:165,B:0,A:255).</summary>
    public static ColorF Orange { get; private set; }   
    /// <summary>OrangeRed color (R:255,G:69,B:0,A:255).</summary>
    public static ColorF OrangeRed { get; private set; }    
    /// <summary>Orchid color (R:218,G:112,B:214,A:255).</summary>
    public static ColorF Orchid { get; private set; }   
    /// <summary>PaleGoldenrod color (R:238,G:232,B:170,A:255).</summary>
    public static ColorF PaleGoldenrod { get; private set; }    
    /// <summary>PaleGreen color (R:152,G:251,B:152,A:255).</summary>
    public static ColorF PaleGreen { get; private set; }    
    /// <summary>PaleTurquoise color (R:175,G:238,B:238,A:255).</summary>
    public static ColorF PaleTurquoise { get; private set; }    
    /// <summary>PaleVioletRed color (R:219,G:112,B:147,A:255).</summary>
    public static ColorF PaleVioletRed { get; private set; }    
    /// <summary>PapayaWhip color (R:255,G:239,B:213,A:255).</summary>
    public static ColorF PapayaWhip { get; private set; }   
    /// <summary>PeachPuff color (R:255,G:218,B:185,A:255).</summary>
    public static ColorF PeachPuff { get; private set; }    
    /// <summary>Peru color (R:205,G:133,B:63,A:255).</summary>
    public static ColorF Peru { get; private set; } 
    /// <summary>Pink color (R:255,G:192,B:203,A:255).</summary>
    public static ColorF Pink { get; private set; } 
    /// <summary>Plum color (R:221,G:160,B:221,A:255).</summary>
    public static ColorF Plum { get; private set; } 
    /// <summary>PowderBlue color (R:176,G:224,B:230,A:255).</summary>
    public static ColorF PowderBlue { get; private set; }   
    /// <summary>Purple color (R:128,G:0,B:128,A:255).</summary>
    public static ColorF Purple { get; private set; }   
    /// <summary>Red color (R:255,G:0,B:0,A:255).</summary>
    public static ColorF Red { get; private set; }  
    /// <summary>RosyBrown color (R:188,G:143,B:143,A:255).</summary>
    public static ColorF RosyBrown { get; private set; }    
    /// <summary>RoyalBlue color (R:65,G:105,B:225,A:255).</summary>
    public static ColorF RoyalBlue { get; private set; }    
    /// <summary>SaddleBrown color (R:139,G:69,B:19,A:255).</summary>
    public static ColorF SaddleBrown { get; private set; }  
    /// <summary>Salmon color (R:250,G:128,B:114,A:255).</summary>
    public static ColorF Salmon { get; private set; }   
    /// <summary>SandyBrown color (R:244,G:164,B:96,A:255).</summary>
    public static ColorF SandyBrown { get; private set; }   
    /// <summary>SeaGreen color (R:46,G:139,B:87,A:255).</summary>
    public static ColorF SeaGreen { get; private set; } 
    /// <summary>SeaShell color (R:255,G:245,B:238,A:255).</summary>
    public static ColorF SeaShell { get; private set; } 
    /// <summary>Sienna color (R:160,G:82,B:45,A:255).</summary>
    public static ColorF Sienna { get; private set; }   
    /// <summary>Silver color (R:192,G:192,B:192,A:255).</summary>
    public static ColorF Silver { get; private set; }   
    /// <summary>SkyBlue color (R:135,G:206,B:235,A:255).</summary>
    public static ColorF SkyBlue { get; private set; }  
    /// <summary>SlateBlue color (R:106,G:90,B:205,A:255).</summary>
    public static ColorF SlateBlue { get; private set; }    
    /// <summary>SlateGray color (R:112,G:128,B:144,A:255).</summary>
    public static ColorF SlateGray { get; private set; }    
    /// <summary>Snow color (R:255,G:250,B:250,A:255).</summary>
    public static ColorF Snow { get; private set; } 
    /// <summary>SpringGreen color (R:0,G:255,B:127,A:255).</summary>
    public static ColorF SpringGreen { get; private set; }  
    /// <summary>SteelBlue color (R:70,G:130,B:180,A:255).</summary>
    public static ColorF SteelBlue { get; private set; }    
    /// <summary>Tan color (R:210,G:180,B:140,A:255).</summary>
    public static ColorF Tan { get; private set; }  
    /// <summary>Teal color (R:0,G:128,B:128,A:255).</summary>
    public static ColorF Teal { get; private set; } 
    /// <summary>Thistle color (R:216,G:191,B:216,A:255).</summary>
    public static ColorF Thistle { get; private set; }  
    /// <summary>Tomato color (R:255,G:99,B:71,A:255).</summary>
    public static ColorF Tomato { get; private set; }   
    /// <summary>Turquoise color (R:64,G:224,B:208,A:255).</summary>
    public static ColorF Turquoise { get; private set; }    
    /// <summary>Violet color (R:238,G:130,B:238,A:255).</summary>
    public static ColorF Violet { get; private set; }   
    /// <summary>Wheat color (R:245,G:222,B:179,A:255).</summary>
    public static ColorF Wheat { get; private set; }    
    /// <summary>White color (R:255,G:255,B:255,A:255).</summary>
    public static ColorF White { get; private set; }    
    /// <summary>WhiteSmoke color (R:245,G:245,B:245,A:255).</summary>
    public static ColorF WhiteSmoke { get; private set; }   
    /// <summary>Yellow color (R:255,G:255,B:0,A:255).</summary>
    public static ColorF Yellow { get; private set; }   
    /// <summary>YellowGreen color (R:154,G:205,B:50,A:255).</summary>
    public static ColorF YellowGreen { get; private set; }
    
    #endregion
}