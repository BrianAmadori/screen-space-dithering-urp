#ifndef __GRAIN_UTILS__
#define __GRAIN_UTILS__

// Implementation based on Timothy Lottes' "Large Grain"
// Reference code: https://www.shadertoy.com/view/4sSXDW
// Other article of interest: http://devlog-martinsh.blogspot.fr/2013/05/image-imperfections-and-film-grain-post.html
float Noise(float2 n, float x)
{
    n += x;
    return frac(sin(dot(n.xy, float2(12.9898, 78.233))) * 43758.5453);
}

float Step1(float2 uv, float n)
{
    float b = 2.0, c = -12.0;
    return (1.0 / (4.0 + b * 4.0 + abs(c))) * (
        Noise(uv + float2(-1.0, -1.0), n) +
        Noise(uv + float2( 0.0, -1.0), n) * b +
        Noise(uv + float2( 1.0, -1.0), n) +
        Noise(uv + float2(-1.0,  0.0), n) * b +
        Noise(uv + float2( 0.0,  0.0), n) * c +
        Noise(uv + float2( 1.0,  0.0), n) * b +
        Noise(uv + float2(-1.0,  1.0), n) +
        Noise(uv + float2( 0.0,  1.0), n) * b +
        Noise(uv + float2( 1.0,  1.0), n)
    );
}

float Step2(float2 uv, float n)
{
    float b = 2.0, c = 4.0;
    return (1.0 / (4.0 + b * 4.0 + abs(c))) * (
        Step1(uv + float2(-1.0, -1.0), n) +
        Step1(uv + float2( 0.0, -1.0), n) * b +
        Step1(uv + float2( 1.0, -1.0), n) +
        Step1(uv + float2(-1.0,  0.0), n) * b +
        Step1(uv + float2( 0.0,  0.0), n) * c +
        Step1(uv + float2( 1.0,  0.0), n) * b +
        Step1(uv + float2(-1.0,  1.0), n) +
        Step1(uv + float2( 0.0,  1.0), n) * b +
        Step1(uv + float2( 1.0,  1.0), n)
    );
}

float Step3BW(float2 uv, float phase)
{
    return Step2(uv, frac(phase));
}

float3 Step3(float2 uv, float phase)
{
    float a = Step2(uv, 0.07 * frac(phase));
    float b = Step2(uv, 0.11 * frac(phase));
    float c = Step2(uv, 0.13 * frac(phase));
    return float3(a, b, c);
}

#endif