#pragma multi_compile_local _ bloom
sampler2D _LevelTex;
sampler2D _NoiseTex;
sampler2D _PalTex;
sampler2D _GrabTexture;
float4 _spriteRect;

uniform float _RAIN;

struct v2f {
    float4  pos : SV_POSITION;
    float2  uv : TEXCOORD0;
    float2 scrPos : TEXCOORD1;
    float4 clr : COLOR;
    float2 texCoord : TEXCOORD3;
};

float4 _MainTex_ST;

inline fixed4 flameJet(v2f i){
    fixed3 s0 =  tex2D(_PalTex,float2(20/32.0, 5/8.0) ).xyz*.4+.3;
    s0 = fixed3(clamp(s0.x,0.3,.8),clamp(s0.y,0.3,.8),clamp(s0.z,0.3,.8))*(1-i.uv.x*.9);
    fixed3 f0 = fixed3(0.82,0.365,0.106);
    fixed3 f1 = fixed3(0.098,0.133,0.424);
    float t = _RAIN+i.clr.z;
    float2 cuv = i.uv*2-1;
    cuv.y *=1+saturate(smoothstep(0.4-i.clr.x*.2,-i.clr.x*.2,i.uv.x))*5;
    float center = abs(cuv.x);
    float middle = smoothstep(.5,-center*.2-.1,abs(cuv.y)*.5); 
    float edgeFade = smoothstep(.1,1,abs(cuv.x));
    fixed firstHalf = step(.5,i.uv.x);
    fixed secondHalf = 1-firstHalf;
    fixed pipeFade = smoothstep(.95,1,abs(cuv.x))*secondHalf;
    center = smoothstep(0,1,abs(cuv.x));


    float bigNoise = tex2D(_NoiseTex,i.uv*fixed2(1,.123)*.5+i.clr.z-float2(t*3.0-middle*.03,0));
    float noise = tex2D(_NoiseTex,i.uv*fixed2(1,.32)+i.clr.z-float2(t*3+middle*.125,bigNoise*.5));
#if bloom
    float noise1 = .5;
    float tinyNoise =.5;
    float tinyNoiseRev = .5;
#else
    float noise1 = tex2D(_NoiseTex,i.uv*fixed2(1,.32)*1.9+fixed2(.11,24+i.clr.z)-float2(t*4.5+middle*.13,bigNoise*.7));
    float tinyNoise = tex2D(_NoiseTex,i.uv*fixed2(1,.32)*4.1+fixed2(.51,84+i.clr.z)-float2(t*18.5+middle*.19,bigNoise*.5));
    float tinyNoiseRev = tex2D(_NoiseTex,i.uv*fixed2(1,.32)*5.1+fixed2(.91,34+i.clr.z)-float2(t*14.3+middle*.39,bigNoise*.25));
#endif

    noise +=noise1-(tinyNoiseRev*.5+tinyNoise*.5)*i.clr.x;
    noise *= .5;
    float smokeNoise = (tinyNoise*(1+i.clr.x*.2)+tinyNoiseRev*(.3+i.clr.x*.3)+noise1*.5)*.6;
    float fire = middle+noise*(.9+edgeFade*.3);
    fixed firetransition = smoothstep(.47-fire*.1,.53-fire*.1,i.clr.y);
    float fireMix = .7+edgeFade*(1-i.clr.x*.8)+.3*center*firstHalf;
    float smoke = middle*(.7+i.uv.x*.3)*(1-center*1*firstHalf)+smokeNoise*(1-center*firstHalf*.9)
                -(smoothstep(0.6-i.uv.x*.4*i.clr.x,0.9-i.uv.x*.4*i.clr.x,tinyNoise)*i.uv.x*30*smoothstep(0.5,1,i.clr.x));
    float smokeMix = .8+center*firstHalf+i.clr.x*.2;
    float slowSmoke = pulse(sin(i.uv.x*10+(bigNoise*2-1)+smoke-t*30)*.5+cuv.y,0,.6) ;
    slowSmoke += pulse(sin(i.uv.x*12+noise1*2+fire-t*33.3)*.5+cuv.y,0,.6) ;
    slowSmoke += pulse(sin(i.uv.x*11+smoke*(1+i.clr.x)-t*25.3)*.5+cuv.y,0,.6) ;
    float slowSmokeMix = .6+center*.2+ pipeFade+center*firstHalf+edgeFade*firstHalf;
    smokeMix = lerp(slowSmokeMix,smokeMix,i.clr.x)+firetransition;
    smoke = lerp(slowSmoke,smoke,i.clr.x);

    float smallFire = middle+bigNoise*(.9+edgeFade*.3)+slowSmoke*.3+noise1*.4;
    float smallFireMix = 1.1+center*.1*firstHalf+i.uv.x*1.9-bigNoise*.2+smoothstep(.1,0,middle);

    fireMix = lerp(smallFireMix,fireMix,i.clr.x);
    fire = lerp(smallFire,fire,i.clr.x);


    float mask = step(lerp(smokeMix,fireMix,firetransition),lerp(smoke,fire,firetransition));
    float fcm = smoothstep(center*(1+i.clr.x*.2),1.6-i.clr.x*.3,noise+middle);//fire color mask
    fixed3 fireColor =lerp(f1,f0*(1+(1-center)*4*(1-i.clr.x*.5)),fcm)*(1+smoothstep(0.4,1,fcm)*2);
    fixed3 smokeColor =s0*smokeNoise;
    fixed3 slowSmokeColor = s0*1.8*(.2+smoothstep(0.0,2.6+pipeFade+i.uv.x*2,slowSmoke)*.8);
    fixed3 finalSmokeColor = lerp(slowSmokeColor,smokeColor,i.clr.x);
    float sfcm =(.5+smoothstep(1,3.0,smallFire)*.5+saturate(1-center*secondHalf)*.1);//small fire color mask
    fixed3 smallFireColor =lerp(f1,f0,sfcm)*(1+smoothstep(0.3,1,sfcm)*2);
    fixed3 finalFireColor = lerp(smallFireColor,fireColor,i.clr.x);
    finalSmokeColor = lerp(finalSmokeColor,finalFireColor,(1-center)*.5*smoothstep(0.2,.6,i.clr.y));
    fixed3 finalColor = lerp(finalSmokeColor, finalFireColor, firetransition);
#if bloom
    center = smoothstep(0.0,.6,center);
    fixed depth = get_depth_sat(tex2D(_LevelTex,i.texCoord).x);
    depth =  saturate(iLerp(0,5,depth));
    mask = smoothstep(fireMix-.5,fireMix+.3,fire)*(1-center*.9)*(.05+.15*i.clr.x)*firetransition;
    mask*=depth;
    return fixed4(finalColor*mask,mask);
#else
    return fixed4(finalColor,mask*i.clr.w);
#endif
}

