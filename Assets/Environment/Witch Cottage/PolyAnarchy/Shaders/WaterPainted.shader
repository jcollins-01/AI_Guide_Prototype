// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// Shader created with Shader Forge v1.38 
// Shader Forge (c) Neat Corporation / Joachim Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.38;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,cgin:,lico:1,lgpr:1,limd:3,spmd:1,trmd:0,grmd:1,uamb:True,mssp:True,bkdf:True,hqlp:False,rprd:True,enco:False,rmgx:True,imps:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:3,bdst:7,dpts:2,wrdp:False,dith:0,atcv:False,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:True,qofs:1,qpre:3,rntp:2,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.5,fgcg:0.5,fgcb:0.5,fgca:1,fgde:0.01,fgrn:0,fgrf:300,stcl:False,atwp:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False,fsmp:False;n:type:ShaderForge.SFN_Final,id:2865,x:35248,y:33264,varname:node_2865,prsc:2|diff-9515-OUT,spec-2888-OUT,gloss-4882-OUT,alpha-499-OUT,voffset-3857-OUT,tess-8938-OUT;n:type:ShaderForge.SFN_Vector1,id:2888,x:34861,y:33289,varname:node_2888,prsc:2,v1:0;n:type:ShaderForge.SFN_Vector1,id:4882,x:34861,y:33346,varname:node_4882,prsc:2,v1:0;n:type:ShaderForge.SFN_Color,id:4430,x:32737,y:32875,ptovrint:False,ptlb:ColorB,ptin:_ColorB,varname:_WaterColorB_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0.07450257,c2:0.399544,c3:0.7794118,c4:1;n:type:ShaderForge.SFN_Lerp,id:1638,x:33159,y:32905,varname:node_1638,prsc:2|A-4915-RGB,B-8582-OUT,T-6398-OUT;n:type:ShaderForge.SFN_ValueProperty,id:6749,x:32303,y:33385,ptovrint:False,ptlb:Depth,ptin:_Depth,varname:_Depth_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:4.37;n:type:ShaderForge.SFN_DepthBlend,id:963,x:32473,y:33411,varname:node_963,prsc:2|DIST-6749-OUT;n:type:ShaderForge.SFN_ViewVector,id:4903,x:32184,y:33607,varname:node_4903,prsc:2;n:type:ShaderForge.SFN_NormalVector,id:7904,x:32184,y:33771,prsc:2,pt:False;n:type:ShaderForge.SFN_Dot,id:2799,x:32428,y:33676,varname:node_2799,prsc:2,dt:4|A-4903-OUT,B-7904-OUT;n:type:ShaderForge.SFN_Divide,id:3070,x:32685,y:33564,varname:node_3070,prsc:2|A-963-OUT,B-2799-OUT;n:type:ShaderForge.SFN_Clamp01,id:6398,x:32905,y:33564,varname:node_6398,prsc:2|IN-3070-OUT;n:type:ShaderForge.SFN_Color,id:4915,x:32737,y:32683,ptovrint:False,ptlb:ColorA,ptin:_ColorA,varname:node_4915,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0,c2:0.9926471,c3:0.949827,c4:1;n:type:ShaderForge.SFN_RemapRangeAdvanced,id:6931,x:32949,y:33355,varname:node_6931,prsc:2|IN-6398-OUT,IMIN-7548-OUT,IMAX-3906-OUT,OMIN-2191-OUT,OMAX-5133-OUT;n:type:ShaderForge.SFN_Slider,id:3906,x:32492,y:33288,ptovrint:False,ptlb:FoamMax,ptin:_FoamMax,varname:_FoamInMax_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.61,max:1;n:type:ShaderForge.SFN_Vector1,id:2191,x:32649,y:33379,varname:node_2191,prsc:2,v1:0;n:type:ShaderForge.SFN_Vector1,id:5133,x:32649,y:33445,varname:node_5133,prsc:2,v1:1;n:type:ShaderForge.SFN_RemapRangeAdvanced,id:7002,x:33779,y:33743,varname:node_7002,prsc:2|IN-6398-OUT,IMIN-4522-OUT,IMAX-2315-OUT,OMIN-7133-OUT,OMAX-373-OUT;n:type:ShaderForge.SFN_Slider,id:4522,x:33170,y:33737,ptovrint:False,ptlb:DepthMin,ptin:_DepthMin,varname:_DepthInMin_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.278,max:1;n:type:ShaderForge.SFN_Slider,id:2315,x:33170,y:33840,ptovrint:False,ptlb:DepthMax,ptin:_DepthMax,varname:_DepthInMax_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.61,max:1;n:type:ShaderForge.SFN_Vector1,id:7133,x:33559,y:33852,varname:node_7133,prsc:2,v1:0;n:type:ShaderForge.SFN_Vector1,id:373,x:33573,y:33921,varname:node_373,prsc:2,v1:1;n:type:ShaderForge.SFN_Clamp01,id:1399,x:33119,y:33355,varname:node_1399,prsc:2|IN-6931-OUT;n:type:ShaderForge.SFN_ComponentMask,id:9266,x:34162,y:33387,varname:node_9266,prsc:2,cc1:0,cc2:-1,cc3:-1,cc4:-1|IN-7911-OUT;n:type:ShaderForge.SFN_Vector1,id:5453,x:34059,y:33643,varname:node_5453,prsc:2,v1:0.5;n:type:ShaderForge.SFN_Multiply,id:8882,x:34342,y:33449,varname:node_8882,prsc:2|A-9266-OUT,B-5453-OUT;n:type:ShaderForge.SFN_Clamp01,id:499,x:34737,y:33459,varname:node_499,prsc:2|IN-8922-OUT;n:type:ShaderForge.SFN_Multiply,id:6099,x:33674,y:33228,varname:node_6099,prsc:2|A-6202-OUT,B-8790-OUT;n:type:ShaderForge.SFN_ValueProperty,id:8790,x:33494,y:33343,ptovrint:False,ptlb:FoamPower,ptin:_FoamPower,varname:_PowerFoam_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:8.3;n:type:ShaderForge.SFN_Clamp01,id:7911,x:33955,y:33351,varname:node_7911,prsc:2|IN-4103-OUT;n:type:ShaderForge.SFN_Add,id:7426,x:33952,y:33089,varname:node_7426,prsc:2|A-1638-OUT,B-7911-OUT;n:type:ShaderForge.SFN_Clamp01,id:9515,x:34161,y:33113,varname:node_9515,prsc:2|IN-7426-OUT;n:type:ShaderForge.SFN_Add,id:8922,x:34554,y:33556,varname:node_8922,prsc:2|A-8882-OUT,B-7002-OUT;n:type:ShaderForge.SFN_OneMinus,id:2846,x:33318,y:33388,varname:node_2846,prsc:2|IN-1399-OUT;n:type:ShaderForge.SFN_Slider,id:7548,x:32473,y:33188,ptovrint:False,ptlb:FoamMin,ptin:_FoamMin,varname:node_7548,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.51,max:1;n:type:ShaderForge.SFN_Multiply,id:2881,x:33309,y:33083,varname:node_2881,prsc:2|A-1638-OUT,B-7086-RGB;n:type:ShaderForge.SFN_Tex2d,id:7086,x:32908,y:33092,ptovrint:False,ptlb:FoamTexture,ptin:_FoamTexture,varname:node_7086,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-2085-UVOUT;n:type:ShaderForge.SFN_Multiply,id:6202,x:33494,y:33174,varname:node_6202,prsc:2|A-2881-OUT,B-2846-OUT;n:type:ShaderForge.SFN_Power,id:8135,x:33529,y:33480,varname:node_8135,prsc:2|VAL-2846-OUT,EXP-9809-OUT;n:type:ShaderForge.SFN_Vector1,id:9809,x:33248,y:33545,varname:node_9809,prsc:2,v1:5;n:type:ShaderForge.SFN_Add,id:4103,x:33774,y:33460,varname:node_4103,prsc:2|A-6099-OUT,B-8135-OUT;n:type:ShaderForge.SFN_Panner,id:2085,x:32422,y:32949,varname:node_2085,prsc:2,spu:0.002,spv:0.002|UVIN-7327-UVOUT;n:type:ShaderForge.SFN_TexCoord,id:7327,x:32174,y:32949,varname:node_7327,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_Time,id:9399,x:32656,y:33999,varname:node_9399,prsc:2;n:type:ShaderForge.SFN_FragmentPosition,id:7338,x:32833,y:34284,varname:node_7338,prsc:2;n:type:ShaderForge.SFN_Add,id:1393,x:33106,y:34302,varname:node_1393,prsc:2|A-7338-X,B-62-OUT;n:type:ShaderForge.SFN_Sin,id:8648,x:33457,y:34302,varname:node_8648,prsc:2|IN-9052-OUT;n:type:ShaderForge.SFN_Multiply,id:791,x:33640,y:34302,varname:node_791,prsc:2|A-8648-OUT,B-6188-OUT;n:type:ShaderForge.SFN_ValueProperty,id:6188,x:33457,y:34477,ptovrint:False,ptlb:WaveSpread,ptin:_WaveSpread,varname:node_7191,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0;n:type:ShaderForge.SFN_Add,id:8945,x:33824,y:34302,varname:node_8945,prsc:2|A-791-OUT,B-4785-OUT;n:type:ShaderForge.SFN_ValueProperty,id:4785,x:33640,y:34477,ptovrint:False,ptlb:WaveWidth,ptin:_WaveWidth,varname:node_493,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:10;n:type:ShaderForge.SFN_Multiply,id:9052,x:33289,y:34302,varname:node_9052,prsc:2|A-1393-OUT,B-5138-OUT;n:type:ShaderForge.SFN_ValueProperty,id:5138,x:33106,y:34466,ptovrint:False,ptlb:WaveCount,ptin:_WaveCount,varname:_WaveSpread_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:10;n:type:ShaderForge.SFN_RemapRange,id:4836,x:34005,y:34302,varname:node_4836,prsc:2,frmn:-1,frmx:1,tomn:0,tomx:1|IN-8945-OUT;n:type:ShaderForge.SFN_Append,id:9880,x:34607,y:34253,varname:node_9880,prsc:2|A-6925-OUT,B-6949-OUT;n:type:ShaderForge.SFN_Append,id:3857,x:34811,y:34253,varname:node_3857,prsc:2|A-9880-OUT,B-5549-OUT;n:type:ShaderForge.SFN_Vector1,id:6925,x:34408,y:34195,varname:node_6925,prsc:2,v1:0;n:type:ShaderForge.SFN_Vector1,id:5549,x:34607,y:34394,varname:node_5549,prsc:2,v1:0;n:type:ShaderForge.SFN_Multiply,id:6949,x:34408,y:34314,varname:node_6949,prsc:2|A-2496-OUT,B-73-OUT;n:type:ShaderForge.SFN_Slider,id:73,x:33848,y:34504,ptovrint:False,ptlb:WaveHeight,ptin:_WaveHeight,varname:node_5911,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.073,max:1;n:type:ShaderForge.SFN_Multiply,id:2496,x:34227,y:34236,varname:node_2496,prsc:2|A-3446-R,B-4836-OUT;n:type:ShaderForge.SFN_Slider,id:8938,x:34658,y:33711,ptovrint:False,ptlb:Tesselation,ptin:_Tesselation,varname:node_8938,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:10,max:10;n:type:ShaderForge.SFN_TexCoord,id:6808,x:33368,y:33982,varname:node_6808,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_Panner,id:6186,x:33711,y:34085,varname:node_6186,prsc:2,spu:0.5,spv:0.5|UVIN-6808-UVOUT,DIST-62-OUT;n:type:ShaderForge.SFN_Tex2d,id:3446,x:33913,y:34085,ptovrint:False,ptlb:Wave_Perlin_Noise,ptin:_Wave_Perlin_Noise,varname:_WaveDistortion_copy,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-6186-UVOUT;n:type:ShaderForge.SFN_ValueProperty,id:3147,x:32670,y:34191,ptovrint:False,ptlb:WaveSpeed,ptin:_WaveSpeed,varname:node_3147,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:10;n:type:ShaderForge.SFN_Multiply,id:62,x:32884,y:34108,varname:node_62,prsc:2|A-9399-TSL,B-3147-OUT;n:type:ShaderForge.SFN_Tex2d,id:6352,x:32890,y:32499,ptovrint:False,ptlb:Base Texture,ptin:_BaseTexture,varname:node_6352,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-4304-OUT;n:type:ShaderForge.SFN_Panner,id:8528,x:31829,y:32722,varname:node_8528,prsc:2,spu:0.01,spv:0.01|UVIN-3797-UVOUT;n:type:ShaderForge.SFN_TexCoord,id:3797,x:31829,y:32508,varname:node_3797,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_Tex2d,id:1063,x:32908,y:32255,ptovrint:False,ptlb:Base Texture Mask,ptin:_BaseTextureMask,varname:node_1063,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-6637-UVOUT;n:type:ShaderForge.SFN_Lerp,id:8582,x:33183,y:32623,varname:node_8582,prsc:2|A-6352-RGB,B-4430-RGB,T-1063-RGB;n:type:ShaderForge.SFN_Panner,id:6637,x:32592,y:32263,varname:node_6637,prsc:2,spu:0.005,spv:0.001|UVIN-2227-UVOUT;n:type:ShaderForge.SFN_TexCoord,id:2227,x:32344,y:32263,varname:node_2227,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_Lerp,id:4304,x:32448,y:32700,varname:node_4304,prsc:2|A-8118-UVOUT,B-3158-RGB,T-6269-OUT;n:type:ShaderForge.SFN_TexCoord,id:8118,x:32064,y:32508,varname:node_8118,prsc:2,uv:0,uaff:False;n:type:ShaderForge.SFN_Tex2d,id:3158,x:32126,y:32701,ptovrint:False,ptlb:Distort Mask,ptin:_DistortMask,varname:node_3158,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,ntxv:0,isnm:False|UVIN-8528-UVOUT;n:type:ShaderForge.SFN_ValueProperty,id:2146,x:31793,y:32938,ptovrint:False,ptlb:Distort Magnitude,ptin:_DistortMagnitude,varname:node_2146,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0.003;n:type:ShaderForge.SFN_Clamp01,id:6269,x:31970,y:32894,varname:node_6269,prsc:2|IN-2146-OUT;proporder:4915-4430-6352-1063-3158-2146-8790-7548-3906-4522-6749-2315-7086-3446-3147-6188-4785-5138-73-8938;pass:END;sub:END;*/

Shader "WaterPainted" {
    Properties {
        _ColorA ("ColorA", Color) = (0,0.9926471,0.949827,1)
        _ColorB ("ColorB", Color) = (0.07450257,0.399544,0.7794118,1)
        _BaseTexture ("Base Texture", 2D) = "white" {}
        _BaseTextureMask ("Base Texture Mask", 2D) = "white" {}
        _DistortMask ("Distort Mask", 2D) = "white" {}
        _DistortMagnitude ("Distort Magnitude", Float ) = 0.003
        _FoamPower ("FoamPower", Float ) = 8.3
        _FoamMin ("FoamMin", Range(0, 1)) = 0.51
        _FoamMax ("FoamMax", Range(0, 1)) = 0.61
        _DepthMin ("DepthMin", Range(0, 1)) = 0.278
        _Depth ("Depth", Float ) = 4.37
        _DepthMax ("DepthMax", Range(0, 1)) = 0.61
        _FoamTexture ("FoamTexture", 2D) = "white" {}
        _Wave_Perlin_Noise ("Wave_Perlin_Noise", 2D) = "white" {}
        _WaveSpeed ("WaveSpeed", Float ) = 10
        _WaveSpread ("WaveSpread", Float ) = 0
        _WaveWidth ("WaveWidth", Float ) = 10
        _WaveCount ("WaveCount", Float ) = 10
        _WaveHeight ("WaveHeight", Range(0, 1)) = 0.073
        _Tesselation ("Tesselation", Range(0, 10)) = 10
        [HideInInspector]_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent+1"
            "RenderType"="Transparent"
        }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDBASE
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdbase
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _CameraDepthTexture;
            uniform float4 _ColorB;
            uniform float _Depth;
            uniform float4 _ColorA;
            uniform float _FoamMax;
            uniform float _DepthMin;
            uniform float _DepthMax;
            uniform float _FoamPower;
            uniform float _FoamMin;
            uniform sampler2D _FoamTexture; uniform float4 _FoamTexture_ST;
            uniform float _WaveSpread;
            uniform float _WaveWidth;
            uniform float _WaveCount;
            uniform float _WaveHeight;
            uniform float _Tesselation;
            uniform sampler2D _Wave_Perlin_Noise; uniform float4 _Wave_Perlin_Noise_ST;
            uniform float _WaveSpeed;
            uniform sampler2D _BaseTexture; uniform float4 _BaseTexture_ST;
            uniform sampler2D _BaseTextureMask; uniform float4 _BaseTextureMask_ST;
            uniform sampler2D _DistortMask; uniform float4 _DistortMask_ST;
            uniform float _DistortMagnitude;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                float4 projPos : TEXCOORD7;
                UNITY_FOG_COORDS(8)
                #if defined(LIGHTMAP_ON) || defined(UNITY_SHOULD_SAMPLE_SH)
                    float4 ambientOrLightmapUV : TEXCOORD9;
                #endif
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                #ifdef LIGHTMAP_ON
                    o.ambientOrLightmapUV.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                    o.ambientOrLightmapUV.zw = 0;
                #elif UNITY_SHOULD_SAMPLE_SH
                #endif
                #ifdef DYNAMICLIGHTMAP_ON
                    o.ambientOrLightmapUV.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                float4 node_9399 = _Time;
                float node_62 = (node_9399.r*_WaveSpeed);
                float2 node_6186 = (o.uv0+node_62*float2(0.5,0.5));
                float4 _Wave_Perlin_Noise_var = tex2Dlod(_Wave_Perlin_Noise,float4(TRANSFORM_TEX(node_6186, _Wave_Perlin_Noise),0.0,0));
                v.vertex.xyz += float3(float2(0.0,((_Wave_Perlin_Noise_var.r*(((sin(((mul(unity_ObjectToWorld, v.vertex).r+node_62)*_WaveCount))*_WaveSpread)+_WaveWidth)*0.5+0.5))*_WaveHeight)),0.0);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                o.projPos = ComputeScreenPos (o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                float Tessellation(TessVertex v){
                    return _Tesselation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
                float sceneZ = max(0,LinearEyeDepth (UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)))) - _ProjectionParams.g);
                float partZ = max(0,i.projPos.z - _ProjectionParams.g);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = 1;
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float gloss = 1.0 - 0.0; // Convert roughness to gloss
                float perceptualRoughness = 0.0;
                float roughness = perceptualRoughness * perceptualRoughness;
                float specPow = exp2( gloss * 10.0 + 1.0 );
/////// GI Data:
                UnityLight light;
                #ifdef LIGHTMAP_OFF
                    light.color = lightColor;
                    light.dir = lightDirection;
                    light.ndotl = LambertTerm (normalDirection, light.dir);
                #else
                    light.color = half3(0.f, 0.f, 0.f);
                    light.ndotl = 0.0f;
                    light.dir = half3(0.f, 0.f, 0.f);
                #endif
                UnityGIInput d;
                d.light = light;
                d.worldPos = i.posWorld.xyz;
                d.worldViewDir = viewDirection;
                d.atten = attenuation;
                #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
                    d.ambient = 0;
                    d.lightmapUV = i.ambientOrLightmapUV;
                #else
                    d.ambient = i.ambientOrLightmapUV;
                #endif
                #if UNITY_SPECCUBE_BLENDING || UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMin[0] = unity_SpecCube0_BoxMin;
                    d.boxMin[1] = unity_SpecCube1_BoxMin;
                #endif
                #if UNITY_SPECCUBE_BOX_PROJECTION
                    d.boxMax[0] = unity_SpecCube0_BoxMax;
                    d.boxMax[1] = unity_SpecCube1_BoxMax;
                    d.probePosition[0] = unity_SpecCube0_ProbePosition;
                    d.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                d.probeHDR[0] = unity_SpecCube0_HDR;
                d.probeHDR[1] = unity_SpecCube1_HDR;
                Unity_GlossyEnvironmentData ugls_en_data;
                ugls_en_data.roughness = 1.0 - gloss;
                ugls_en_data.reflUVW = viewReflectDirection;
                UnityGI gi = UnityGlobalIllumination(d, 1, normalDirection, ugls_en_data );
                lightDirection = gi.light.dir;
                lightColor = gi.light.color;
////// Specular:
                float NdotL = saturate(dot( normalDirection, lightDirection ));
                float LdotH = saturate(dot(lightDirection, halfDirection));
                float3 specularColor = 0.0;
                float specularMonochrome;
                float4 node_4276 = _Time;
                float2 node_8528 = (i.uv0+node_4276.g*float2(0.01,0.01));
                float4 _DistortMask_var = tex2D(_DistortMask,TRANSFORM_TEX(node_8528, _DistortMask));
                float3 node_4304 = lerp(float3(i.uv0,0.0),_DistortMask_var.rgb,saturate(_DistortMagnitude));
                float4 _BaseTexture_var = tex2D(_BaseTexture,TRANSFORM_TEX(node_4304, _BaseTexture));
                float2 node_6637 = (i.uv0+node_4276.g*float2(0.005,0.001));
                float4 _BaseTextureMask_var = tex2D(_BaseTextureMask,TRANSFORM_TEX(node_6637, _BaseTextureMask));
                float node_6398 = saturate((saturate((sceneZ-partZ)/_Depth)/0.5*dot(viewDirection,i.normalDir)+0.5));
                float3 node_1638 = lerp(_ColorA.rgb,lerp(_BaseTexture_var.rgb,_ColorB.rgb,_BaseTextureMask_var.rgb),node_6398);
                float2 node_2085 = (i.uv0+node_4276.g*float2(0.002,0.002));
                float4 _FoamTexture_var = tex2D(_FoamTexture,TRANSFORM_TEX(node_2085, _FoamTexture));
                float node_2191 = 0.0;
                float node_2846 = (1.0 - saturate((node_2191 + ( (node_6398 - _FoamMin) * (1.0 - node_2191) ) / (_FoamMax - _FoamMin))));
                float3 node_7911 = saturate(((((node_1638*_FoamTexture_var.rgb)*node_2846)*_FoamPower)+pow(node_2846,5.0)));
                float3 diffuseColor = saturate((node_1638+node_7911)); // Need this for specular when using metallic
                diffuseColor = DiffuseAndSpecularFromMetallic( diffuseColor, specularColor, specularColor, specularMonochrome );
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = abs(dot( normalDirection, viewDirection ));
                float NdotH = saturate(dot( normalDirection, halfDirection ));
                float VdotH = saturate(dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, roughness );
                float normTerm = GGXTerm(NdotH, roughness);
                float specularPBL = (visTerm*normTerm) * UNITY_PI;
                #ifdef UNITY_COLORSPACE_GAMMA
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                #endif
                specularPBL = max(0, specularPBL * NdotL);
                #if defined(_SPECULARHIGHLIGHTS_OFF)
                    specularPBL = 0.0;
                #endif
                half surfaceReduction;
                #ifdef UNITY_COLORSPACE_GAMMA
                    surfaceReduction = 1.0-0.28*roughness*perceptualRoughness;
                #else
                    surfaceReduction = 1.0/(roughness*roughness + 1.0);
                #endif
                specularPBL *= any(specularColor) ? 1.0 : 0.0;
                float3 directSpecular = attenColor*specularPBL*FresnelTerm(specularColor, LdotH);
                half grazingTerm = saturate( gloss + specularMonochrome );
                float3 indirectSpecular = (gi.indirect.specular);
                indirectSpecular *= FresnelLerp (specularColor, grazingTerm, NdotV);
                indirectSpecular *= surfaceReduction;
                float3 specular = (directSpecular + indirectSpecular);
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                indirectDiffuse += gi.indirect.diffuse;
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse + specular;
                float node_7133 = 0.0;
                fixed4 finalRGBA = fixed4(finalColor,saturate(((node_7911.r*0.5)+(node_7133 + ( (node_6398 - _DepthMin) * (1.0 - node_7133) ) / (_DepthMax - _DepthMin)))));
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            ZWrite Off
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_FORWARDADD
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdadd
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _CameraDepthTexture;
            uniform float4 _ColorB;
            uniform float _Depth;
            uniform float4 _ColorA;
            uniform float _FoamMax;
            uniform float _DepthMin;
            uniform float _DepthMax;
            uniform float _FoamPower;
            uniform float _FoamMin;
            uniform sampler2D _FoamTexture; uniform float4 _FoamTexture_ST;
            uniform float _WaveSpread;
            uniform float _WaveWidth;
            uniform float _WaveCount;
            uniform float _WaveHeight;
            uniform float _Tesselation;
            uniform sampler2D _Wave_Perlin_Noise; uniform float4 _Wave_Perlin_Noise_ST;
            uniform float _WaveSpeed;
            uniform sampler2D _BaseTexture; uniform float4 _BaseTexture_ST;
            uniform sampler2D _BaseTextureMask; uniform float4 _BaseTextureMask_ST;
            uniform sampler2D _DistortMask; uniform float4 _DistortMask_ST;
            uniform float _DistortMagnitude;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float3 tangentDir : TEXCOORD5;
                float3 bitangentDir : TEXCOORD6;
                float4 projPos : TEXCOORD7;
                LIGHTING_COORDS(8,9)
                UNITY_FOG_COORDS(10)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                float4 node_9399 = _Time;
                float node_62 = (node_9399.r*_WaveSpeed);
                float2 node_6186 = (o.uv0+node_62*float2(0.5,0.5));
                float4 _Wave_Perlin_Noise_var = tex2Dlod(_Wave_Perlin_Noise,float4(TRANSFORM_TEX(node_6186, _Wave_Perlin_Noise),0.0,0));
                v.vertex.xyz += float3(float2(0.0,((_Wave_Perlin_Noise_var.r*(((sin(((mul(unity_ObjectToWorld, v.vertex).r+node_62)*_WaveCount))*_WaveSpread)+_WaveWidth)*0.5+0.5))*_WaveHeight)),0.0);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos( v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                o.projPos = ComputeScreenPos (o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                float Tessellation(TessVertex v){
                    return _Tesselation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;
                float sceneZ = max(0,LinearEyeDepth (UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)))) - _ProjectionParams.g);
                float partZ = max(0,i.projPos.z - _ProjectionParams.g);
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float gloss = 1.0 - 0.0; // Convert roughness to gloss
                float perceptualRoughness = 0.0;
                float roughness = perceptualRoughness * perceptualRoughness;
                float specPow = exp2( gloss * 10.0 + 1.0 );
////// Specular:
                float NdotL = saturate(dot( normalDirection, lightDirection ));
                float LdotH = saturate(dot(lightDirection, halfDirection));
                float3 specularColor = 0.0;
                float specularMonochrome;
                float4 node_2907 = _Time;
                float2 node_8528 = (i.uv0+node_2907.g*float2(0.01,0.01));
                float4 _DistortMask_var = tex2D(_DistortMask,TRANSFORM_TEX(node_8528, _DistortMask));
                float3 node_4304 = lerp(float3(i.uv0,0.0),_DistortMask_var.rgb,saturate(_DistortMagnitude));
                float4 _BaseTexture_var = tex2D(_BaseTexture,TRANSFORM_TEX(node_4304, _BaseTexture));
                float2 node_6637 = (i.uv0+node_2907.g*float2(0.005,0.001));
                float4 _BaseTextureMask_var = tex2D(_BaseTextureMask,TRANSFORM_TEX(node_6637, _BaseTextureMask));
                float node_6398 = saturate((saturate((sceneZ-partZ)/_Depth)/0.5*dot(viewDirection,i.normalDir)+0.5));
                float3 node_1638 = lerp(_ColorA.rgb,lerp(_BaseTexture_var.rgb,_ColorB.rgb,_BaseTextureMask_var.rgb),node_6398);
                float2 node_2085 = (i.uv0+node_2907.g*float2(0.002,0.002));
                float4 _FoamTexture_var = tex2D(_FoamTexture,TRANSFORM_TEX(node_2085, _FoamTexture));
                float node_2191 = 0.0;
                float node_2846 = (1.0 - saturate((node_2191 + ( (node_6398 - _FoamMin) * (1.0 - node_2191) ) / (_FoamMax - _FoamMin))));
                float3 node_7911 = saturate(((((node_1638*_FoamTexture_var.rgb)*node_2846)*_FoamPower)+pow(node_2846,5.0)));
                float3 diffuseColor = saturate((node_1638+node_7911)); // Need this for specular when using metallic
                diffuseColor = DiffuseAndSpecularFromMetallic( diffuseColor, specularColor, specularColor, specularMonochrome );
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = abs(dot( normalDirection, viewDirection ));
                float NdotH = saturate(dot( normalDirection, halfDirection ));
                float VdotH = saturate(dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, roughness );
                float normTerm = GGXTerm(NdotH, roughness);
                float specularPBL = (visTerm*normTerm) * UNITY_PI;
                #ifdef UNITY_COLORSPACE_GAMMA
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                #endif
                specularPBL = max(0, specularPBL * NdotL);
                #if defined(_SPECULARHIGHLIGHTS_OFF)
                    specularPBL = 0.0;
                #endif
                specularPBL *= any(specularColor) ? 1.0 : 0.0;
                float3 directSpecular = attenColor*specularPBL*FresnelTerm(specularColor, LdotH);
                float3 specular = directSpecular;
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                float3 diffuse = directDiffuse * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse + specular;
                float node_7133 = 0.0;
                fixed4 finalRGBA = fixed4(finalColor * saturate(((node_7911.r*0.5)+(node_7133 + ( (node_6398 - _DepthMin) * (1.0 - node_7133) ) / (_DepthMax - _DepthMin)))),0);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }
            Offset 1, 1
            Cull Back
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_SHADOWCASTER
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles n3ds wiiu 
            #pragma target 5.0
            uniform float _WaveSpread;
            uniform float _WaveWidth;
            uniform float _WaveCount;
            uniform float _WaveHeight;
            uniform float _Tesselation;
            uniform sampler2D _Wave_Perlin_Noise; uniform float4 _Wave_Perlin_Noise_ST;
            uniform float _WaveSpeed;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                V2F_SHADOW_CASTER;
                float2 uv0 : TEXCOORD1;
                float2 uv1 : TEXCOORD2;
                float2 uv2 : TEXCOORD3;
                float4 posWorld : TEXCOORD4;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                float4 node_9399 = _Time;
                float node_62 = (node_9399.r*_WaveSpeed);
                float2 node_6186 = (o.uv0+node_62*float2(0.5,0.5));
                float4 _Wave_Perlin_Noise_var = tex2Dlod(_Wave_Perlin_Noise,float4(TRANSFORM_TEX(node_6186, _Wave_Perlin_Noise),0.0,0));
                v.vertex.xyz += float3(float2(0.0,((_Wave_Perlin_Noise_var.r*(((sin(((mul(unity_ObjectToWorld, v.vertex).r+node_62)*_WaveCount))*_WaveSpread)+_WaveWidth)*0.5+0.5))*_WaveHeight)),0.0);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos( v.vertex );
                TRANSFER_SHADOW_CASTER(o)
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                float Tessellation(TessVertex v){
                    return _Tesselation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : COLOR {
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
        Pass {
            Name "Meta"
            Tags {
                "LightMode"="Meta"
            }
            Cull Off
            
            CGPROGRAM
            #pragma hull hull
            #pragma domain domain
            #pragma vertex tessvert
            #pragma fragment frag
            #define UNITY_PASS_META 1
            #define SHOULD_SAMPLE_SH ( defined (LIGHTMAP_OFF) && defined(DYNAMICLIGHTMAP_OFF) )
            #define _GLOSSYENV 1
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Tessellation.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityMetaPass.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile DIRLIGHTMAP_OFF DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE
            #pragma multi_compile DYNAMICLIGHTMAP_OFF DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma only_renderers d3d9 d3d11 glcore gles n3ds wiiu 
            #pragma target 5.0
            uniform sampler2D _CameraDepthTexture;
            uniform float4 _ColorB;
            uniform float _Depth;
            uniform float4 _ColorA;
            uniform float _FoamMax;
            uniform float _FoamPower;
            uniform float _FoamMin;
            uniform sampler2D _FoamTexture; uniform float4 _FoamTexture_ST;
            uniform float _WaveSpread;
            uniform float _WaveWidth;
            uniform float _WaveCount;
            uniform float _WaveHeight;
            uniform float _Tesselation;
            uniform sampler2D _Wave_Perlin_Noise; uniform float4 _Wave_Perlin_Noise_ST;
            uniform float _WaveSpeed;
            uniform sampler2D _BaseTexture; uniform float4 _BaseTexture_ST;
            uniform sampler2D _BaseTextureMask; uniform float4 _BaseTextureMask_ST;
            uniform sampler2D _DistortMask; uniform float4 _DistortMask_ST;
            uniform float _DistortMagnitude;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 posWorld : TEXCOORD3;
                float3 normalDir : TEXCOORD4;
                float4 projPos : TEXCOORD5;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.uv1 = v.texcoord1;
                o.uv2 = v.texcoord2;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                float4 node_9399 = _Time;
                float node_62 = (node_9399.r*_WaveSpeed);
                float2 node_6186 = (o.uv0+node_62*float2(0.5,0.5));
                float4 _Wave_Perlin_Noise_var = tex2Dlod(_Wave_Perlin_Noise,float4(TRANSFORM_TEX(node_6186, _Wave_Perlin_Noise),0.0,0));
                v.vertex.xyz += float3(float2(0.0,((_Wave_Perlin_Noise_var.r*(((sin(((mul(unity_ObjectToWorld, v.vertex).r+node_62)*_WaveCount))*_WaveSpread)+_WaveWidth)*0.5+0.5))*_WaveHeight)),0.0);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityMetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST );
                o.projPos = ComputeScreenPos (o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                return o;
            }
            #ifdef UNITY_CAN_COMPILE_TESSELLATION
                struct TessVertex {
                    float4 vertex : INTERNALTESSPOS;
                    float3 normal : NORMAL;
                    float4 tangent : TANGENT;
                    float2 texcoord0 : TEXCOORD0;
                    float2 texcoord1 : TEXCOORD1;
                    float2 texcoord2 : TEXCOORD2;
                };
                struct OutputPatchConstant {
                    float edge[3]         : SV_TessFactor;
                    float inside          : SV_InsideTessFactor;
                    float3 vTangent[4]    : TANGENT;
                    float2 vUV[4]         : TEXCOORD;
                    float3 vTanUCorner[4] : TANUCORNER;
                    float3 vTanVCorner[4] : TANVCORNER;
                    float4 vCWts          : TANWEIGHTS;
                };
                TessVertex tessvert (VertexInput v) {
                    TessVertex o;
                    o.vertex = v.vertex;
                    o.normal = v.normal;
                    o.tangent = v.tangent;
                    o.texcoord0 = v.texcoord0;
                    o.texcoord1 = v.texcoord1;
                    o.texcoord2 = v.texcoord2;
                    return o;
                }
                float Tessellation(TessVertex v){
                    return _Tesselation;
                }
                float4 Tessellation(TessVertex v, TessVertex v1, TessVertex v2){
                    float tv = Tessellation(v);
                    float tv1 = Tessellation(v1);
                    float tv2 = Tessellation(v2);
                    return float4( tv1+tv2, tv2+tv, tv+tv1, tv+tv1+tv2 ) / float4(2,2,2,3);
                }
                OutputPatchConstant hullconst (InputPatch<TessVertex,3> v) {
                    OutputPatchConstant o = (OutputPatchConstant)0;
                    float4 ts = Tessellation( v[0], v[1], v[2] );
                    o.edge[0] = ts.x;
                    o.edge[1] = ts.y;
                    o.edge[2] = ts.z;
                    o.inside = ts.w;
                    return o;
                }
                [domain("tri")]
                [partitioning("fractional_odd")]
                [outputtopology("triangle_cw")]
                [patchconstantfunc("hullconst")]
                [outputcontrolpoints(3)]
                TessVertex hull (InputPatch<TessVertex,3> v, uint id : SV_OutputControlPointID) {
                    return v[id];
                }
                [domain("tri")]
                VertexOutput domain (OutputPatchConstant tessFactors, const OutputPatch<TessVertex,3> vi, float3 bary : SV_DomainLocation) {
                    VertexInput v = (VertexInput)0;
                    v.vertex = vi[0].vertex*bary.x + vi[1].vertex*bary.y + vi[2].vertex*bary.z;
                    v.normal = vi[0].normal*bary.x + vi[1].normal*bary.y + vi[2].normal*bary.z;
                    v.tangent = vi[0].tangent*bary.x + vi[1].tangent*bary.y + vi[2].tangent*bary.z;
                    v.texcoord0 = vi[0].texcoord0*bary.x + vi[1].texcoord0*bary.y + vi[2].texcoord0*bary.z;
                    v.texcoord1 = vi[0].texcoord1*bary.x + vi[1].texcoord1*bary.y + vi[2].texcoord1*bary.z;
                    VertexOutput o = vert(v);
                    return o;
                }
            #endif
            float4 frag(VertexOutput i) : SV_Target {
                i.normalDir = normalize(i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float3 normalDirection = i.normalDir;
                float sceneZ = max(0,LinearEyeDepth (UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)))) - _ProjectionParams.g);
                float partZ = max(0,i.projPos.z - _ProjectionParams.g);
                UnityMetaInput o;
                UNITY_INITIALIZE_OUTPUT( UnityMetaInput, o );
                
                o.Emission = 0;
                
                float4 node_7868 = _Time;
                float2 node_8528 = (i.uv0+node_7868.g*float2(0.01,0.01));
                float4 _DistortMask_var = tex2D(_DistortMask,TRANSFORM_TEX(node_8528, _DistortMask));
                float3 node_4304 = lerp(float3(i.uv0,0.0),_DistortMask_var.rgb,saturate(_DistortMagnitude));
                float4 _BaseTexture_var = tex2D(_BaseTexture,TRANSFORM_TEX(node_4304, _BaseTexture));
                float2 node_6637 = (i.uv0+node_7868.g*float2(0.005,0.001));
                float4 _BaseTextureMask_var = tex2D(_BaseTextureMask,TRANSFORM_TEX(node_6637, _BaseTextureMask));
                float node_6398 = saturate((saturate((sceneZ-partZ)/_Depth)/0.5*dot(viewDirection,i.normalDir)+0.5));
                float3 node_1638 = lerp(_ColorA.rgb,lerp(_BaseTexture_var.rgb,_ColorB.rgb,_BaseTextureMask_var.rgb),node_6398);
                float2 node_2085 = (i.uv0+node_7868.g*float2(0.002,0.002));
                float4 _FoamTexture_var = tex2D(_FoamTexture,TRANSFORM_TEX(node_2085, _FoamTexture));
                float node_2191 = 0.0;
                float node_2846 = (1.0 - saturate((node_2191 + ( (node_6398 - _FoamMin) * (1.0 - node_2191) ) / (_FoamMax - _FoamMin))));
                float3 node_7911 = saturate(((((node_1638*_FoamTexture_var.rgb)*node_2846)*_FoamPower)+pow(node_2846,5.0)));
                float3 diffColor = saturate((node_1638+node_7911));
                float specularMonochrome;
                float3 specColor;
                diffColor = DiffuseAndSpecularFromMetallic( diffColor, 0.0, specColor, specularMonochrome );
                float roughness = 0.0;
                o.Albedo = diffColor + specColor * roughness * roughness * 0.5;
                
                return UnityMetaFragment( o );
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
    CustomEditor "ShaderForgeMaterialInspector"
}
