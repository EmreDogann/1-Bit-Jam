Shader "Hidden/Dither/ScreenSpaceDither"
{
    Properties
    {
        _BlitTexture("Source Blitter Texture", 2D) = "white" {}
		_BlueNoiseTex("Blue Noise Texture", 2D) = "white" {}
		_WhiteNoiseTex("White Noise Texture", 2D) = "white" {}
		_IGNoiseTex("Interleaved Gradient Noise Texture", 2D) = "white" {}
		_BayerNoiseTex("Bayer Noise Texture", 2D) = "white" {}
		_ColorRampTex("Color Ramp", 2D) = "white" {}
    	
    	_BG("Background Color", Color) = (0,0,0,1)
        _FG("Foreground Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always
        
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType"="Plane"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            #pragma shader_feature _ ENABLE_HQ_FILTERING
            #pragma shader_feature _ ENABLE_WORLD_SPACE_DITHER
            #pragma shader_feature _ USE_RAMP_TEX

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
				float4 _BlitTexture_TexelSize;
				float4 _BlueNoiseTex_TexelSize;
				float4 _WhiteNoiseTex_TexelSize;
				float4 _IGNoiseTex_TexelSize;
				float4 _BayerNoiseTex_TexelSize;

	            float4 _BL;
			    float4 _TL;
			    float4 _TR;
			    float4 _BR;

	            float4 _BG;
	            float4 _MG;
	            float4 _FG;

	            float _Tiling;
	            float _Threshold;
				float _MGThreshold;
            CBUFFER_END

			SAMPLER(sampler_BlitTexture);

            TEXTURE2D(_GBuffer1);		// R: Reflectivity (metallic) / specular        G: Dither Type        B: specular (Unused)        A: occlusion
			SAMPLER(sampler_GBuffer1);

            TEXTURE2D(_BlueNoiseTex);
			SAMPLER(sampler_BlueNoiseTex);

            TEXTURE2D(_WhiteNoiseTex);
			SAMPLER(sampler_WhiteNoiseTex);

            TEXTURE2D(_IGNoiseTex);
			SAMPLER(sampler_IGNoiseTex);

            TEXTURE2D(_BayerNoiseTex);
			SAMPLER(sampler_BayerNoiseTex);

            TEXTURE2D(_ColorRampTex);
			SAMPLER(sampler_ColorRampTex);

            #if ENABLE_HQ_FILTERING
				// Divide by texelsize other tiling will need to be orders or magnitude larger in order to work
				// -0.05f as some energy is lost due to the smoothing done by the bicubic filtering.
				#define SAMPLE_DITHER_TEX(textureName, uv) \
					textureBicubic(textureName, sampler##textureName, uv / textureName##_TexelSize.xy * _Tiling, textureName##_TexelSize.xy) - 0.1f;
            #else
				#define SAMPLE_DITHER_TEX(textureName, uv) \
					SAMPLE_TEXTURE2D_GRAD(textureName, sampler##textureName, uv * textureName##_TexelSize.xy * _Tiling, ddx(uv), ddy(uv));
            #endif

            float4 cubeProject(Texture2D tex, SamplerState texSampler, float2 texel, float3 dir)
			{
				float3x3 rotDirMatrix = {
					0.9473740, -0.1985178, 0.2511438,
					0.2511438, 0.9473740, -0.1985178,
					-0.1985178, 0.2511438, 0.9473740
				};

				dir = mul(rotDirMatrix, dir);
				float2 uvCoords;
				if ((abs(dir.x) > abs(dir.y)) && (abs(dir.x) > abs(dir.z)))
				{
					uvCoords = dir.yz; // X axis
				}
				else if ((abs(dir.z) > abs(dir.x)) && (abs(dir.z) > abs(dir.y)))
				{
					uvCoords = dir.xy; // Z axis
				}
				else
				{
					uvCoords = dir.xz; // Y axis
				}

				return SAMPLE_TEXTURE2D(tex, texSampler, texel * _Tiling * uvCoords);
			}

			float2 edge(float2 uv, float2 delta)
			{
				float3 up = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(0.0, 1.0) * delta);
				float3 down = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(0.0, -1.0) * delta);
				float3 left = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(1.0, 0.0) * delta);
				float3 right = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv + float2(-1.0, 0.0) * delta);
				float3 centre = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);

				return float2(min(up.b, min(min(down.b, left.b), min(right.b, centre.b))),
				              max(max(distance(centre.rg, up.rg), distance(centre.rg, down.rg)),
				                  max(distance(centre.rg, left.rg), distance(centre.rg, right.rg))));
			}

			// from http://www.java-gaming.org/index.php?topic=35123.0
			float4 cubic(float v)
			{
				float4 n = float4(1.0, 2.0, 3.0, 4.0) - v;
				float4 s = n * n * n;
				float x = s.x;
				float y = s.y - 4.0 * s.x;
				float z = s.z - 4.0 * s.y + 6.0 * s.x;
				float w = 6.0 - x - y - z;
				return float4(x, y, z, w) * (1.0 / 6.0);
			}

            // From: https://stackoverflow.com/a/42179924
            // Other Resources: https://vec3.ca/bicubic-filtering-in-fewer-taps/
            // https://http.download.nvidia.com/developer/SDK/Individual_Samples/DEMOS/OpenGL/src/fast_third_order/docs/Gems2_ch20_SDK.pdf
			float4 textureBicubic(Texture2D tex, SamplerState samplerTex, float2 uv, float2 texelSize)
			{
				float2 texCoords = uv * texelSize - 0.5;

				float2 fxy = frac(texCoords);
				texCoords -= fxy;

				float4 xcubic = cubic(fxy.x);
				float4 ycubic = cubic(fxy.y);

				float4 c = texCoords.xxyy + float2(-0.5, 1.5).xyxy;

				float4 s = float4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
				float4 offset = c + float4(xcubic.yw, ycubic.yw) / s;

				offset *= texelSize.xxyy;

				float4 sample0 = tex.Sample(samplerTex, offset.xz);
				float4 sample1 = tex.Sample(samplerTex, offset.yz);
				float4 sample2 = tex.Sample(samplerTex, offset.xw);
				float4 sample3 = tex.Sample(samplerTex, offset.yw);

				float sx = s.x / (s.x + s.y);
				float sy = s.z / (s.z + s.w);

				return lerp(
					lerp(sample3, sample2, sx),
					lerp(sample1, sample0, sx),
					sy
				);
			}

			// Z buffer to linear depth.
			// Does NOT correctly handle oblique view frustums.
			// Does NOT work with orthographic projection.
			// zBufferParam = { (f-n)/n, 1, (f-n)/n*f, 1/f }
			float LinearEyeDepthInverse(float depth, float4 zBufferParam)
			{
				return (zBufferParam.z * depth + zBufferParam.w);
			}

            //smooth version of step
            float aaStep(float compValue, float gradient){
              float change = fwidth(gradient);
              //base the range of the inverse lerp on the change over two pixels
              float lowerEdge = compValue - change;
              float upperEdge = compValue + change;
              //do the inverse interpolation
              float stepped = (gradient - lowerEdge) / (upperEdge - lowerEdge);
              stepped = saturate(stepped);
              //smoothstep version here would be `smoothstep(lowerEdge, upperEdge, gradient)`
              return stepped;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float3 sourceColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord).rgb;
            	// Multiplied by 10 because we mapped the types are integers scaled down to multiples of 0.1.
            	int ditherType = round(SAMPLE_TEXTURE2D(_GBuffer1, sampler_GBuffer1, input.texcoord).g * 10);

            	float4 ditherColor;
            	// #if ENABLE_WORLD_SPACE_DITHER
            		float2 UV = input.texcoord;
					// Sample the depth from the Camera depth texture.
					#if UNITY_REVERSED_Z
						real depth = SampleSceneDepth(UV);
					#else
						// Adjust Z to match NDC for OpenGL ([-1, 1])
						real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
					#endif
					// depth = LinearEyeDepthInverse(depth, _ZBufferParams);

					float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
					float3 localPos = TransformWorldToObject(worldPos);
					float3 normalWS = SampleSceneNormals(UV);
					float3 normalOS = TransformWorldToObject(normalWS);

					// References: https://www.patreon.com/posts/quick-game-art-16714688
					// https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/
					// https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a#1997
					// https://forum.unity.com/threads/box-triplanar-mapping-following-object-rotation.501252/

					// Current version based on Cheap as chips triplanar:
					// https://www.reddit.com/r/unrealengine/comments/70o0js/material_super_cheap_triplanar_mapping_solution/
					// https://imgur.com/aHAPPor
					float3 uvScaled = localPos * 0.1f;
					float3 blending = abs(normalOS);

					// Triplanar uvs
					float2 uvX = uvScaled.yz; // x facing plane
					float2 uvY = uvScaled.xz; // y facing plane
					float2 uvZ = uvScaled.xy; // z facing plane

					// Flip UVs to correct for mirroring
					half3 axisSign = sign(normalWS); // Get the sign (-1 or 1) of the surface normal.
					uvX.x *= axisSign.x;
					uvY.x *= axisSign.y;
					uvZ.x *= -axisSign.z;

					blending = pow(blending, 160);
					blending /= dot(blending, float3(1,1,1));

					float2 projectionUV = (uvZ * blending.z) + ((uvX * blending.x) + (uvY * blending.y));
					if (ditherType == 0 || ditherType == 1) // Blue Noise
					{
						ditherColor = SAMPLE_DITHER_TEX(_BlueNoiseTex, projectionUV);
					} else if (ditherType == 2) // White Noise
					{
						ditherColor = SAMPLE_DITHER_TEX(_WhiteNoiseTex, projectionUV);
					} else if (ditherType == 3) // Interleaved-Gradient Noise
					{
						ditherColor = SAMPLE_DITHER_TEX(_IGNoiseTex, projectionUV);
					} else if (ditherType == 4) // Bayer Noise
					{
						ditherColor = SAMPLE_DITHER_TEX(_BayerNoiseTex, projectionUV);
					}
				// #else
				// 	float3 dir = normalize(lerp(lerp(_BL, _TL, input.texcoord.y), lerp(_BR, _TR, input.texcoord.y), input.texcoord.x));
				// 	if (ditherType == 0 || ditherType == 1) // Blue Noise
				// 	{
				// 		ditherColor = cubeProject(_BlueNoiseTex, sampler_BlueNoiseTex, _BlueNoiseTex_TexelSize.xy, dir);
				// 	} else if (ditherType == 2) // White Noise
				// 	{
				// 		ditherColor = cubeProject(_WhiteNoiseTex, sampler_WhiteNoiseTex, _WhiteNoiseTex_TexelSize.xy, dir);
				// 	} else if (ditherType == 3) // Interleaved-Gradient Noise
				// 	{
				// 		ditherColor = cubeProject(_IGNoiseTex, sampler_IGNoiseTex, _IGNoiseTex_TexelSize.xy, dir);
				// 	} else if (ditherType == 4) // Bayer Noise
				// 	{
				// 		ditherColor = cubeProject(_BayerNoiseTex, sampler_BayerNoiseTex, _BayerNoiseTex_TexelSize.xy, dir);
				// 	}
				// #endif
            	float ditherLum = Luminance(ditherColor);
                float lum = Luminance(sourceColor);

                // float2 edgeData = edge(input.uv, _MainTex_TexelSize.xy * 1.0f);
                // lum = (edgeData.y < _Threshold) ? lum : ((edgeData.x < 0.1f) ? 1.0f : 0.0f);
                
                float ramp = step(ditherLum, lum);

            	#if USE_RAMP_TEX
					float3 output = SAMPLE_TEXTURE2D(_ColorRampTex, sampler_ColorRampTex, float2(ramp, 0.5f));
            	#else
            		// float3 output = lerp(_BG, _FG, round(ramp));
            		float3 output = lerp(_MG, _FG, step(_MGThreshold, abs(lum - ditherLum)));
            		output = lerp(_BG, output, ramp);
            		// float3 output = lerp(_BG, sourceColor, ramp); // No thresholding
            	#endif

				return float4(output, 1.0f);
            }
            ENDHLSL
        }
    }
}
