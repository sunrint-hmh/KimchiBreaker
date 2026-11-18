Shader "KimchiBreaker/PSXRubber"
{
    Properties
    {
        _BaseMap("Rubber atlas",2D)="white"{}
        _Palette("32 color palette (8 tones x 4 families)",2D)="white"{}
        _DitherStrength("Ordered dither",Range(0,1))=.9
        _TexelGrid("Texture grid",Float)=128
        _AtlasSplit("Cuff / hand UV split",Range(.4,.7))=.56
        _LightFloor("Minimum vertex light",Range(0,1))=.65
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
        Pass
        {
            Name "PSXRubber"
            Tags {"LightMode"="UniversalForwardOnly"}
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BaseMap); TEXTURE2D(_Palette);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _DitherStrength,_TexelGrid,_LightFloor,_AtlasSplit;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float vertexLight:TEXCOORD1;};
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
                float3 normal=TransformObjectToWorldNormal(input.normalOS);
                Light key=GetMainLight();
                float diffuse=saturate(dot(normal,key.direction));
                float ambient=dot(max(SampleSH(normal),0),float3(.2126,.7152,.0722));
                output.vertexLight=clamp(_LightFloor+diffuse*.48+ambient*.16,_LightFloor,1.15);
                return output;
            }
            float Dither(int2 p)
            {
                // PS1 GPU's 4x4 offset table, applied in texel space to avoid swimming.
                const float table[16]={-4,0,-3,1,2,-2,3,-1,-3,1,-4,0,3,-1,2,-2};
                return table[(p.y&3)*4+(p.x&3)];
            }
            half4 Frag(Varyings input):SV_Target
            {
                float grid=max(8,_TexelGrid);
                // Match the generated atlas' cuff boundary without changing mesh UVs.
                float2 atlasUV=input.uv;
                atlasUV.y=input.uv.y<.5?input.uv.y*(_AtlasSplit*2):_AtlasSplit+(input.uv.y-.5)*(2*(1-_AtlasSplit));
                int2 texel=(int2)floor(atlasUV*grid);
                float2 uv=(float2(texel)+.5)/grid;
                float3 sampled=SAMPLE_TEXTURE2D_LOD(_BaseMap,sampler_PointRepeat,uv,0).rgb;
                float3 srgb=LinearToSRGB(max(sampled,0));
                // Hue family is independent of brightness; each family has eight stepped tones.
                float3 hue=srgb/max(max(srgb.r,srgb.g),max(srgb.b,.0001));
                float3 references[4]={float3(1,.29,.42),float3(1,.32,.24),float3(1,.62,.35),float3(1,.65,.61)};
                int family=0;float best=1e6;
                [unroll]for(int i=0;i<4;i++)
                {
                    float distance=dot(hue-references[i],hue-references[i]);
                    if(distance<best){best=distance;family=i;}
                }
                float value=saturate(max(max(srgb.r,srgb.g),srgb.b)*input.vertexLight);
                int tone=(int)clamp(floor(value*7+.5+Dither(texel)*.11*_DitherStrength),0,7);
                float2 paletteUV=float2((tone+.5)/8,(family+.5)/4);
                return half4(SAMPLE_TEXTURE2D_LOD(_Palette,sampler_PointClamp,paletteUV,0).rgb,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
