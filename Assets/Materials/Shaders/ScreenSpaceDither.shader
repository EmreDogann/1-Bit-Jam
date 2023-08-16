﻿Shader "Hidden/Dither/ScreenSpaceDither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		_NoiseTex("Noise Texture", 2D) = "white" {}
		_ColorRampTex("Color Ramp", 2D) = "white" {}
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
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma shader_feature _ ENABLE_WORLD_SPACE_DITHER

            // #include "DecodeDepthNormals.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            	float3 positionWS : TEXCOORD1;
            	float3 positionOAS : TEXCOORD2;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_TexelSize;
				float4 _NoiseTex_TexelSize;
            
	            float4 _BL;
			    float4 _TL;
			    float4 _TR;
			    float4 _BR;

	            float _Tiling;
	            float _Threshold;
            CBUFFER_END

            TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
			SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_ColorRampTex);
			SAMPLER(sampler_ColorRampTex);

            float cubeProject(Texture2D tex, SamplerState texSampler, float2 texel, float3 dir)
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

				return SAMPLE_TEXTURE2D(tex, texSampler, texel * _Tiling * uvCoords).r;
			}

			float2 edge(float2 uv, float2 delta)
			{
				float3 up = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, 1.0) * delta);
				float3 down = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0.0, -1.0) * delta);
				float3 left = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(1.0, 0.0) * delta);
				float3 right = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-1.0, 0.0) * delta);
				float3 centre = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

				return float2(min(up.b, min(min(down.b, left.b), min(right.b, centre.b))),
				              max(max(distance(centre.rg, up.rg), distance(centre.rg, down.rg)),
				                  max(distance(centre.rg, left.rg), distance(centre.rg, right.rg))));
			}

            Varyings Vertex(Attributes input)
            {
                Varyings output;
            	VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
            	output.positionWS = vertexInput.positionWS;
				// OAS - Object Aligned Space
                // Apply only the scale to the object space vertex in order to compensate for rotation.
                output.positionOAS = input.positionOS.xyz;
            	
                output.uv = input.uv;
                return output;
            }
            
            float4 Fragment(Varyings input) : SV_Target
            {
                float3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).xyz;

            	#if ENABLE_WORLD_SPACE_DITHER
            		float2 UV = input.positionHCS.xy / _ScaledScreenParams.xy;
	                // Sample the depth from the Camera depth texture.
	                #if UNITY_REVERSED_Z
						real depth = SampleSceneDepth(UV);
	                #else
						// Adjust Z to match NDC for OpenGL ([-1, 1])
						real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
	                #endif
            		// depth = Linear01Depth(depth, _ZBufferParams);
	                
	                float3 worldPos = ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
            		float3 normalWS = SampleSceneNormals(input.uv);

	                // References: https://www.patreon.com/posts/quick-game-art-16714688
	                // https://catlikecoding.com/unity/tutorials/advanced-rendering/triplanar-mapping/
	                // https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a#1997
	                // https://forum.unity.com/threads/box-triplanar-mapping-following-object-rotation.501252/
	                // #if defined(ENABLE_ALIGNED_TRIPLANAR)
	                //     float3 uvScaled = input.positionOAS * _TriplanarScale;
	                //     float3 blending = abs(input.normalOS);
	                //     half3 axisSign = sign(input.normalOS); // Get the sign (-1 or 1) of the surface normal.
	                // #else
	                    float3 uvScaled = worldPos * 0.1f;
	                    float3 blending = abs(normalWS);
	                    half3 axisSign = sign(normalWS); // Get the sign (-1 or 1) of the surface normal.
	                // #endif
	                
	                // Triplanar uvs
	                float2 uvX = uvScaled.yz; // x facing plane
	                float2 uvY = uvScaled.xz; // y facing plane
	                float2 uvZ = uvScaled.xy; // z facing plane
	                
	                // Flip UVs to correct for mirroring
	                uvX.x *= axisSign.x;
	                uvY.x *= axisSign.y;
	                uvZ.x *= -axisSign.z;
	                
	                blending = saturate(blending - 0);
	                blending = pow(blending, 5);
	                blending /= dot(blending, float3(1,1,1));
	                
	                float4 color = blending.z * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvZ * _NoiseTex_TexelSize.xy * _Tiling);
	                color += blending.x * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvX * _NoiseTex_TexelSize.xy * _Tiling);
	                color += blending.y * SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvY * _NoiseTex_TexelSize.xy * _Tiling);
            		float ditherLum = color.r;
            	#else
	                float3 dir = normalize(lerp(lerp(_BL, _TL, input.uv.y), lerp(_BR, _TR, input.uv.y), input.uv.x));
	                float ditherLum = cubeProject(_NoiseTex, sampler_NoiseTex, _NoiseTex_TexelSize.xy, dir);
            	#endif
                float lum = col.b;
                
                float2 edgeData = edge(input.uv, _MainTex_TexelSize.xy * 1.0f);
                lum = (edgeData.y < _Threshold) ? lum : ((edgeData.x < 0.1f) ? 1.0f : 0.0f);
                
                float ramp = (lum <= clamp(ditherLum, 0.1f, 0.9f)) ? 0.1f : 0.9f;
                float3 output = SAMPLE_TEXTURE2D(_ColorRampTex, sampler_ColorRampTex, float2(ramp, 0.5f));

            	// Normals computed from screen-space derivatives.
            	// return float4(5 * cross(ddy(worldPos), ddx(worldPos)), 1);

				return float4(output, 1.0f);
            }
            ENDHLSL
        }
    }
}
