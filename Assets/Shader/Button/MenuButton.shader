Shader "UI/RadialGradientButton"
{
  Properties
  {
    // Базовые цвета (Normal)
    _ColorCenter   ("Center Color",        Color) = (0.843, 0.176, 0.212, 1)
    _ColorEdge     ("Edge Color",          Color) = (0.596, 0.176, 0.200, 1)

    // Hover — чуть светлее
    _HoverCenter   ("Hover Center",        Color) = (0.960, 0.260, 0.300, 1)
    _HoverEdge     ("Hover Edge",          Color) = (0.720, 0.220, 0.240, 1)

    // Pressed — темнее + сжатый градиент
    _PressCenter   ("Press Center",        Color) = (0.650, 0.130, 0.160, 1)
    _PressEdge     ("Press Edge",          Color) = (0.420, 0.100, 0.130, 1)

    // Disabled — серый
    _DisabledCenter("Disabled Center",     Color) = (0.420, 0.380, 0.380, 1)
    _DisabledEdge  ("Disabled Edge",       Color) = (0.280, 0.260, 0.260, 1)

    _Radius        ("Gradient Radius", Range(0.1, 2.0)) = 1.0

    // Состояние: 0=Normal 1=Hover 2=Pressed 3=Selected 4=Disabled
    _State         ("Button State",    Range(0, 4))    = 0

    // Плавность перехода (0=моментально, 1=медленно)
    _Blend         ("State Blend",     Range(0, 1))    = 0
  }

  SubShader
  {
    Tags { "Queue"="Transparent" "RenderType"="Transparent" }
    Blend SrcAlpha OneMinusSrcAlpha
    Cull Off ZWrite Off

    Pass
    {
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "UnityCG.cginc"

      fixed4 _ColorCenter,    _ColorEdge;
      fixed4 _HoverCenter,    _HoverEdge;
      fixed4 _PressCenter,    _PressEdge;
      fixed4 _DisabledCenter, _DisabledEdge;
      float  _Radius, _State, _Blend;

      struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
      struct v2f    { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

      v2f vert(appdata v) {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv  = v.uv;
        return o;
      }

      // Возвращает цвет градиента для конкретного состояния
      fixed4 getStateColor(float2 uv, int state) {
        float dist = clamp(length(uv - 0.5) * 2.0 / _Radius, 0.0, 1.0);

        if (state == 1) return lerp(_HoverCenter,    _HoverEdge,    dist);
        if (state == 2) return lerp(_PressCenter,    _PressEdge,    dist);
        if (state == 3) return lerp(_PressCenter,    _PressEdge,    dist * 0.85);
        if (state == 4) return lerp(_DisabledCenter, _DisabledEdge, dist);
        return               lerp(_ColorCenter,    _ColorEdge,    dist);
      }

      fixed4 frag(v2f i) : SV_Target {
        int stateA = (int)_State;
        int stateB = stateA + 1;

        fixed4 colA = getStateColor(i.uv, stateA);
        fixed4 colB = getStateColor(i.uv, stateB);

        // _Blend = 0 → чистое stateA, _Blend = 1 → чистое stateB
        return lerp(colA, colB, _Blend);
      }
      ENDCG
    }
  }
}