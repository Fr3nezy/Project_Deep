// Shared surface code for Deeplonauts environment shaders (Shader Graph Custom Function, File mode).
// SG_Terrain_Lit calls TerrainSurface_float, SG_Rock_Blend calls RockSurface_float.
// The ground layer that rocks blend into is sampled with exactly the same code and parameters as the
// terrain, so where a rock meets the seabed both show the same mud pixel.
#ifndef DEEPLONAUTS_ENV_COMMON_INCLUDED
#define DEEPLONAUTS_ENV_COMMON_INCLUDED

#define ENV_HASH21(p) frac(sin(mul(float2x2(127.1, 311.7, 269.5, 183.3), p)) * 43758.5453)
#define ENV_HASH11(p) frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453)

// Ground layer published by GroundMaterialPublisher (global shader properties)
TEXTURE2D(_GroundBaseMap);  SAMPLER(sampler_GroundBaseMap);
TEXTURE2D(_GroundBumpMap);  SAMPLER(sampler_GroundBumpMap);
TEXTURE2D(_GroundMSMap);    SAMPLER(sampler_GroundMSMap);
float4 _GroundTint;          // rgb tint
float4 _GroundUV;            // x: tile size (m), yz: offset, w: stochastic on/off
float4 _GroundColorParams;   // x: brightness, y: saturation, z: contrast, w: normal strength
float4 _GroundSurfParams;    // x: smoothness min, y: smoothness max, z: stochastic sharpness, w: macro strength
float4 _GroundMacro;         // x: macro scale (m)

// Neighbour rock proxies published per renderer by GroundBlendProbe (MaterialPropertyBlock)
#define ENV_MAX_ROCK_NEIGHBORS 4
float4x4 _RockProxyM[ENV_MAX_ROCK_NEIGHBORS]; // world -> proxy space (rotation + translation)
float4 _RockProxyR[ENV_MAX_ROCK_NEIGHBORS];   // xyz: half extents (m), w: shape (1 ellipsoid, 2 + r rounded box of radius r)
float4 _RockNeighborParams;                   // x: blend width (m), y: normal blend, z: sediment, w: neighbour count

struct EnvStochastic
{
    float2 uv0;
    float2 uv1;
    float2 uv2;
    float3 w;
    int count;
};

// Heitz & Neyret triangle-grid stochastic tiling: 3 randomly offset samples blended by barycentric weights
EnvStochastic EnvStochasticSetup(float2 uv, bool enabled, float sharpness)
{
    EnvStochastic s;
    s.uv0 = uv; s.uv1 = uv; s.uv2 = uv;
    s.w = float3(1, 0, 0);
    s.count = 1;
    if (enabled)
    {
        float2 skew = mul(float2x2(1.0, 0.0, -0.57735027, 1.15470054), uv * 3.4641016);
        float2 id = floor(skew);
        float3 b = float3(frac(skew), 0);
        b.z = 1.0 - b.x - b.y;
        float2 v1, v2, v3;
        if (b.z > 0) { s.w = float3(b.z, b.y, b.x); v1 = id; v2 = id + float2(0, 1); v3 = id + float2(1, 0); }
        else { s.w = float3(-b.z, 1.0 - b.y, 1.0 - b.x); v1 = id + float2(1, 1); v2 = id + float2(1, 0); v3 = id + float2(0, 1); }
        s.uv0 = uv + ENV_HASH21(v1);
        s.uv1 = uv + ENV_HASH21(v2);
        s.uv2 = uv + ENV_HASH21(v3);
        s.w = pow(max(s.w, 1e-4), sharpness);
        s.w /= dot(s.w, 1);
        s.count = 3;
    }
    return s;
}

float4 EnvSample(TEXTURE2D_PARAM(tex, samp), EnvStochastic s, float2 dx, float2 dy)
{
    float4 r = SAMPLE_TEXTURE2D_GRAD(tex, samp, s.uv0, dx, dy) * s.w.x;
    if (s.count > 1)
    {
        r += SAMPLE_TEXTURE2D_GRAD(tex, samp, s.uv1, dx, dy) * s.w.y;
        r += SAMPLE_TEXTURE2D_GRAD(tex, samp, s.uv2, dx, dy) * s.w.z;
    }
    return r;
}

float3 EnvSampleNormal(TEXTURE2D_PARAM(tex, samp), EnvStochastic s, float2 dx, float2 dy, float strength)
{
    float3 n = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, s.uv0, dx, dy), strength) * s.w.x;
    if (s.count > 1)
    {
        n += UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, s.uv1, dx, dy), strength) * s.w.y;
        n += UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(tex, samp, s.uv2, dx, dy), strength) * s.w.z;
    }
    return normalize(n);
}

float EnvValueNoise(float2 p)
{
    float2 ip = floor(p);
    float2 fp = frac(p);
    fp = fp * fp * (3 - 2 * fp);
    return lerp(lerp(ENV_HASH11(ip), ENV_HASH11(ip + float2(1, 0)), fp.x),
                lerp(ENV_HASH11(ip + float2(0, 1)), ENV_HASH11(ip + float2(1, 1)), fp.x), fp.y);
}

// Two octaves of world-space value noise in [0,1]
float EnvMacroNoise(float2 xz, float scale)
{
    float2 mp = xz / max(scale, 0.01);
    return EnvValueNoise(mp) * 0.65 + EnvValueNoise(mp * 2.7 + 17.3) * 0.35;
}

float3 EnvGrade(float3 col, float3 tint, float brightness, float saturation, float contrast)
{
    col *= tint * brightness;
    float lum = dot(col, float3(0.2126, 0.7152, 0.0722));
    col = lerp(lum.xxx, col, saturation);
    return max(0, (col - 0.18) * contrast + 0.18);
}

// ---------------------------------------------------------------------------------------------
// Terrain: world (or mesh) UV, stochastic tiling, macro variation
// ---------------------------------------------------------------------------------------------
void TerrainSurface_float(
    UnityTexture2D BaseMap, float4 Tint, float Brightness, float Saturation, float Contrast,
    UnityTexture2D NormalMap, float NormalStrength, UnityTexture2D MetallicSmoothness,
    float SmoothnessMin, float SmoothnessMax, UnityTexture2D AOMap, float AOStrength,
    float2 Tiling, float2 Offset, bool UseWorldUV, float WorldTileSize,
    bool StochasticTiling, float StochasticSharpness, float MacroStrength, float MacroScale,
    float4 UV, float3 PositionWS,
    out float3 BaseColor, out float3 NormalTS, out float Metallic, out float Smoothness, out float Occlusion)
{
    float2 uv = UseWorldUV ? PositionWS.xz / max(WorldTileSize, 0.001) + Offset : UV.xy * Tiling + Offset;
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    EnvStochastic s = EnvStochasticSetup(uv, StochasticTiling, StochasticSharpness);

    float3 col = EnvSample(TEXTURE2D_ARGS(BaseMap.tex, BaseMap.samplerstate), s, dx, dy).rgb;
    col *= lerp(1 - MacroStrength, 1 + MacroStrength, EnvMacroNoise(PositionWS.xz, MacroScale));
    BaseColor = EnvGrade(col, Tint.rgb, Brightness, Saturation, Contrast);

    NormalTS = EnvSampleNormal(TEXTURE2D_ARGS(NormalMap.tex, NormalMap.samplerstate), s, dx, dy, NormalStrength);
    float4 ms = EnvSample(TEXTURE2D_ARGS(MetallicSmoothness.tex, MetallicSmoothness.samplerstate), s, dx, dy);
    Metallic = ms.r;
    Smoothness = lerp(SmoothnessMin, SmoothnessMax, ms.a);
    float ao = EnvSample(TEXTURE2D_ARGS(AOMap.tex, AOMap.samplerstate), s, dx, dy).g;
    Occlusion = lerp(1, ao, AOStrength);
}

// Ground layer from the published globals, projected on world XZ. Normal returned in world space.
void EnvSampleGround(float3 positionWS, out float3 color, out float3 normalWS, out float smoothness)
{
    float2 uv = positionWS.xz / max(_GroundUV.x, 0.001) + _GroundUV.yz;
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);
    EnvStochastic s = EnvStochasticSetup(uv, _GroundUV.w > 0.5, _GroundSurfParams.z);

    float3 col = EnvSample(TEXTURE2D_ARGS(_GroundBaseMap, sampler_GroundBaseMap), s, dx, dy).rgb;
    col *= lerp(1 - _GroundSurfParams.w, 1 + _GroundSurfParams.w, EnvMacroNoise(positionWS.xz, _GroundMacro.x));
    color = EnvGrade(col, _GroundTint.rgb, _GroundColorParams.x, _GroundColorParams.y, _GroundColorParams.z);

    float3 n = EnvSampleNormal(TEXTURE2D_ARGS(_GroundBumpMap, sampler_GroundBumpMap), s, dx, dy, _GroundColorParams.w);
    normalWS = normalize(float3(n.x, n.z, n.y)); // XZ projection: tangent = +X, bitangent = +Z, normal = +Y

    float4 ms = EnvSample(TEXTURE2D_ARGS(_GroundMSMap, sampler_GroundMSMap), s, dx, dy);
    smoothness = lerp(_GroundSurfParams.x, _GroundSurfParams.y, ms.a);
}

// Ground layer on arbitrary surfaces: the top projection is exactly EnvSampleGround (matches the terrain),
// side projections (single sample, no stochastic) avoid the vertical streaks of a pure XZ projection.
void EnvSampleGroundTriplanar(float3 positionWS, float3 geomNormalWS, out float3 color, out float3 normalWS, out float smoothness)
{
    float3 w = pow(abs(geomNormalWS), 4);
    w /= max(dot(w, 1), 1e-5);

    float3 topCol, topN;
    float topSmooth;
    EnvSampleGround(positionWS, topCol, topN, topSmooth);

    float tile = max(_GroundUV.x, 0.001);
    float2 uvX = positionWS.zy / tile + _GroundUV.yz;   // faces looking along X
    float2 uvZ = positionWS.xy / tile + _GroundUV.yz;   // faces looking along Z

    float3 colX = SAMPLE_TEXTURE2D(_GroundBaseMap, sampler_GroundBaseMap, uvX).rgb;
    float3 colZ = SAMPLE_TEXTURE2D(_GroundBaseMap, sampler_GroundBaseMap, uvZ).rgb;
    float macro = lerp(1 - _GroundSurfParams.w, 1 + _GroundSurfParams.w, EnvMacroNoise(positionWS.xz, _GroundMacro.x));
    colX = EnvGrade(colX * macro, _GroundTint.rgb, _GroundColorParams.x, _GroundColorParams.y, _GroundColorParams.z);
    colZ = EnvGrade(colZ * macro, _GroundTint.rgb, _GroundColorParams.x, _GroundColorParams.y, _GroundColorParams.z);

    float3 nX = UnpackNormalScale(SAMPLE_TEXTURE2D(_GroundBumpMap, sampler_GroundBumpMap, uvX), _GroundColorParams.w);
    float3 nZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_GroundBumpMap, sampler_GroundBumpMap, uvZ), _GroundColorParams.w);
    // tangent-space to world for each side projection (sign keeps the bump facing outwards)
    float3 nXW = float3(nX.z * sign(geomNormalWS.x), nX.y, nX.x);
    float3 nZW = float3(nZ.x, nZ.y, nZ.z * sign(geomNormalWS.z));

    float smX = lerp(_GroundSurfParams.x, _GroundSurfParams.y, SAMPLE_TEXTURE2D(_GroundMSMap, sampler_GroundMSMap, uvX).a);
    float smZ = lerp(_GroundSurfParams.x, _GroundSurfParams.y, SAMPLE_TEXTURE2D(_GroundMSMap, sampler_GroundMSMap, uvZ).a);

    color = colX * w.x + topCol * w.y + colZ * w.z;
    normalWS = normalize(nXW * w.x + topN * w.y + nZW * w.z);
    smoothness = smX * w.x + topSmooth * w.y + smZ * w.z;
}

// ---------------------------------------------------------------------------------------------
// Neighbour proxies: analytic SDFs standing in for the rocks this one touches
// ---------------------------------------------------------------------------------------------

// Distance (approximate, iq) and outward gradient of an ellipsoid with half extents r
float EnvSdEllipsoid(float3 p, float3 r, out float3 grad)
{
    r = max(r, 1e-3);
    float3 pr = p / r;
    float3 prr = p / (r * r);
    float k0 = length(pr);
    float k1 = max(length(prr), 1e-5);
    grad = prr / k1;
    return k0 * (k0 - 1.0) / k1;
}

// Exact distance and gradient of a box with half extents r and edges rounded by radius round
float EnvSdRoundBox(float3 p, float3 r, float round, out float3 grad)
{
    float3 q = abs(p) - max(r - round, 1e-3);
    float3 s = float3(p.x >= 0 ? 1 : -1, p.y >= 0 ? 1 : -1, p.z >= 0 ? 1 : -1);
    float inner = max(q.x, max(q.y, q.z));
    float3 outside = max(q, 0);
    float3 g = inner > 0 ? outside
             : (q.x >= q.y && q.x >= q.z ? float3(1, 0, 0) : (q.y >= q.z ? float3(0, 1, 0) : float3(0, 0, 1)));
    grad = normalize(g * s);
    return length(outside) + min(inner, 0) - round;
}

// Nearest neighbour proxy surface: signed distance (m) and world-space gradient. False when there are none.
bool EnvNearestRockProxy(float3 positionWS, out float dist, out float3 gradWS)
{
    dist = 1e5;
    gradWS = float3(0, 1, 0);
    int count = (int)_RockNeighborParams.w;
    [unroll]
    for (int i = 0; i < ENV_MAX_ROCK_NEIGHBORS; i++)
    {
        if (i < count)
        {
            float3 p = mul(_RockProxyM[i], float4(positionWS, 1)).xyz;
            float4 shape = _RockProxyR[i];
            float3 g;
            float d = shape.w < 2 ? EnvSdEllipsoid(p, shape.xyz, g) : EnvSdRoundBox(p, shape.xyz, frac(shape.w), g);
            if (d < dist)
            {
                dist = d;
                gradWS = mul(g, (float3x3)_RockProxyM[i]); // proxy -> world: transpose of the rotation
            }
        }
    }
    return count > 0;
}

// ---------------------------------------------------------------------------------------------
// Rock: Painter maps on mesh UV, blended into the ground layer by three masks
//   contact  - distance from the per-instance ground plane (GroundBlendProbe), noisy edge
//   top      - world-up facing surfaces get a sediment drape (rotation independent)
//   cavity   - baked AO crevices fill with sediment
// and by the neighbour proxies: near another rock the normal bends towards the one both surfaces share
// at the intersection (own geometric normal + neighbour gradient) and sediment settles in the joint.
// ---------------------------------------------------------------------------------------------
void RockSurface_float(
    UnityTexture2D BaseMap, float4 Tint, float Brightness, float Saturation, float Contrast,
    UnityTexture2D NormalMap, float NormalStrength, UnityTexture2D MetallicSmoothness,
    float SmoothnessMin, float SmoothnessMax, UnityTexture2D AOMap, float AOStrength,
    float ContactHeight, float ContactNoise, float ContactNoiseScale, float ContactNormalBlend,
    float SedimentAmount, float SedimentSoftness, float SedimentNoise, float CavitySediment,
    float4 GroundPlanePoint, float4 GroundPlaneNormal,
    float4 UV, float3 PositionWS, float3 NormalWS, float3 TangentWS, float3 BitangentWS,
    out float3 BaseColor, out float3 NormalTS, out float Metallic, out float Smoothness, out float Occlusion)
{
    float2 uv = UV.xy;
    float3 rockCol = EnvGrade(SAMPLE_TEXTURE2D(BaseMap.tex, BaseMap.samplerstate, uv).rgb, Tint.rgb, Brightness, Saturation, Contrast);
    float3 rockNTS = UnpackNormalScale(SAMPLE_TEXTURE2D(NormalMap.tex, NormalMap.samplerstate, uv), NormalStrength);
    float4 rockMS = SAMPLE_TEXTURE2D(MetallicSmoothness.tex, MetallicSmoothness.samplerstate, uv);
    float rockAO = SAMPLE_TEXTURE2D(AOMap.tex, AOMap.samplerstate, uv).g;

    float3x3 tbn = float3x3(normalize(TangentWS), normalize(BitangentWS), normalize(NormalWS));
    float3 rockNWS = normalize(mul(rockNTS, tbn));

    // breakup noise, sampled on two planes so vertical faces are not streaked
    float noiseScale = max(ContactNoiseScale, 0.01);
    float noise = (EnvValueNoise(PositionWS.xz / noiseScale) + EnvValueNoise(PositionWS.xy / noiseScale + 31.7)) * 0.5;

    // contact with the ground plane (w = 1 when GroundBlendProbe found the ground)
    float contact = 0;
    if (GroundPlaneNormal.w > 0.5)
    {
        float d = dot(PositionWS - GroundPlanePoint.xyz, normalize(GroundPlaneNormal.xyz));
        d += (noise - 0.5) * ContactNoise * ContactHeight;
        contact = 1 - smoothstep(0, max(ContactHeight, 0.001), d);
    }

    // sediment drape on up-facing surfaces
    float threshold = 1 - SedimentAmount;
    float up = rockNWS.y + (noise - 0.5) * SedimentNoise;
    float top = SedimentAmount > 0 ? smoothstep(threshold - SedimentSoftness, threshold + SedimentSoftness, up) : 0;

    // sediment settled in crevices
    float cavity = saturate((1 - rockAO) * CavitySediment * 2);

    // joint with the nearest neighbour rock
    float neighbor = 0;
    float3 sharedNWS = normalize(NormalWS);
    float neighborDist;
    float3 neighborGrad;
    if (EnvNearestRockProxy(PositionWS, neighborDist, neighborGrad))
    {
        float width = max(_RockNeighborParams.x, 0.001);
        neighborDist += (noise - 0.5) * ContactNoise * width;
        neighbor = 1 - smoothstep(0, width, neighborDist);
        sharedNWS = normalize(sharedNWS + normalize(neighborGrad));
    }

    float mask = saturate(max(max(contact, neighbor * _RockNeighborParams.z), max(top, cavity)));

    float3 groundCol, groundNWS;
    float groundSmooth;
    EnvSampleGroundTriplanar(PositionWS, normalize(NormalWS), groundCol, groundNWS, groundSmooth);

    BaseColor = lerp(rockCol, groundCol, mask);

    float normalBlend = max(contact * ContactNormalBlend, max(top * 0.5, cavity * 0.3));
    float3 nWS = normalize(lerp(rockNWS, groundNWS, normalBlend));
    nWS = normalize(lerp(nWS, sharedNWS, neighbor * _RockNeighborParams.y));
    NormalTS = normalize(mul(tbn, nWS));

    Metallic = lerp(rockMS.r, 0, mask);
    Smoothness = lerp(lerp(SmoothnessMin, SmoothnessMax, rockMS.a), groundSmooth, mask);
    Occlusion = lerp(lerp(1, rockAO, AOStrength), 1, mask);
}

#endif
