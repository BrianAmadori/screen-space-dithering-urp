// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/Dithering/Dithering Image Effect" 
{
	Properties 
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_PaletteColorCount ("Mixed Color Count", float) = 4
		_PaletteHeight ("Palette Height", float) = 128
		_PaletteTex ("Palette", 2D) = "black" {}
		_PatternSize ("Palette Size", float) = 8
		_PatternTex ("Palette Texture", 2D) = "black" {}
		_PatternScale("Pattern Scale", float) = 1
	}

	SubShader 
	{
		Tags 
		{ 
			"IgnoreProjector"="True" 
			"RenderType"="Opaque" 
		}
		
		LOD 200

		Lighting Off
		ZTest Always 
		Cull Off 
		ZWrite Off 
		Fog { Mode Off }

		Pass 
		{
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag

				sampler2D _MainTex;
				sampler2D _PaletteTex;
				sampler2D _PatternTex;

				float _PaletteColorCount;
				float _PaletteHeight;
				float _PatternSize;
				float _PatternScale;

				half2 _Grain_Params1; // x: lum_contrib, y: intensity
				half4 _Grain_Params2; // x: xscale, h: yscale, z: xoffset, w: yoffset
				sampler2D _GrainTex;

				struct VertexInput 
				{
					float4 position : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct Input 
				{
					float4 position : SV_POSITION;
					float2 uv : TEXCOORD0;
					float4 ditherPos : TEXCOORD1;
				};

				#include "Dithering Base.cginc"
				#include "GrainUtils.cginc"

				Input vert(VertexInput i) 
				{
					Input o;
					o.position = UnityObjectToClipPos(i.position);
					o.uv = i.uv;
					o.ditherPos = GetDitherPos(i.position, _PatternSize);
					return o;
				}

				//
				// Luminance (Rec.709 primaries according to ACES specs)
				//
				half AcesLuminance(half3 c)
				{
				    return dot(c, half3(0.2126, 0.7152, 0.0722));
				}

				fixed4 frag(Input i) : COLOR 
				{
				
					float4 c = tex2D(_MainTex, i.uv);
					float4 grain = tex2D(_GrainTex, frac(i.uv * _Grain_Params2.xy + _Grain_Params2.zw));

					#ifndef UNITY_COLORSPACE_GAMMA
					c = pow(c, 0.454545);				
					#endif
					// Noisiness response curve based on scene luminance
			        float lum = 1.0 - sqrt(AcesLuminance(c));
			        lum = lerp(1.0, lum, _Grain_Params1.x);
					grain *= lum * _Grain_Params1.y;

			        c += c * grain;

					return fixed4(GetDitherColor(c.rgb, _PatternTex, _PaletteTex, _PaletteHeight, i.ditherPos, _PaletteColorCount, _PatternScale), c.a);
				}
			
			ENDCG
		}
	}

	Fallback "Unlit/Texture"
}