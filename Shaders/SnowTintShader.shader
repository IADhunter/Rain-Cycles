Shader "Futile/SnowTintShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags {"Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}
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
			AlphaTest Greater 0.8
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex vert
			#pragma fragment frag		
			#pragma profileoption NumInstructionSlots=4096
			#pragma profileoption NumMathInstructionSlots=4096
            #pragma multi_compile __ HR
			#pragma exclude_renderers OpenGL
			#include "UnityCG.cginc"
			#include "_ShaderFix.cginc"
            #include "_Functions.cginc"
            #include "_RippleClip.cginc"
            #include "_Snow.cginc"

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
			};
			#if defined(SHADER_API_PSSL)
			sampler2D _GrabTexture;
			#else
			sampler2D _GrabTexture : register(s0);
			#endif
			sampler2D _PalTex;
			float _light = 0;
			sampler2D _MainTex;
			float2 _MainTex_TexelSize;
			float4 _MainTex_ST;
			sampler2D _LevelTex;
			float2 _LevelTex_TexelSize;
			float4 _lightDirAndPixelSize;
			float2 _screenSize;
			float4 _spriteRect;
			float4 _EffectColor;
			float _WetTerrain;
			float _waterLevel;
			float _RAIN;
			float _cloudsSpeed;
			float _fogAmount;
			sampler2D _NoiseTex;
			sampler2D _NoiseTex2;
			sampler2D _UniNoise;
			sampler2D _SnowSources;
			float2 _SnowSources_TexelSize;
			
			// ============================================
			// PARÁMETROS DEL MOD
			// ============================================
			float _SnowTintAmount = 0.5;      // 0 = negro, 0.5 = vanilla, 1 = blanco
			float _SnowGrainAmount = 0.0;     // 0 = sin grain, 1 = máximo
			
			// ============================================
			// PARÁMETROS DEL TERRAIN (para grain)
			// ============================================
			uniform float4 _terrainParams;    // (light factor, waves, edge radius, grain)

			v2f vert (appdata_full v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
				o.clr = v.color;
				o.scrPos = ComputeScreenPos(o.pos);
				return o;
			}
                
			float GetDepth (float a)
			{
				if (a==1.0) return 255;
				a=round(a*255);
				float shadows = (step(a,90)*-1+1)*90;
				return fmod(a-shadows-1, 30);
			}

			float GetShadows (float a)
			{
				a*=255;
				return (step(round(a),90)*-1+1);
			}
			
			float ShGain(float x, float k) 
			{
				float a = 0.5*pow(2.0*((x<0.5)?x:1.0-x), k);
				return (x<0.5)?a:1.0-a;
			}
			
			float4 frag(v2f i) : SV_Target
			{
				float2 textCoord = float2(floor(i.scrPos.x*_screenSize.x)/_screenSize.x, floor(i.scrPos.y*_screenSize.y)/_screenSize.y);
				textCoord.x -= _spriteRect.x;
				textCoord.y -= _spriteRect.y;

				textCoord.x /= _spriteRect.z - _spriteRect.x;
				textCoord.y /= _spriteRect.w - _spriteRect.y;
#if RIPPLE
                fixed rippleMask = tex2D(_GameplayRippleMask,i.scrPos).x;
                textCoord += rippleDistortion(rippleMask, i.scrPos)*1;
#endif
				float2 distUV=float2(textCoord.x,textCoord.y);
				float4 texCol = tex2D(_SnowTex,distUV);
				float depth = GetDepth(texCol.x);
				float4 grabColor = tex2D(_GrabTexture, float2(i.scrPos.x, i.scrPos.y));
				clip(grabColor.a - 0.8);
				if( (grabColor.x > 1.0/255.0 || grabColor.y != 0.0 || grabColor.z != 0.0)&&depth>5.0) 
				return float4(0,0,0,0);
				float shadows = GetShadows(texCol.x);
				float shadowGradient = (texCol.x-(depth+1)/255-(shadows/255*90))*4.25;
				float4 fog = tex2D(_PalTex, float2(1.5/32.0, 7.5/8.0));

				//////////////RAINWORLD SHADOWS
				float shadow = tex2D(_NoiseTex, float2((textCoord.x*0.5) + (_RAIN*0.1*_cloudsSpeed) - (0.003*(clamp(depth,0,30))), 1-(textCoord.y*0.5) + (_RAIN*0.2*_cloudsSpeed) - (0.003*(clamp(depth,0,30))))).x;
	
				shadow = 0.5 + sin(fmod(shadow+(_RAIN*0.1*_cloudsSpeed)-textCoord.y, 1)*3.14*2)*0.5;
				shadow = clamp(((shadow - 0.5)*6)+0.5-(_light*4), 0,1);

				float4 shadowFix = FixEdgeShadowStretch(i.scrPos,true);
				float2 grabPos =  float2(i.scrPos.x + -_lightDirAndPixelSize.x*_lightDirAndPixelSize.z*(depth-5)*shadowFix.x,
										 i.scrPos.y +  _lightDirAndPixelSize.y*_lightDirAndPixelSize.w*(depth-5)*shadowFix.y);
				grabPos = lerp(grabPos, ((grabPos-float2(0.5, 0.3))*(1 + (depth-5.0)/460.0))+float2(0.5, 0.3), shadowFix.zw);
				
				// ============================================
				// FIX BUG: Sombra de criaturas iluminadas
				// ============================================
				// ANTES: float4 grabColor2 = tex2D(_GrabTexture,grabPos);
				//        grabColor2 = -step(grabColor2,0.003921568627451)+1;
				// PROBLEMA: Leía colores RGB de criaturas iluminadas (piernas, cabeza)
				//           causando que esos colores aparecieran en la sombra proyectada.
				// FIX: Convertir a luminancia antes del step para forzar monocromático.
				//      Así cualquier color (rojo, azul, verde) se trata igual: "hay algo aquí".
				
				float4 grabColor2 = tex2D(_GrabTexture, grabPos);
				// Convertir a luminancia para evitar que colores de criaturas afecten la sombra
				float grabLuminance = dot(grabColor2.rgb, float3(0.299, 0.587, 0.114));
				grabColor2 = -step(grabLuminance, 0.003921568627451) + 1;
				
				if (depth < 6) {
					grabColor2 = 0;
				}
					
				////////////// FACTOR DE LUZ LOCAL
				float lightFactor = (-shadow+1)*shadows*(-grabColor2+1);
				
				//////////////CONTINUE
				#if HR
					float4 snow = tex2D(_PalTex,float2(depth*0.03333*0.6375,0.125+(shadowGradient*0.0625)));
					float4 snowLight = tex2D(_PalTex,float2(depth*0.03333*0.6375,0.57+(shadowGradient*0.0625)));
					snow = lerp(snow,snowLight,(-shadow+1)*shadows*(-grabColor2+1));
					snow = .02+snow-shadowGradient*.01;
					snow = lerp(snow,snow+fog*.2,_fogAmount*(depth/30));
                    float4 result = float4(snow.xyz,texCol.y);
                    smoothRippleClip(result, i.scrPos);
                    
                    // ============================================
                    // SNOWLIGHT — Tinte global (0=negro, 0.5=vanilla, 1=blanco)
                    // ============================================
                    float tintFactor = (_SnowTintAmount - 0.5) * 2.0;
                    float tintOpacity = abs(tintFactor);
                    float3 tintColor = tintFactor > 0.0 ? float3(1.0, 1.0, 1.0) : float3(0.0, 0.0, 0.0);
                    result.xyz = lerp(result.xyz, tintColor, tintOpacity);
                    
                    // ============================================
                    // SNOWGRAIN — Escarcha granular (por encima del tinte)
                    // ============================================
                    if (_SnowGrainAmount > 0.0)
                    {
                        // 1. Crear pseudo-normal desde _UniNoise
                        float2 grainUV = textCoord * 40.0;
                        float grainNoise = tex2D(_UniNoise, grainUV).r;
                        float grainNoise2 = tex2D(_UniNoise, grainUV * 0.75 + float2(0.5, 0.5)).r;
                        
                        // Normal base plana
                        float3 normalMap = float3(0.0, 0.0, 1.0);
                        
                        // grainFactor direccional
                        float grainFactor = saturate(
                            dot(normalize(float2(-_lightDirAndPixelSize.x, _lightDirAndPixelSize.y)), 
                                normalMap.xy) * (1.0 - abs(normalMap.z) * 0.25)
                        );
                        grainFactor = pow(max((grainFactor - 1.0) / (1.0 - _terrainParams.x) + 1.0, 0.0), 0.2) * 0.8 + 0.2;
                        
                        // Perturbar normal
                        normalMap.x += pow(grainNoise * 2.0 - 1.0, 3) * _SnowGrainAmount * grainFactor * 0.5;
                        normalMap.y += pow(grainNoise2 * 2.0 - 1.0, 3) * _SnowGrainAmount * grainFactor * 0.5;
                        normalMap = normalize(normalMap);
                        
                        // 2. Iluminación especular con ShGain
                        float3 lightDir = normalize(float3(-_lightDirAndPixelSize.x, _lightDirAndPixelSize.y, 2.0));
                        float snowLighting = max(dot(lightDir, normalMap), 0.0);
                        snowLighting = ShGain(snowLighting, 1.0 - _terrainParams.x * 2.0);
                        
                        // 3. Whiteout
                        float whiteout = 1.0 + tex2D(_UniNoise, grainUV * 1.3).g * 0.5;
                        
                        // ============================================
                        // 4. WAVE ANIMADO — reflejo suave que desplaza los brillantes
                        // ============================================
                        
                        float2 waveDir = normalize(float2(-_lightDirAndPixelSize.x, _lightDirAndPixelSize.y));
                        if (length(waveDir) < 0.1) waveDir = float2(0.7, 0.7);
                        
                        float waveCoord = dot(textCoord, waveDir);
                        
                        float slowWave = sin(waveCoord * 12.0 + _RAIN * 0.05) * 0.10;
                        float fastWave = sin(waveCoord * 35.0 + _RAIN * 0.09) * 0.025;
                        
                        float2 waveOffset = waveDir * (slowWave + fastWave);
                        
                        // ============================================
                        // 5. BLINK ESTÁTICO — dos capas de brillo:
                        //    Capa A: MUY BRILLANTES (más raros, intensos)
                        //    Capa B: BRILLANTES (intensidad -40%)
                        // ============================================
                        
                        // --- CAPA A: Muy brillantes (más raros, wave aumentado) ---
                        float2 blinkUV = grainUV * 3.7 + float2(0.13, 0.67) + waveOffset * 1.15;
                        float blinkMask = tex2D(_UniNoise, blinkUV).r * tex2D(_UniNoise, blinkUV * 0.6 + float2(0.31, 0.89)).g;
                        float brightBlink = smoothstep(0.52, 0.82, blinkMask);
                        brightBlink = pow(brightBlink, 1.5) * 3.0;
                        
                        // --- CAPA B: Brillantes (wave casi igual, intensidad -40%) ---
                        float2 semiBlinkUV = grainUV * 3.7 + float2(0.73, 0.27) + waveOffset * 0.98;
                        float semiBlinkMask = tex2D(_UniNoise, semiBlinkUV).r * tex2D(_UniNoise, semiBlinkUV * 0.6 + float2(0.31, 0.89)).g;
                        float semiBlink = smoothstep(0.35, 0.65, semiBlinkMask);
                        semiBlink = pow(semiBlink, 1.5) * 0.9;
                        
                        // Combinar ambas capas de brillo
                        float staticBlink = 1.0 + (brightBlink + semiBlink) * lightFactor;
                        
                        // ============================================
                        // 6. RELIEVE OSCURO — dos capas de tonos oscuros:
                        //    Capa A: Zonas de luz (muy dispersos, muy sutiles)
                        //    Capa B: Zonas de sombra (más juntitos, hasta 27% opacidad)
                        // ============================================
                        
                        // Factor de sombra combinado (0 = luz total, 1 = sombra total)
                        float combinedShadow = saturate(
                            (1.0 - saturate(shadowGradient * 0.5 + 0.5)) * 0.35 +
                            (1.0 - lightFactor) * 0.35 +
                            shadows * 0.30
                        );
                        
                        // --- CAPA OSCURA A: Luz directa (muy dispersa, opacidad ~4%) ---
                        float2 darkUV_A = grainUV * 5.3 + float2(0.91, 0.43);
                        float darkMask_A = tex2D(_UniNoise, darkUV_A).r;
                        float darkStrength_A = smoothstep(0.78, 0.94, darkMask_A);
                        darkStrength_A = pow(darkStrength_A, 2.0) * 0.04;
                        darkStrength_A *= lightFactor * 0.25;
                        
                        // --- CAPA OSCURA B: Zonas de sombra (más juntitos, variaciones de opacidad) ---
                        float2 darkUV_B = grainUV * 3.8 + float2(0.17, 0.83);
                        float darkMask_B = tex2D(_UniNoise, darkUV_B).r * tex2D(_UniNoise, darkUV_B * 0.65 + float2(0.47, 0.11)).g;
                        float darkStrength_B = smoothstep(0.28, 0.52, darkMask_B);
                        
                        // VARIACIONES DE OPACIDAD: segunda máscara que modula intensidad por parches
                        float2 darkVarUV = grainUV * 1.9 + float2(0.61, 0.19);
                        float darkVariation = tex2D(_UniNoise, darkVarUV).r;
                        darkVariation = lerp(0.4, 1.0, darkVariation);
                        darkStrength_B *= darkVariation;
                        
                        // Máx opacidad reducida al 27% para suavizar donde tapan las nubes
                        darkStrength_B = pow(darkStrength_B, 1.3) * 0.27;
                        darkStrength_B *= combinedShadow;
                        
                        // Combinar capas oscuras
                        float totalDarkStrength = saturate(darkStrength_A + darkStrength_B) * _SnowGrainAmount;
                        
                        // Color del relieve oscuro: muy oscuro, ligeramente azulado
                        float3 darkGrainColor = snow.rgb * 0.15 + float3(0.01, 0.01, 0.015);
                        
                        // Aplicar relieve oscuro sobre la base ANTES del grain claro
                        float3 baseWithRelief = lerp(result.rgb, darkGrainColor, totalDarkStrength);
                        
                        // 7. Color del grain claro = 60% blanco + 40% color de nieve
                        float3 grainColor = lerp(snow.rgb, float3(1.0, 1.0, 1.0), 0.6);
                        
                        // BOOST de brillo base en zonas de luz
                        grainColor *= 1.0 + lightFactor * 0.5;
                        
                        // Aplicar blink estático (multiplicativo)
                        grainColor *= staticBlink;
                        
                        // 8. OPACIDAD por capas de luz de la nieve (shadowGradient)
                        float grainLayerOpacity;
                        if (shadowGradient > 0.3)
                        {
                            grainLayerOpacity = lerp(0.6, 1.0, smoothstep(0.3, 0.8, shadowGradient));
                        }
                        else if (shadowGradient > -0.3)
                        {
                            grainLayerOpacity = lerp(0.08, 0.35, smoothstep(-0.3, 0.3, shadowGradient));
                        }
                        else
                        {
                            grainLayerOpacity = 0.08;
                        }
                        
                        // 9. Luz para grain: mínimo 0.15 para que las nubes no anulen del todo
                        float grainLight = 0.15 + 0.85 * lightFactor;
                        
                        // 10. Atenuación por ripple
                        float rippleAttenuation = 1.0;
                        #if RIPPLE
                        rippleAttenuation = 1.0 - tex2D(_GameplayRippleMask, i.scrPos).x * 0.3;
                        #endif
                        
                        // 11. Multiplicar por fog
                        float3 grainWithFog = lerp(grainColor, fog.rgb, _fogAmount * 0.3);
                        
                        // 12. Combinar todo: base con relieve oscuro → lerp hacia grain claro
                        float grainStrength = _SnowGrainAmount * snowLighting * grainLight * whiteout * grainLayerOpacity * rippleAttenuation * 0.6;
                        result.rgb = lerp(baseWithRelief, grainWithFog, grainStrength);
                    }
                    
                    return result;
				#else
					float4 snow = tex2D(_PalTex,float2(depth*0.03333*0.9375,0.125+(shadowGradient*0.0625)));
					float4 snowLight = tex2D(_PalTex,float2(depth*0.03333*0.9375,0.57+(shadowGradient*0.0625)));
					snow = lerp(snow,snowLight,(-shadow+1)*shadows*(-grabColor2+1));
					snow+=.2+shadowGradient*.1;
					snow = lerp(snow,fog,_fogAmount*(depth/30));
                    float4 result = float4(snow.xyz,texCol.y);
                    smoothRippleClip(result, i.scrPos);
                    
                    // ============================================
                    // SNOWLIGHT — Tinte global
                    // ============================================
                    float tintFactor = (_SnowTintAmount - 0.5) * 2.0;
                    float tintOpacity = abs(tintFactor);
                    float3 tintColor = tintFactor > 0.0 ? float3(1.0, 1.0, 1.0) : float3(0.0, 0.0, 0.0);
                    result.xyz = lerp(result.xyz, tintColor, tintOpacity);
                    
                    // ============================================
                    // SNOWGRAIN — Escarcha granular
                    // ============================================
                    if (_SnowGrainAmount > 0.0)
                    {
                        // 1. Crear pseudo-normal
                        float2 grainUV = textCoord * 40.0;
                        float grainNoise = tex2D(_UniNoise, grainUV).r;
                        float grainNoise2 = tex2D(_UniNoise, grainUV * 0.75 + float2(0.5, 0.5)).r;
                        
                        float3 normalMap = float3(0.0, 0.0, 1.0);
                        
                        float grainFactor = saturate(
                            dot(normalize(float2(-_lightDirAndPixelSize.x, _lightDirAndPixelSize.y)), 
                                normalMap.xy) * (1.0 - abs(normalMap.z) * 0.25)
                        );
                        grainFactor = pow(max((grainFactor - 1.0) / (1.0 - _terrainParams.x) + 1.0, 0.0), 0.2) * 0.8 + 0.2;
                        
                        normalMap.x += pow(grainNoise * 2.0 - 1.0, 3) * _SnowGrainAmount * grainFactor * 0.5;
                        normalMap.y += pow(grainNoise2 * 2.0 - 1.0, 3) * _SnowGrainAmount * grainFactor * 0.5;
                        normalMap = normalize(normalMap);
                        
                        // 2. Iluminación especular
                        float3 lightDir = normalize(float3(-_lightDirAndPixelSize.x, _lightDirAndPixelSize.y, 2.0));
                        float snowLighting = max(dot(lightDir, normalMap), 0.0);
                        snowLighting = ShGain(snowLighting, 1.0 - _terrainParams.x * 2.0);
                        
                        // 3. Whiteout
                        float whiteout = 1.0 + tex2D(_UniNoise, grainUV * 1.3).g * 0.5;
                        
                        // ============================================
                        // 4. WAVE ANIMADO
                        // ============================================
                        
                        float2 waveDir = normalize(float2(-_lightDirAndPixelSize.x, _lightDirAndPixelSize.y));
                        if (length(waveDir) < 0.1) waveDir = float2(0.7, 0.7);
                        
                        float waveCoord = dot(textCoord, waveDir);
                        
                        float slowWave = sin(waveCoord * 12.0 + _RAIN * 0.05) * 0.10;
                        float fastWave = sin(waveCoord * 35.0 + _RAIN * 0.09) * 0.025;
                        
                        float2 waveOffset = waveDir * (slowWave + fastWave);
                        
                        // ============================================
                        // 5. BLINK ESTÁTICO — dos capas de brillo
                        // ============================================
                        
                        // --- CAPA A: Muy brillantes (más raros) ---
                        float2 blinkUV = grainUV * 3.7 + float2(0.13, 0.67) + waveOffset * 1.15;
                        float blinkMask = tex2D(_UniNoise, blinkUV).r * tex2D(_UniNoise, blinkUV * 0.6 + float2(0.31, 0.89)).g;
                        float brightBlink = smoothstep(0.52, 0.82, blinkMask);
                        brightBlink = pow(brightBlink, 1.5) * 3.0;
                        
                        // --- CAPA B: Brillantes (intensidad -40%) ---
                        float2 semiBlinkUV = grainUV * 3.7 + float2(0.73, 0.27) + waveOffset * 0.98;
                        float semiBlinkMask = tex2D(_UniNoise, semiBlinkUV).r * tex2D(_UniNoise, semiBlinkUV * 0.6 + float2(0.31, 0.89)).g;
                        float semiBlink = smoothstep(0.35, 0.65, semiBlinkMask);
                        semiBlink = pow(semiBlink, 1.5) * 0.9;
                        
                        float staticBlink = 1.0 + (brightBlink + semiBlink) * lightFactor;
                        
                        // ============================================
                        // 6. RELIEVE OSCURO
                        // ============================================
                        
                        float combinedShadow = saturate(
                            (1.0 - saturate(shadowGradient * 0.5 + 0.5)) * 0.35 +
                            (1.0 - lightFactor) * 0.35 +
                            shadows * 0.30
                        );
                        
                        // --- CAPA OSCURA A: Luz directa (muy dispersa, ~4%) ---
                        float2 darkUV_A = grainUV * 5.3 + float2(0.91, 0.43);
                        float darkMask_A = tex2D(_UniNoise, darkUV_A).r;
                        float darkStrength_A = smoothstep(0.78, 0.94, darkMask_A);
                        darkStrength_A = pow(darkStrength_A, 2.0) * 0.04;
                        darkStrength_A *= lightFactor * 0.25;
                        
                        // --- CAPA OSCURA B: Zonas de sombra ---
                        float2 darkUV_B = grainUV * 3.8 + float2(0.17, 0.83);
                        float darkMask_B = tex2D(_UniNoise, darkUV_B).r * tex2D(_UniNoise, darkUV_B * 0.65 + float2(0.47, 0.11)).g;
                        float darkStrength_B = smoothstep(0.28, 0.52, darkMask_B);
                        
                        float2 darkVarUV = grainUV * 1.9 + float2(0.61, 0.19);
                        float darkVariation = tex2D(_UniNoise, darkVarUV).r;
                        darkVariation = lerp(0.4, 1.0, darkVariation);
                        darkStrength_B *= darkVariation;
                        
                        // Máx opacidad reducida al 27%
                        darkStrength_B = pow(darkStrength_B, 1.3) * 0.27;
                        darkStrength_B *= combinedShadow;
                        
                        float totalDarkStrength = saturate(darkStrength_A + darkStrength_B) * _SnowGrainAmount;
                        float3 darkGrainColor = snow.rgb * 0.15 + float3(0.01, 0.01, 0.015);
                        float3 baseWithRelief = lerp(result.rgb, darkGrainColor, totalDarkStrength);
                        
                        // 7. Color del grain claro
                        float3 grainColor = lerp(snow.rgb, float3(1.0, 1.0, 1.0), 0.6);
                        grainColor *= 1.0 + lightFactor * 0.5;
                        grainColor *= staticBlink;
                        
                        // 8. OPACIDAD por capas de luz
                        float grainLayerOpacity;
                        if (shadowGradient > 0.3)
                        {
                            grainLayerOpacity = lerp(0.6, 1.0, smoothstep(0.3, 0.8, shadowGradient));
                        }
                        else if (shadowGradient > -0.3)
                        {
                            grainLayerOpacity = lerp(0.08, 0.35, smoothstep(-0.3, 0.3, shadowGradient));
                        }
                        else
                        {
                            grainLayerOpacity = 0.08;
                        }
                        
                        // 9. Luz para grain con mínimo para nubes
                        float grainLight = 0.15 + 0.85 * lightFactor;
                        
                        // 10. Ripple
                        float rippleAttenuation = 1.0;
                        #if RIPPLE
                        rippleAttenuation = 1.0 - tex2D(_GameplayRippleMask, i.scrPos).x * 0.3;
                        #endif
                        
                        // 11. Fog
                        float3 grainWithFog = lerp(grainColor, fog.rgb, _fogAmount * 0.3);
                        
                        // 12. Combinar
                        float grainStrength = _SnowGrainAmount * snowLighting * grainLight * whiteout * grainLayerOpacity * rippleAttenuation * 0.6;
                        result.rgb = lerp(baseWithRelief, grainWithFog, grainStrength);
                    }
                    
                    return result;
				#endif
			}
			ENDCG
		}
	}
	FallBack "Transparent"
}