Shader "Custom/Bendy Tree" 
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_Metallic("Metallic", Range(0, 1)) = 1
		_Smoothness("Smoothness", Range(0, 1)) = 1 
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM

#pragma surface surf Standard vertex:vert addshadow
#pragma target 3.0

		#include "Common.cginc"

		float4 _Color;
		float _Metallic;
		float _Smoothness;

		struct Input 
		{
			float Dummy;
		};

		void surf(Input i, inout SurfaceOutputStandard o)
		{
			o.Albedo = _Color.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
			o.Alpha = 1;
		}

		void vert(inout appdata_full v)
		{
			float bendability = saturate(v.texcoord.x);
			v.vertex.xyz = Bend(bendability, v.vertex.xyz);
		}
		ENDCG
	}

	FallBack "Standard"
}