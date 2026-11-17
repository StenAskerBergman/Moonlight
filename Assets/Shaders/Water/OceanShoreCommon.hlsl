#ifndef MOONLIGHT_OCEAN_SHORE_COMMON_INCLUDED
#define MOONLIGHT_OCEAN_SHORE_COMMON_INCLUDED

// Shared contract between the ocean surface (Moonlight/Shore Ocean) and the terrain
// (Moonlight/Terrain Splat (Normal Mapped)).
//
// WHY THIS EXISTS. The ocean and the islands used to have no idea the other existed: the sea
// was one rigid sheet bobbing +/-0.1 everywhere at once, the beach was built for a sea that did
// not move, and nothing knew where any coast was. Every wave timing below is evaluated from the
// same globals in both shaders, so a breaker that rolls in on the water arrives at the waterline
// at the same instant the swash runs up the sand - per island, per stretch of coast.
//
// All globals are written by OceanSurfaceBuilder (C#) after each map generation / reload.
// While _OceanShoreEnabled is 0 every function here degrades to "open sea, no coast".

TEXTURE2D(_OceanShoreField);
SAMPLER(sampler_OceanShoreField);

// R  signed distance to the shoreline in metres (+ seaward, - inland)
// G  water depth below the sea datum in metres (negative on land)
// BA unit vector pointing seaward (away from the nearest coast)
float4 _OceanShoreFieldRect;   // xy = world min XZ, zw = 1 / world size XZ
float4 _OceanDenseRect;        // xy = min XZ, zw = max XZ of the displaced ocean surface
float4 _OceanShoreWave;        // x height (m), y wavelength (m), z period (s), w reach offshore (m)
float4 _OceanSwash;            // x run-up height (m), y wet-sand height (m), z swash foam, w inland reach (m)
float4 _OceanSurfaceParams;    // x distance over which waves fade out at the surface's outer edge (m), yzw unused
float  _OceanSeaLevel;
float  _OceanShoreEnabled;

#define OCEAN_TWO_PI 6.28318530718

struct OceanShoreSample
{
    float distance;   // metres to the coast, + seaward
    float depth;      // metres of water under the datum
    float2 seaward;   // unit, points away from the coast (zero far from any coast)
    float valid;      // 1 inside the baked field
};

OceanShoreSample SampleOceanShore(float2 positionXZ)
{
    OceanShoreSample s;
    s.distance = 10000.0;
    s.depth = 1000.0;
    s.seaward = float2(0.0, 0.0);
    s.valid = 0.0;

    if (_OceanShoreEnabled < 0.5) return s;

    float2 uv = (positionXZ - _OceanShoreFieldRect.xy) * _OceanShoreFieldRect.zw;
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return s;

    float4 f = SAMPLE_TEXTURE2D_LOD(_OceanShoreField, sampler_OceanShoreField, uv, 0);
    s.distance = f.r;
    s.depth = f.g;
    float len = length(f.ba);
    s.seaward = len > 1e-3 ? f.ba / len : float2(0.0, 0.0);
    s.valid = 1.0;
    return s;
}

// 1 well inside the displaced ocean surface, falling to exactly 0 at its outer edge, so the
// tessellated surface and the flat horizon skirt beyond it always meet at the same height.
float OceanDenseFade(float2 positionXZ)
{
    float2 lo = positionXZ - _OceanDenseRect.xy;
    float2 hi = _OceanDenseRect.zw - positionXZ;
    float inside = min(min(lo.x, lo.y), min(hi.x, hi.y));
    return saturate(inside / max(_OceanSurfaceParams.x, 1.0));
}

// Phase of the shore-bound wave train. Constant phase moves toward the coast over time
// (distance falls as time rises). The slow positional drift stops every set arriving as a
// perfectly concentric ring around each island.
float OceanShorePhase(float distance, float2 positionXZ)
{
    float k = OCEAN_TWO_PI / max(_OceanShoreWave.y, 0.5);
    float w = OCEAN_TWO_PI / max(_OceanShoreWave.z, 0.5);
    float drift = sin(positionXZ.x * 0.071 + positionXZ.y * 0.043) * 1.3
                + sin(positionXZ.x * -0.037 + positionXZ.y * 0.089 + 1.7) * 1.1;
    return distance * k + _Time.y * w + drift;
}

// Wave-train envelope against distance from the coast: absent offshore, strongest a few metres
// out where swell shoals into breakers, and deliberately small at the waterline so the
// GEOMETRIC waterline barely moves. The visible wash on the sand is the swash, below.
float OceanShoreEnvelope(float distance)
{
    float reach = max(_OceanShoreWave.w, 1.0);
    float offshore = 1.0 - smoothstep(reach * 0.45, reach, distance);
    float waterline = lerp(0.2, 1.0, smoothstep(0.0, 3.0, distance));
    return offshore * waterline;
}

// Sharpened crest: narrow peaks, broad troughs. Mean ~0 so the datum does not drift.
float OceanShoreShape(float phase)
{
    float c = 0.5 + 0.5 * cos(phase);
    return c * c - 0.375;
}

// Height of the shore wave and its derivative along the seaward distance axis.
void OceanShoreWave(OceanShoreSample s, float2 positionXZ, out float height, out float dHeightDDistance)
{
    height = 0.0;
    dHeightDDistance = 0.0;
    if (s.valid < 0.5) return;

    const float eps = 0.25;
    float a = _OceanShoreWave.x;
    float h0 = a * OceanShoreEnvelope(s.distance) * OceanShoreShape(OceanShorePhase(s.distance, positionXZ));
    float hp = a * OceanShoreEnvelope(s.distance + eps) * OceanShoreShape(OceanShorePhase(s.distance + eps, positionXZ));
    float hm = a * OceanShoreEnvelope(s.distance - eps) * OceanShoreShape(OceanShorePhase(s.distance - eps, positionXZ));
    height = h0;
    dHeightDDistance = (hp - hm) / (2.0 * eps);
}

// Foam left by the breakers: the crest whitens as it shoals and leaves a trail behind it.
float OceanShoreBreakerFoam(OceanShoreSample s, float2 positionXZ)
{
    if (s.valid < 0.5) return 0.0;
    float reach = max(_OceanShoreWave.w, 1.0);
    float breakZone = 1.0 - smoothstep(1.0, reach * 0.55, s.distance);
    float phase = OceanShorePhase(s.distance, positionXZ);
    float crest = smoothstep(0.72, 0.98, 0.5 + 0.5 * cos(phase));
    float trail = smoothstep(0.55, 1.0, 0.5 + 0.5 * cos(phase - 0.9)) * 0.55;
    return saturate(max(crest, trail)) * breakZone * step(-0.5, s.distance);
}

// 0..1 progress of the swash up the beach, peaking just after a breaker reaches the waterline.
float OceanRunup(float2 positionXZ)
{
    float phase0 = OceanShorePhase(0.0, positionXZ);
    float c = saturate(0.5 + 0.5 * cos(phase0 - 0.7));
    return c * sqrt(c);
}

float OceanHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float OceanValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = OceanHash21(i);
    float b = OceanHash21(i + float2(1.0, 0.0));
    float c = OceanHash21(i + float2(0.0, 1.0));
    float d = OceanHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Terrain side of the seam: how wet a point of ground is and how much swash foam lies on it.
// The water mesh cannot run up the sand without moving the geometric waterline (which is what
// made every beach crawl), so the wash is drawn ON the sand, in phase with the breakers.
void OceanTerrainSwash(float3 positionWS, out float wetness, out float foam)
{
    wetness = 0.0;
    foam = 0.0;
    if (_OceanShoreEnabled < 0.5) return;

    OceanShoreSample s = SampleOceanShore(positionWS.xz);
    if (s.valid < 0.5) return;

    float inlandReach = max(_OceanSwash.w, 0.5);
    float coast = smoothstep(-inlandReach, -inlandReach * 0.5, s.distance);
    if (coast <= 0.0) return;

    float h = positionWS.y - _OceanSeaLevel;
    float runupMax = max(_OceanSwash.x, 0.0);
    float runup = OceanRunup(positionWS.xz) * runupMax;

    // The sheet of water currently covering the sand, and the damp band it keeps re-wetting.
    float sheet = 1.0 - smoothstep(runup - 0.01, runup + 0.015, h);
    float damp = 1.0 - smoothstep(runupMax * 0.5, runupMax + max(_OceanSwash.y, 0.01), h);
    wetness = saturate(max(sheet, damp * 0.7)) * coast;

    float t = _Time.y;
    float n = OceanValueNoise(positionWS.xz * 3.1 + float2(t * 0.15, -t * 0.05)) * 0.6
            + OceanValueNoise(positionWS.xz * 7.3 - float2(t * 0.2, t * 0.12)) * 0.4;
    float edge = 1.0 - saturate(abs(h - runup) / 0.035);
    float lace = edge * smoothstep(0.32, 0.62, n)
               + sheet * smoothstep(0.6, 0.8, n) * 0.45 * (runup / max(runupMax, 1e-3));
    foam = saturate(lace) * coast * _OceanSwash.z * smoothstep(-0.06, 0.0, h);
}

#endif
