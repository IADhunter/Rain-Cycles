static float _DepthMult = .766;

float _pulse(float value, float p, float a){
    return smoothstep(a,0,abs(value-p));
}

fixed _stripe(float v, float w){
   return step(-w, v)*step(v,w);
}

inline float _iLerp(float from, float to, float value){
    return (value-from)/(to-from);
}

inline float _mod(float x, float y){ //GLSL-like modulo function
    return x-y*floor(x/y);
}

inline float TubeSegment(float4 clr, float2 uv, float2 scrPos, sampler2D _NoiseTex, float _RAIN) {
    float2 cuv = abs(uv*2-1);
    float noise = tex2D(_NoiseTex, scrPos * 1.5 + _RAIN * 0.1 + clr.x * 0.2).x;
    noise = noise * 2.0 - 1.0;
    float sphere = 1 - pow(saturate(length(cuv) + noise * 0.4), 2);
    if (sphere <= 0) return 0;
    sphere *= 2;
    sphere = clamp(sphere, 0, 2);
    return (clr.x * .9 + sphere * .1) * _DepthMult;
}

inline float Blob(float4 clr, float2 uv,sampler2D _NoiseTex,float _RAIN){
    float wave = sin(_RAIN*(3+9*clr.z)+clr.y*3.14159)*.5+.5;
    float2 cuv = abs(uv*2-1);
    float noise = tex2D(_NoiseTex,uv*.25+fixed2(clr.y*3,clr.z*7)).x;
    noise = saturate(smoothstep(.1+wave*.4,0,abs(noise-.5)));
    float noise1 = tex2D(_NoiseTex,uv*.2+fixed2(clr.y*5+wave*.05,clr.z*17)).x;
    noise1 = smoothstep(0.25,1,noise1);
    float sphere =1-pow(saturate(length(cuv)+.15+noise1*.4-(1-wave)*.15),2);
    float blob = sphere;
    blob = sphere-noise*.2*(1-wave);
    if (blob <= 0) return 0;
    return (clr.x*.9 + blob*.1)*_DepthMult;
}

inline float Wire(float4 clr, float2 uv,sampler2D _NoiseTex, float _RAIN){
    uv.x+=clr.z*33;
    fixed wire = (-uv.y*uv.y+uv.y)*3;
    fixed direction =clr.y*2-1;
    float wave = sin(uv.x*4+_RAIN*sign(direction)*(8+10*clr.z)+clr.z*33)*clr.z*.15;
    fixed n1 = tex2D(_NoiseTex,uv*fixed2(4,.076)+fixed2(-wave*.3,clr.w*11)).x*2-1;
    fixed n = tex2D(_NoiseTex,uv*fixed2(4,.076)+fixed2(wire*direction*.2-wave*.6,clr.w*11)).x*2-1;
    wire = max(wire,wire+wave);
    if (wire-n1*.75 <= 0) return 0; 
    wire = saturate(wire-n*.51);
    return (clr.x*.9 + wire*.1)*_DepthMult;
}

inline float getRock(float2 uv, float t, float4 clr, sampler2D _NoiseTex) {
    float2 time = float2(t+clr.w*123,0);
    float dx = tex2D(_NoiseTex,uv*.1+clr.y*133).x*2-1;
    float dy = tex2D(_NoiseTex,uv*.1+clr.z*313).x*2-1;
    float2 dist = float2(dx,dy);
    uv = uv * 2 - 1;
    uv+=dist*float2(.45,.36);
    float2 nUV = float2(uv.x*2  - smoothstep(-.8, .8, uv.x) * 1.30, uv.y);
    float big = tex2D(_NoiseTex, nUV * .125 + time+clr.z*111).x;
    float shape = tex2D(_NoiseTex, nUV * .05 + sin(time * 7) * .04+clr.y*117).x;
    float small = tex2D(_NoiseTex, nUV * .25 + time * 1.5+clr.z*133 ).x;
    float tiny = tex2D(_NoiseTex, nUV * .5 + time * 3+clr.y*222).x;
    float sphere = 1-pow(saturate(length(uv)+ float2((big * 2 - 1) * .125,0)),2);
    float noise = (1 - small * .5) * (1 - big * .55) * (1 - tiny * .25);
    sphere = sphere-noise*.6;
    float rock = smoothstep(.0,.8,sphere*shape);
    if (rock <= 0.1) return 0;
    return (clr.x*.9+saturate(rock)*.1)*_DepthMult;
}

inline float CrownSegment(float2 uv, float4 clr, sampler2D _NoiseTex, float time) {
    float depth = 0;
    uv = uv * 2 - 1;
    uv *= half2(2, 0.5);
    if (uv.y > 0) uv *= half2(1, 3);
    uv += half2(0, -0.5);
    uv *= 1.2;
    depth += 1 - clr.r*0.95;
    float sphere = pow(saturate(length(uv)),2);
    depth += sphere * 0.05;
    
    if (sphere > 0.9) return 0;
    return 1 - depth;
}

inline float CandleStream(float2 cuv, float2 uv,float variation, float pos, float noise1, sampler2D _NoiseTex){
    float noise2 = tex2D(_NoiseTex,uv+variation).x;
    float bottomOffset = smoothstep(0,-1,cuv.y)*.2;
    float topCutOff = smoothstep(.3,.5,uv.y+.1+abs(pos)*.3);
    float stream1 = _pulse(cuv.x,pos+(noise1*2-1)*.10+(noise2*2-1)*.1+pos*bottomOffset,(.1+noise1*.1+bottomOffset)*topCutOff)*.1;

    return (1-abs(pos*2))*.9*step(0.0001,stream1)+stream1;
}

inline float2 Candle(float4 clr, float2 uv,sampler2D _NoiseTex,float _RAIN, bool applyDepth){
    float2 cuv = uv*2-1;
    float2 uv2 = uv*fixed2(1,1/clr.w);
    uv =fixed2(uv.x,1-uv.y)*fixed2(1,1/clr.w);
    float noise = tex2D(_NoiseTex,uv*.4+clr.z).x;
    float cNoise = noise * 2 - 1;
    float topOffset = smoothstep(.7,.3,uv.y)*.3; 
    float topCutOff = smoothstep(.2,.3,uv.y); 
    float bottomCutOff = smoothstep(1,0.5,length((float2(uv2.x,clamp(uv2.y*1.5,0,.5))*2-1)*fixed2(.5,1))); 
    float topCircle = smoothstep(.6,.2,length(fixed2(uv.x,uv.y*2+cNoise*.3)*2-1));
    cuv.x *=1-topOffset*1;
    float mainCylinder = _pulse(cuv.x,cNoise*(.05+topOffset*.5),(.5+noise*.1))*.9*topCutOff;
    cuv.x *=1-topOffset*.3;
    mainCylinder = lerp(mainCylinder,saturate(uv.y-.1)*4,topCircle);
    float combined = mainCylinder;
    float posVar = tex2D(_NoiseTex,fixed2(clr.y,clr.z))*111;
    combined = max(combined,CandleStream(cuv,uv,.2+clr.y,sin(posVar)*.5,noise,_NoiseTex));
    combined = max(combined,CandleStream(cuv,uv,.8+clr.z,sin(posVar+1.5)*.5,noise,_NoiseTex));
    combined = max(combined,CandleStream(cuv,uv,0+clr.y,sin(posVar+3)*.5,noise,_NoiseTex));
    combined = max(combined,CandleStream(cuv,uv,0.5+clr.z,sin(posVar+4.5)*.5,noise,_NoiseTex));
    fixed wick = _stripe(uv.x*2-1,.11)*_stripe(uv.y-.05,.22);
    combined = max(combined,wick*.6);
    combined = combined*bottomCutOff;
    if (combined <= 0) return 0;
    if (applyDepth)
       combined = clamp(clr.x-.101 + combined*.101,.00001,.96);
    return float2(combined,wick); 
}

inline float Cylinder(float4 clr, float2 uv, float squish, float thickness){
    float a = 0;
    float r = squish;
    float2 cuv = uv*2-1;
    float sphereGradient = sqrt(1-cuv.x*cuv.x);
    float rotOffset = thickness*sin(1-r);
    float2 offcuv=cuv-float2(0,rotOffset*.5);
    float c =offcuv.y+sphereGradient*r; 
    float cylinder = step(abs(cuv.y+sphereGradient*r),rotOffset*.5);
    float toptobottom = cylinder*((cuv.y+sphereGradient*r)/(rotOffset)+.5);
    float circle = step(length(offcuv/fixed2(1,r)),1);
    float gradient = _iLerp(r,-r,offcuv.y);
    fixed mask =saturate( circle+cylinder);
    a = lerp(a,sphereGradient*.5+.5,cylinder);
    a = lerp(a,gradient,circle);
    a = lerp(a,lerp(-sin(1-r),sin(1-r),a)*.5+.5,mask);
    if (mask <= 0) return 0;
    float d = thickness*sin(r)*.5;
    a = lerp(a,lerp(a-r*thickness*sphereGradient*.05,a,toptobottom),cylinder);

    return a;
}

inline float HalfSphere(float4 clr, float2 uv, float squish, bool applyDepth){
    squish = clamp(squish,0.00001,1);
    float a = 0;
    float r = clr.y;
    float2 cuv = uv*2-1;
    float circle = step(length(cuv/fixed2(1,r)),1);
    float gradient = _iLerp(r,-r,cuv.y);
    squish = lerp((1/squish),1,r);
    a = length(cuv*fixed2(1,squish));
    a = sqrt(1-a*a);
    fixed mask = step(cuv.y,0)*step(0,a);
    a = mask*(a*.5+.5);
    mask = saturate(mask+circle);
    float b =r*squish;
    b = sqrt(1-b*b);
    a = lerp(a,gradient,circle);
    a = lerp(a,lerp(-b,b,a)*.5+.5,circle);
    if (a <= 0) return 0;
    if (applyDepth)
       a = clamp(clr.x-.101 + a*.101,.00001,.96);
    return a; 
}

inline float BarsRing(float4 clr, float2 uv, float scale, float offset, float squish, float thickness, bool applyDepth) {
    float a = 0;
    float r = squish;
    float2 cuv = uv*2-1;
    fixed variation = clr.z;
    cuv*=scale;
    cuv.y-=offset;
    float sphereGradient = sqrt(1-cuv.x*cuv.x);
    fixed bars = _stripe(_mod(cuv.x+variation,.2+.2*sphereGradient),.08+.07*sphereGradient);
    fixed bars2 = _stripe(_mod(cuv.x-.55-variation,.2+.2*sphereGradient),.06+.07*sphereGradient);
    float rotOffset = thickness*sin(1-r);
    float2 offcuv=cuv-float2(0,rotOffset*.5);
    float c =offcuv.y+sphereGradient*r; 
    float cylinder = step(abs(cuv.y+sphereGradient*r),rotOffset*.5);
    float cylinder2 = step(abs(-cuv.y+sphereGradient*r),rotOffset*.5);
    a = lerp(0,lerp(.5,.0,sphereGradient/scale), bars2*cylinder2);
    a = lerp(a,lerp(.5,1,sphereGradient/scale), bars*cylinder);

    if (a <= 0) return 0;
    if (applyDepth)
         a = clamp(clr.x-.101 + a*.101,.00001,.96);
    return a;
}

inline float Ring(float4 clr, float2 uv, float scale, float offset, float squish, float thickness, bool applyDepth) {
    float a = 0;
    float r = squish;
    float2 cuv = uv*2-1;
    fixed variation = clr.z;
    cuv*=scale;
    cuv.y-=offset;
    float sphereGradient = sqrt(1-cuv.x*cuv.x);
    float rotOffset = thickness*sin(1-r);
    float2 offcuv=cuv-float2(0,rotOffset*.5);
    float c =offcuv.y+sphereGradient*r; 
    float cylinder = step(abs(cuv.y+sphereGradient*r),rotOffset*.5);
    float toptobottom = cylinder*((cuv.y+sphereGradient*r)/(rotOffset)+.5);
    float cylinder2 = step(abs(-cuv.y+sphereGradient*r),rotOffset*.5);
    a = lerp(0,lerp(.5,0,sphereGradient/scale), cylinder2);
    a = lerp(a,lerp(.5,1,sphereGradient/scale), cylinder);

    if (a <= 0) return 0;
    if (applyDepth)
         a = clamp(clr.x-.101 + a*.101,.00001,.96);
    return a;
}

inline float CandleHolder(float4 clr, float2 uv, float squish, bool applyDepth){
    if (uv.y<.5)
    uv.y+=sin((uv.x+clr.z)*11)*.01;
    clr.y = clamp(clr.y,0.0001,1);
    float a = HalfSphere(clr,uv*1.1-.05,.5,false);
    a = max(a,Ring(clr,uv,1.3,.1,squish,.4+clr.z*.4,false));
    a = max(a,Ring(clr,uv,2.5,.1,squish,.8+clr.z*.8,false));
    a = max(a,Ring(clr,uv,1.01,-.2,squish,.1+clr.z*.1,false));
    a = max(a,BarsRing(clr,uv,1.02,-.1,squish,.3+clr.z*.3,false));
    if (a <= 0) return 0;
    if (applyDepth)
        return clamp(clr.x-0.1666666667 + a*0.1666666667*2,.00001,.96);
    return a;
}

inline float CandlePole(float4 clr, float2 uv, sampler2D _NoiseTex2, bool applyDepth){
    float2 uv2 = uv*fixed2(1,1/clr.w);
    float2 cuv2 = uv2*2-1;
    float2 cuv = uv*2-1;
    fixed variation = clr.y;
    cuv.y -= abs(cuv.x)*clr.z;
    fixed noise =  tex2D(_NoiseTex2,fixed2(_mod(cuv2.y*.01+variation,.2)-abs(cuv2.x)*.03,_mod(cuv2.y*.1,.1333)-abs(cuv2.x)*.03)*.125);
    fixed noise2 =  tex2D(_NoiseTex2,abs(cuv2)*.0025+variation);
    float irregular =  noise*(1-noise2*.5);
    float a = sqrt(max(irregular*(1-uv.y*.9),.1*(1-uv.y))-(cuv.x*cuv.x)*1.3);

    if (a <= 0) return 0;
    if (applyDepth)
        return clamp(clr.x-0.0666666667 + a*0.0666666667*2,.00001,.96);
    return a;
}

inline float pack_red(float light, float depth, float palette) {
    palette = trunc(palette)*30;
    return (palette + depth + light * 90) / 255;
}

//rotate light vector near top or left edge of the screen to be parallel to that edge, to avoid casting shadows from outside of the texture
inline float2 ParallelNearEdges (float2 lightDir, float2 scrPos) {
    return lightDir*saturate(float2(scrPos.x,1.0-scrPos.y)*10);
}

inline void SelfShadow(inout float shadow, float2 lightDir, float depth, float2 scrPos, sampler2D _CameraDepthTexture, int width){
    depth*=30;
    [loop]
    for (int dist = 1; dist<=30; dist++){
        if (depth+dist>30) return;
        fixed sampledDepth = trunc(tex2D(_CameraDepthTexture,scrPos+lightDir*dist).x*30);
        shadow = max(shadow,saturate(step(dist,sampledDepth-depth)-step(dist+width,sampledDepth-depth)));
    }
}

inline float3 getNormals(float depth, float depthOffset, float2 scrPos, float clmp, float adjust, sampler2D _CameraDepthTexture, float2 _CameraDepthTexture_TexelSize ){
                depthOffset = depthOffset*.9*_DepthMult;
                fixed d1 = tex2D(_CameraDepthTexture,scrPos+fixed2(_CameraDepthTexture_TexelSize.x,0)).x-depthOffset;
                fixed d2 = tex2D(_CameraDepthTexture,scrPos+fixed2(0,_CameraDepthTexture_TexelSize.y)).x-depthOffset;
                fixed d3 = depth-depthOffset;
                float dd1 =clamp(d1-d3,-clmp,clmp);
                float dd2 =clamp(d2-d3,-clmp,clmp);
                return normalize(float3(dd1,dd2,d3*adjust));
}

sampler2D _CameraDepthTexture;
inline fixed ClipByDepth(float objectDepth, float2 scrPos){
                fixed d = tex2D(_CameraDepthTexture,scrPos).x ;
                if ((d-objectDepth)>.000002 || d == 0) discard;
                return d;
}
