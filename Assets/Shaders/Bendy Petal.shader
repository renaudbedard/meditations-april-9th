Shader "Custom/Bendy Petal" 
{
	Properties
	{
		_Color("Color", Color) = (1, 1, 1, 1)
		_Metallic("Metallic", Range(0, 1)) = 1
		_Smoothness("Smoothness", Range(0, 1)) = 1 
		_Emission("Emission", Color) = (1, 1, 1, 1)
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
		float4 _Emission;

		UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float, _AttachState)
		UNITY_INSTANCING_BUFFER_END(Props)

		struct Input 
		{
			float Dummy;
		};

		void surf(Input i, inout SurfaceOutputStandard o)
		{
			o.Albedo = _Color.rgb;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
			o.Emission = _Emission.rgb;
			o.Alpha = 1;
		}
		
		void vert(inout appdata_full v)
		{
			float attachState = UNITY_ACCESS_INSTANCED_PROP(Props, _AttachState);

			if (attachState > 0.5)
			{
				v.vertex.xyz = mul(unity_ObjectToWorld, v.vertex).xyz;

				float bendability = 1;
				v.vertex.xyz = Bend(bendability, v.vertex.xyz);

				v.vertex.xyz = mul(unity_WorldToObject, v.vertex).xyz;
			}
		}
		ENDCG
	}

	FallBack "Standard"
}