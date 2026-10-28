Shader "Futile/FastBlizzardTintShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha 
		Fog { Color(0,0,0,0) }
		Lighting Off
		Cull Off 
		BindChannels 
		{
			Bind "Vertex", vertex
			Bind "texcoord", texcoord 
			Bind "Color", color 
		}
		Pass
		{
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex vert
			#pragma fragment frag		
			#include "UnityCG.cginc"
			#include "_ShaderFix.cginc"
            #include "_Snow.cginc"
			#include "_RippleClip.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				float4 scrPos : TEXCOORD1;
				float4 clr : COLOR;
				float2 data[6] : COLOR1;
			};
			
			#if defined(SHADER_API_PSSL)
			sampler2D _GrabTexture;
			#else
			sampler2D _GrabTexture : register(s0);
			#endif
			sampler2D _PalTex;
			sampler2D _MainTex;
			sampler2D _WindTexRendered;
			float4 _tileCorrection;
			float2 _MainTex_TexelSize;
			float4 _MainTex_ST;
			sampler2D _UniNoise;
			sampler2D _LevelTex;
			float2 _LevelTex_TexelSize;
			float2 _screenSize;
			float4 _spriteRect;
			float _waterLevel;
			float _RAIN;
			float _fogAmount;
			sampler2D _NoiseTex;
			sampler2D _NoiseTex2;
			
			// ============================================
			// NUEVO: Parámetro de tinte global
			// ============================================
			float _SnowTintAmount = 0.5;

			v2f vert (appdata_full v)
			{
				sampler2D _WindTex =_WindTexRendered;
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex)+_tileCorrection.zw*3.333f;
				o.clr = v.color;
				o.scrPos = ComputeScreenPos(o.pos);
				half _SNOW = _RAIN;
				half2 uv = o.uv+half2(_SNOW,0);
				o.data[0] = uv*float2(2,6.3)+float2(_SNOW*6,0);
				o.data[1] = uv*float2(2,4.1)+float2(-_SNOW*2,_SNOW);
				o.data[2] = uv*float2(1,3)+float2(_SNOW*2,_SNOW*.2);
				o.data[3] = uv*4*float2(.5,1)+float2(_SNOW*1,1);
				o.data[4] = uv*8*float2(.5,1)+float2(_SNOW*10,1);
				o.data[5] = uv*10*float2(.5,1)+float2(_SNOW*20,0);
				return o;
			}
			
			float ShGain(float x, float k) 
			{
				float a = 0.5*pow(2.0*((x<0.5)?x:1.0-x), k);
				return (x<0.5)?a:1.0-a;
			}
			
			float GetDepth (float a)
			{
				if (a==1.0) return 255;
				a=round(a*255);
				float shadows = (step(a,90)*-1+1)*90;
				return fmod(a-shadows-1, 30);
			}

			fixed4 frag (v2f i) : SV_Target
			{
				sampler2D _WindTex =_WindTexRendered;

				float2 textCoord = float2(floor(i.scrPos.x*_screenSize.x)/_screenSize.x, floor(i.scrPos.y*_screenSize.y)/_screenSize.y);
				textCoord.x -= _spriteRect.x;
				textCoord.y -= _spriteRect.y;
				textCoord.x /= _spriteRect.z - _spriteRect.x;
				textCoord.y /= _spriteRect.w - _spriteRect.y;

				half2 mapCoord = textCoord*float2(_tileCorrection.x,_tileCorrection.y)+float2(_tileCorrection.z,_tileCorrection.w);
				half snow = 0;
				half whiteout = (1+i.clr.y);
                fixed4 levelCol = tex2D(_LevelTex,textCoord);
                levelCol = AddSnow(levelCol,textCoord,i.scrPos);
				half depth = GetDepth(levelCol.x)*0.0333333333333333;	
				depth = clamp(depth,0,1);
				half4 grabColor = tex2D(_GrabTexture, half2(i.scrPos.x, i.scrPos.y));
				if( (grabColor.x > 1.0/255.0 || grabColor.y != 0.0 || grabColor.z != 0.0)&&depth>0.1666666666666667) 
				depth = 0.1666666666666667 ;
				half windmap = tex2D(_WindTex,mapCoord).z;
				_waterLevel+=.18-clamp((1-smoothstep(0,.3,windmap)),0,1)*.015;
				float watermask = clamp(smoothstep(1-_waterLevel-.14,_waterLevel,i.scrPos.y),0,1);
				half snInt = clamp(i.clr.x*smoothstep(0,.6,windmap)+clamp(1-step(depth,.99),0,1)*smoothstep(.6,0,windmap)*i.clr.x*.7,0,1);
				snInt*=watermask;

				float mediumNoise = tex2D(_NoiseTex,i.data[0]);
				float smallNoise = tex2D(_NoiseTex,i.data[1]);
				float bigNoise = tex2D(_NoiseTex,i.data[2]);
				float small = tex2D(_UniNoise,i.data[3]).x;
				small -= tex2D(_UniNoise,i.data[4]).y;
				small -= tex2D(_UniNoise,i.data[5]).z;
				snow = 1-smoothstep(-0,4.5,bigNoise*1.2+smallNoise*.8+bigNoise*1.5+mediumNoise*.9);
				snow = snow+clamp(small,0,1)*.3;
				snow = clamp(snow,0,1);
				
				fixed4 fog = clamp(tex2D(_PalTex, half2(1.5/32.0, 7.5/8.0))*snow+snow,0,1);	
				
                fixed4 result = (fixed4)(clamp(lerp(0,snow*snInt*.5,ShGain(depth,1-snInt*.9)*ShGain(depth,1-snInt*.8)+depth*snInt*4),0,1)*.9)*fog*whiteout*fixed4(1,1,1,snow);
                
                // ============================================
                // NUEVO: Aplicar tinte global
                // ============================================
                float tintFactor = (_SnowTintAmount - 0.5) * 2.0;
                float tintOpacity = abs(tintFactor);
                float3 tintColor = tintFactor > 0.0 ? float3(1.0, 1.0, 1.0) : float3(0.0, 0.0, 0.0);
                result.xyz = lerp(result.xyz, tintColor, tintOpacity);
                
                smoothRippleClip(result, i.scrPos);
				return result;

				return float4(1,1,1,1);
			}
			ENDCG
		}
	}
}