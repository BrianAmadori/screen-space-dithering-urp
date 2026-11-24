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
				#pragma multi_compile __ DITHERING_GRAIN_FOLLOWS_CAMERA

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

				float4 _GrainScreenSize;            // xy: render target size in pixels
				float2 _GrainTurnShift;             // how far camera turns moved the image, in pixels
				float2 _GrainSlideShiftAtOneMeter;  // how far camera slides moved a point 1 m away, in pixels

				struct VertexInput 
				{
					float4 position : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct Input 
				{
					float4 position : SV_POSITION;
					float2 uv : TEXCOORD0;
				};

				#include "Dithering Base.cginc"
				#include "GrainUtils.cginc"

				UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

				Input vert(VertexInput i) 
				{
					Input o;
					o.position = UnityObjectToClipPos(i.position);
					o.uv = i.uv;
					return o;
				}

				// The center of the grain and pattern pixel that this screen pixel shows.
				float2 GetGrainPixelCenter(float2 uv)
				{
					float2 pixel = uv * _GrainScreenSize.xy;

					#if DITHERING_GRAIN_FOLLOWS_CAMERA
					// A turn moves every pixel alike. A slide moves each pixel by its own depth, so near things move more.
					float depth = max(LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv)), _ProjectionParams.y);
					pixel -= _GrainTurnShift + _GrainSlideShiftAtOneMeter / depth;
					#endif

					// Whole pixels keep every grain cell exactly grainSize pixels wide. The +0.5 samples texel centers,
					// because a sample on a texel edge lets the GPU pick either neighbor.
					return floor(pixel) + 0.5;
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
					float2 grainPixel = GetGrainPixelCenter(i.uv);
					float4 grain = tex2D(_GrainTex, frac(grainPixel / _GrainScreenSize.xy * _Grain_Params2.xy + _Grain_Params2.zw));

					#ifndef UNITY_COLORSPACE_GAMMA
					c = pow(c, 0.454545);				
					#endif
					// Noisiness response curve based on scene luminance
			        float lum = 1.0 - sqrt(AcesLuminance(c));
			        lum = lerp(1.0, lum, _Grain_Params1.x);
					grain *= lum * _Grain_Params1.y;

			        c += c * grain;

					float4 ditherPos = float4(grainPixel / _PatternSize, 0, 1);
					return fixed4(GetDitherColor(c.rgb, _PatternTex, _PaletteTex, _PaletteHeight, ditherPos, _PaletteColorCount, _PatternScale), c.a);
				}
			
			ENDCG
		}
	}

	Fallback "Unlit/Texture"
}