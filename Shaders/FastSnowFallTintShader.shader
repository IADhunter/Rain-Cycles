Shader "Futile/FastSnowFallTintShader"
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
				fixed3 data[10] : TEXCOORD2;
			};
			
			#if defined(SHADER_API_PSSL)
			sampler2D _GrabTexture;
			#else
			sampler2D _GrabTexture : register(s0);
			#endif
			sampler2D _PalTex;
			float4 _tileCorrection;
			sampler2D _MainTex;
			float2 _MainTex_TexelSize;
			float4 _MainTex_ST;
			sampler2D _WindTexRendered;
			sampler2D _LevelTex;
			sampler2D _UniNoise;
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
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex) +_tileCorrection.zw * 3.333f;
				o.clr = v.color;
				o.scrPos = ComputeScreenPos(o.pos);
				for (int k = 0; k<10;k++)
				{
					float div = (1-(float)k/10);
					float div2 = div*clamp(7-k,-1,1);
					float4 random = tex2Dlod(_UniNoise,float4(div,div*4,0,0));
					float scale = 5;
					float _SNOW = scale*_RAIN*.15*.7*(1.3-div);
					o.data[k] = float4(o.uv.x*(scale+div)+_SNOW*(random.y-.5)*div2,o.uv.y*(scale+div)+_SNOW+_SNOW*div2,0,0)+float4(random.x,random.z,0,0);
				}
				return o;
			}
			
			float ShEaseOutExpo(float t) {
				return (t == 1.0) ? 1.0 : -pow(2.0, -10.0 * t) + 1.0;
			}
			
			float lightness(float4 t) {
				return max(max(t.x,t.y),t.z);
			}
			
			float GetDepth (float a)
			{
				if (a==1.0) return 255;
				a=round(a*255);
				float shadows = (step(a,90)*-1+1)*90;
				return fmod(a-shadows-1, 30);
			}
			
			float GetDepth2 (float a)
			{
				if (a==1.0) return 255;
				a=round(a*254);
				return fmod(a, 30);
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				sampler2D _WindTex =_WindTexRendered;
				float2 textCoord = float2(floor(i.scrPos.x*_screenSize.x)/_screenSize.x, floor(i.scrPos.y*_screenSize.y)/_screenSize.y);
				textCoord.x -= _spriteRect.x;
				textCoord.y -= _spriteRect.y;
				textCoord.x /= _spriteRect.z - _spriteRect.x;
				textCoord.y /= _spriteRect.w - _spriteRect.y;

				half _SNOW = _RAIN*.3;
				half2 mapCoord = textCoord*half2(_tileCorrection.x,_tileCorrection.y)+half2(_tileCorrection.z,_tileCorrection.w);
                fixed4 levelCol = tex2D(_LevelTex,textCoord);
                levelCol = AddSnow(levelCol,textCoord,i.scrPos);
				half depth = GetDepth(levelCol.x)*0.0333333333333333;	
				half windmap = tex2D(_WindTex,mapCoord).y;
				_waterLevel+=.14-clamp((1-smoothstep(0,.1,windmap)),0,1)*.015;
				float watermask = clamp(smoothstep(1-_waterLevel-.05,_waterLevel,i.scrPos.y),0,1);
				half snInt = i.clr.x*smoothstep(.0,.4,windmap)+clamp(1-step(depth,.99),0,1)*smoothstep(.4,0,windmap)*i.clr.x*.4;
				snInt*=watermask;
				snInt = ShEaseOutExpo(snInt);
				snInt*=.2;
				half snow = 0;
				half4 snow9=tex2D(_UniNoise,i.data[8]);
				half4 snow8=tex2D(_UniNoise,i.data[7]);
				half4 snow7=tex2D(_UniNoise,i.data[6]);
				half4 snow6=tex2D(_UniNoise,i.data[5]);
				half4 snow5=tex2D(_UniNoise,i.data[4]);
				half4 snow4=tex2D(_UniNoise,i.data[3]);
				half4 snow3=tex2D(_UniNoise,i.data[2]);
				half4 snow2=tex2D(_UniNoise,i.data[1]);
				half4 snow1=tex2D(_UniNoise,i.data[0]);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow1.x+snow1.y)*.25);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow2.z+snow2.w)*0.44375);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow3.x+snow3.y)*0.6375);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow4.z+snow4.w)*0.83125);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow5.x+snow5.y)*1.025);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow6.z+snow6.w)*1.21875);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow7.x+snow7.y)*1.5);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow8.z+snow8.w)*1.75);
				snow = max(snow, smoothstep(snInt,snInt-0.1,snow9.x+snow9.y)*1.8);
				snow *=.5;

				half4 grabColor = tex2D(_GrabTexture, half2(i.scrPos.x, i.scrPos.y));
				if( (grabColor.x > 1.0/255.0 || grabColor.y != 0.0 || grabColor.z != 0.0)&&(1-snow)>0.1666666666666667) 
				{
					return fixed4(0, 0, 0, 0);
				}
				
				fixed4 fog = tex2D(_PalTex, half2(1.5/32.0, 7.5/8.0));				
				fixed4 colSnow = tex2D(_PalTex,half2((1-snow*.8)*0.9375,0.125+0.0625))+fixed4(0,0,0,1);
				colSnow = lerp(colSnow,fog,_fogAmount*(1-snow*.9));
				
				if ((1-snow*.9)<depth)
				{
					if (snow==0){
						return 0;
					}
				
					half mask = 1-step(snow,0.01);
					snow = lerp(0,snow*.4+.4,mask);
					float4 res = (colSnow*.5+snow)*fixed4(1,1,1,mask);
					
					// ============================================
					// NUEVO: Aplicar tinte global
					// ============================================
					float tintFactor = (_SnowTintAmount - 0.5) * 2.0;
					float tintOpacity = abs(tintFactor);
					float3 tintColor = tintFactor > 0.0 ? float3(1.0, 1.0, 1.0) : float3(0.0, 0.0, 0.0);
					res.xyz = lerp(res.xyz, tintColor, tintOpacity);
					
                    smoothRippleClip(res,i.scrPos);
					return res;
				}
				return 0;				
			}
			ENDCG
		}
	}
}