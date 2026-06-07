// Procedural sky dome: day/night cycle, stars, horizon, below-horizon fog.

#ifndef GENESIS_SKY_DOME_GLSL
#define GENESIS_SKY_DOME_GLSL

const float SKY_PI = 3.14159265358979323846;

float skyHash31(vec3 p)
{
    p = fract(p * 0.3183099 + vec3(0.17, 0.31, 0.47));
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

// lightPropagationDir: direction the directional light travels (away from the sun/moon).
float skyDayFactor(vec3 lightPropagationDir, float sunIntensity)
{
    vec3 towardLight = normalize(-lightPropagationDir);
    float sunElev = towardLight.y;
    float dayFromSun = smoothstep(-0.04, 0.22, sunElev);
    float dayFromIntensity = smoothstep(0.08, 2.0, sunIntensity);
    return clamp(dayFromSun * dayFromIntensity, 0.0, 1.0);
}

vec3 skyNightZenith(vec3 viewDir)
{
    float t = clamp(viewDir.y * 0.5 + 0.5, 0.0, 1.0);
    return mix(vec3(0.01, 0.012, 0.02), vec3(0.02, 0.035, 0.07), t);
}

vec3 skyStars(vec3 viewDir, float timeSec)
{
    if (viewDir.y <= 0.01)
    {
        return vec3(0.0);
    }

    vec3 p = normalize(viewDir) * 140.0;
    vec3 cell = floor(p * 6.5);
    float h = skyHash31(cell);
    float twinkle = 0.55 + 0.45 * sin(timeSec * 1.8 + h * 52.0);
    float star = step(0.9935, h) * twinkle;
    star += step(0.9985, skyHash31(cell + vec3(17.0, 3.0, 11.0))) * twinkle * 0.65;
    return vec3(star * 0.95);
}

vec3 skyHorizonGlow(vec3 viewDir, float dayAmt, vec3 sunTint)
{
    float band = exp(-abs(viewDir.y) * 9.0);
    vec3 nightGlow = vec3(0.04, 0.05, 0.08);
    vec3 dayGlow = sunTint * 0.35 + vec3(0.12, 0.16, 0.28);
    return mix(nightGlow, dayGlow, dayAmt) * band * 0.55;
}

vec3 skyBelowHorizonFog(vec3 viewDir, float strength)
{
    if (viewDir.y >= 0.0 || strength <= 0.0)
    {
        return vec3(0.0);
    }

    float depth = smoothstep(0.0, -0.55, viewDir.y);
    vec3 fogCol = vec3(0.06, 0.07, 0.09);
    return fogCol * depth * strength;
}

vec2 skyMoonDiscUv(vec3 viewDir, vec3 towardMoon, float cosDiscEdge)
{
    vec3 vd = normalize(viewDir);
    float cosAngle = clamp(dot(vd, towardMoon), -1.0, 1.0);
    float sinTheta = sqrt(max(1.0 - cosAngle * cosAngle, 0.0));
    vec3 tangent = vd - towardMoon * cosAngle;
    float tLen2 = dot(tangent, tangent);
    if (tLen2 < 1e-10)
    {
        return vec2(0.5);
    }

    tangent *= inversesqrt(tLen2);
    vec3 moonUp = abs(towardMoon.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    vec3 moonRight = normalize(cross(moonUp, towardMoon));
    moonUp = cross(towardMoon, moonRight);
    float angularRadius = max(acos(clamp(cosDiscEdge, -1.0, 1.0)), 1e-4);
    vec2 discUv = vec2(dot(tangent, moonRight), dot(tangent, moonUp)) * (sinTheta / angularRadius);
    return discUv * 0.5 + 0.5;
}

// Procedural full moon: tight limb, mare/crater variation, faint outer penumbra only.
vec3 skyMoonDiscShading(vec3 viewDir, vec3 lightPropagationDir, float cosDiscEdge)
{
    vec3 towardMoon = normalize(lightPropagationDir);
    float cosAngle = dot(normalize(viewDir), towardMoon);
    float edge = clamp(cosDiscEdge, 0.94, 0.99998);
    float penumbra = (1.0 - edge) * 2.5;
    float outerCos = clamp(edge - penumbra, -1.0, 1.0);
    float disc = smoothstep(outerCos, edge, cosAngle);
    if (disc <= 1e-4)
    {
        return vec3(0.0);
    }

    vec2 mUv = skyMoonDiscUv(viewDir, towardMoon, edge);
    vec3 samplePos = vec3(mUv * 8.0, 0.0);
    float n0 = skyHash31(samplePos);
    float n1 = skyHash31(samplePos * 2.13 + vec3(1.7, 4.1, 0.0));
    float n2 = skyHash31(samplePos * 4.37 + vec3(9.0, 2.3, 0.0));
    float mare = smoothstep(0.38, 0.62, n0 * 0.55 + n1 * 0.3 + n2 * 0.15);
    float crater = smoothstep(0.82, 0.94, n1) * smoothstep(0.15, 0.45, n2);
    vec3 highland = vec3(0.78, 0.80, 0.84);
    vec3 lowland = vec3(0.58, 0.60, 0.66);
    vec3 moonCol = mix(highland, lowland, mare * 0.9);
    moonCol = mix(moonCol, vec3(0.48, 0.50, 0.55), crater * 0.55);

    float radial = length(mUv - 0.5) * 2.0;
    moonCol *= 1.0 - smoothstep(0.55, 1.0, radial) * 0.35;
    moonCol *= 0.65 + 0.35 * disc;
    return moonCol * disc;
}

// Angular bloom around the sun disc; aligned to light direction (same bearing as the sun billboard).
float skySunDiscBloom(vec3 viewDir, vec3 lightPropagationDir, float cosDiscEdge, float bloomRadiusUv, float strength)
{
    if (strength <= 0.0)
    {
        return 0.0;
    }

    float edge = max(cosDiscEdge, 0.85);
    vec3 towardSun = normalize(-lightPropagationDir);
    float cosAngle = dot(normalize(viewDir), towardSun);
    float spread = (1.0 - edge) * mix(2.0, 6.0, clamp(bloomRadiusUv * 36.0, 0.0, 1.0));
    float outerCos = clamp(edge - spread, -1.0, 1.0);
    return smoothstep(outerCos, edge, cosAngle) * strength;
}

// Map a world-space view direction to sky-view LUT UV (matches atmo_skyview.frag).
vec2 skyViewLutUv(vec3 viewDir)
{
    float viewZenith = acos(clamp(viewDir.y, -1.0, 1.0)) / SKY_PI;
    float azimuth = atan(viewDir.x, viewDir.z) / SKY_PI;
    return vec2(azimuth * 0.5 + 0.5, viewZenith);
}

vec3 skySoftKnee(vec3 x, float knee)
{
    return x / (x + vec3(knee));
}

#endif // GENESIS_SKY_DOME_GLSL
