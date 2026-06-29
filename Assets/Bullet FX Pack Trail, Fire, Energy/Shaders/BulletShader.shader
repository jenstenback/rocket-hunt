Shader "Custom/BulletShader"
{
    Properties
    {
        // Standaard ingesteld op een koper/messing kleur
        _Color ("Bullet Color (Brass/Copper)", Color) = (0.85, 0.65, 0.25, 1)
        _MainTex ("Texture (Optional)", 2D) = "white" {}
        
        // Hoge smoothness en metallic zorgen voor die echte kogel-glans
        _Glossiness ("Smoothness", Range(0,1)) = 0.8
        _Metallic ("Metallic", Range(0,1)) = 0.9
        
        // Optioneel: laat de kogel een beetje gloeien zodat je hem in de lucht kunt zien vliegen
        _EmissionColor ("Tracer Glow (Emission)", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        // Massief object (niet meer doorzichtig)
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        // fullforwardshadows zorgt ervoor dat de kogel echte schaduwen kan vangen/werpen
        #pragma surface surf Standard fullforwardshadows
        #include "UnityCG.cginc"

        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _EmissionColor;

        UNITY_INSTANCING_BUFFER_START(Props)
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Basiskleur van de kogel
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            
            // Metaal reflecties voor de echte kogel look
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            
            // Lichtgevende gloed (handig om kogels goed te zien vliegen in een game)
            o.Emission = _EmissionColor.rgb;
            
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}