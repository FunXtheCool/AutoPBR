using System.Text;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Self-contained procedural sky (day/night, stars, horizon) with no LUT texture dependency.</summary>
internal sealed class GlProceduralSkyProgram : IDisposable
{
    private const string Vert330 = """
#version 330 core
layout(location = 0) in vec2 aPos;
out vec2 vUv;
void main()
{
    vUv = aPos * 0.5 + 0.5;
    gl_Position = vec4(aPos, 0.0, 1.0);
}
""";

    private const string Frag330 = """
#version 330 core
in vec2 vUv;
uniform mat4 uInvViewProj;
uniform vec3 uCameraPos;
uniform vec3 uLightDir;
uniform float uSunIntensity;
uniform float uSkyExposure;
uniform float uRenderTime;
uniform float uTurbidity;
uniform float uHorizonFalloff;
uniform float uHorizonFogStrength;
uniform float uSunDiscStrength;
uniform float uSunCosDiscEdge;
uniform float uMoonCosDiscEdge;
uniform float uViewportAspect;
uniform float uSunDiscRadiusUv;
out vec4 FragColor;

const float SKY_PI = 3.14159265358979323846;

vec3 worldRayDir(vec2 uv, mat4 invViewProj, vec3 cameraPos)
{
    vec2 ndc = vec2(uv.x * 2.0 - 1.0, uv.y * 2.0 - 1.0);
    vec4 worldH = invViewProj * vec4(ndc, 1.0, 1.0);
    vec3 farPt = worldH.xyz / max(worldH.w, 1e-6);
    vec3 rd = farPt - cameraPos;
    float len2 = dot(rd, rd);
    if (len2 < 1e-12)
    {
        return vec3(0.0, 1.0, 0.0);
    }
    return rd * inversesqrt(len2);
}

float dayFactor(vec3 lightPropagationDir, float sunIntensity)
{
    vec3 towardLight = normalize(-lightPropagationDir);
    float sunElev = towardLight.y;
    float dayFromSun = smoothstep(-0.04, 0.22, sunElev);
    float dayFromIntensity = smoothstep(0.08, 2.0, sunIntensity);
    return clamp(dayFromSun * dayFromIntensity, 0.0, 1.0);
}

float hash31(vec3 p)
{
    p = fract(p * 0.3183099 + vec3(0.17, 0.31, 0.47));
    p += dot(p, p.yzx + vec3(33.33));
    return fract((p.x + p.y) * p.z);
}

vec3 stars(vec3 viewDir, float timeSec)
{
    if (viewDir.y <= 0.01)
    {
        return vec3(0.0);
    }

    vec3 p = normalize(viewDir) * 140.0;
    vec3 cell = floor(p * 6.5);
    float h = hash31(cell);
    float twinkle = 0.55 + 0.45 * sin(timeSec * 1.8 + h * 52.0);
    float star = step(0.9935, h) * twinkle;
    star += step(0.9985, hash31(cell + vec3(17.0, 3.0, 11.0))) * twinkle * 0.65;
    return vec3(star * 0.95);
}

vec3 nightZenith(vec3 viewDir)
{
    float t = clamp(viewDir.y * 0.5 + 0.5, 0.0, 1.0);
    return mix(vec3(0.01, 0.012, 0.02), vec3(0.02, 0.035, 0.07), t);
}

float rayleighPhase(float cosTheta)
{
    return (3.0 / (16.0 * SKY_PI)) * (1.0 + cosTheta * cosTheta);
}

float miePhase(float cosTheta)
{
    const float g = 0.76;
    float gg = g * g;
    float denom = pow(max(1.0 + gg - 2.0 * g * cosTheta, 1e-3), 1.5);
    return (1.0 - gg) / (4.0 * SKY_PI * denom);
}

vec3 proceduralSky(vec3 viewDir, vec3 lightPropagationDir, float sunIntensity, float turbidity, float horizonFalloff)
{
    float mu = clamp(viewDir.y, -1.0, 1.0);
    float cosSun = dot(viewDir, normalize(-lightPropagationDir));
    float rayleigh = rayleighPhase(cosSun);
    float mie = miePhase(cosSun) * clamp(turbidity * 0.35, 0.2, 4.0);
    vec3 sunCol = vec3(1.0, 0.97, 0.92) * sunIntensity;
    float horizon = pow(1.0 - clamp(mu, 0.0, 1.0), max(horizonFalloff, 0.1));
    vec3 baseSky = mix(vec3(0.02, 0.04, 0.08), vec3(0.24, 0.43, 0.78), clamp(mu * 0.5 + 0.5, 0.0, 1.0));
    vec3 betaR = vec3(5.5e-6, 13.0e-6, 22.4e-6);
    vec3 scatter = (betaR * rayleigh * 120000.0 + vec3(mie * 0.045)) * sunCol;
    vec3 col = baseSky * mix(0.9, 0.45, horizon) + scatter;
    float dayAmt = dayFactor(lightPropagationDir, sunIntensity);
    col = mix(nightZenith(viewDir), col, dayAmt);
    return max(col, vec3(0.0));
}

vec3 horizonGlow(vec3 viewDir, float dayAmt)
{
    float band = exp(-abs(viewDir.y) * 9.0);
    vec3 sunTint = vec3(1.0, 0.93, 0.74);
    vec3 nightGlow = vec3(0.04, 0.05, 0.08);
    vec3 dayGlow = sunTint * 0.35 + vec3(0.12, 0.16, 0.28);
    return mix(nightGlow, dayGlow, dayAmt) * band * 0.55;
}

vec3 belowHorizonFog(vec3 viewDir, float strength)
{
    if (viewDir.y >= 0.0 || strength <= 0.0)
    {
        return vec3(0.0);
    }

    float depth = smoothstep(0.0, -0.55, viewDir.y);
    return vec3(0.06, 0.07, 0.09) * depth * strength;
}

float sunDiscBloom(vec3 viewDir, vec3 lightPropagationDir, float cosDiscEdge, float bloomRadiusUv, float strength)
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

vec2 moonDiscUv(vec3 viewDir, vec3 towardMoon, float cosDiscEdge)
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

vec3 moonDiscShading(vec3 viewDir, vec3 lightPropagationDir, float cosDiscEdge)
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

    vec2 mUv = moonDiscUv(viewDir, towardMoon, edge);
    vec3 samplePos = vec3(mUv * 8.0, 0.0);
    float n0 = hash31(samplePos);
    float n1 = hash31(samplePos * 2.13 + vec3(1.7, 4.1, 0.0));
    float n2 = hash31(samplePos * 4.37 + vec3(9.0, 2.3, 0.0));
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

void main()
{
    vec3 viewDir = worldRayDir(vUv, uInvViewProj, uCameraPos);
    float dayAmt = dayFactor(uLightDir, uSunIntensity);
    vec3 sky = proceduralSky(viewDir, uLightDir, uSunIntensity, uTurbidity, uHorizonFalloff);
    vec3 nightSky = nightZenith(viewDir) + stars(viewDir, uRenderTime);
    sky = mix(nightSky, sky, dayAmt);
    sky += horizonGlow(viewDir, dayAmt);
    sky += belowHorizonFog(viewDir, uHorizonFogStrength);

    vec3 sunTint = vec3(1.0, 0.93, 0.74);
    if (dayAmt > 0.12)
    {
        sky += sunTint * sunDiscBloom(viewDir, uLightDir, uSunCosDiscEdge, uSunDiscRadiusUv, uSunDiscStrength) * dayAmt;
    }

    float nightAmt = 1.0 - dayAmt;
    if (nightAmt > 0.3)
    {
        sky += moonDiscShading(viewDir, uLightDir, uMoonCosDiscEdge) * nightAmt;
    }

    sky *= uSkyExposure;
    sky = sky / (sky + vec3(0.08));
    FragColor = vec4(pow(clamp(sky, vec3(0.0), vec3(64.0)), vec3(1.0 / 2.2)), 1.0);
}
""";

    private readonly GL _gl;
    private uint _program;
    private bool _disposed;

    public GlProceduralSkyProgram(GL gl, bool useOpenGlEs, out string? error)
    {
        _gl = gl;
        error = null;
        _program = 0;
        var vSrc = GlslSourceAdapter.Adapt(Vert330, ShaderType.VertexShader, useOpenGlEs);
        var fSrc = GlslSourceAdapter.Adapt(Frag330, ShaderType.FragmentShader, useOpenGlEs);
        var vs = Compile(ShaderType.VertexShader, vSrc, ref error);
        if (vs == 0)
        {
            return;
        }

        var fs = Compile(ShaderType.FragmentShader, fSrc, ref error);
        if (fs == 0)
        {
            _gl.DeleteShader(vs);
            return;
        }

        _program = _gl.CreateProgram();
        _gl.AttachShader(_program, vs);
        _gl.AttachShader(_program, fs);
        _gl.LinkProgram(_program);
        _gl.GetProgram(_program, GLEnum.LinkStatus, out var ok);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        if (ok == 0)
        {
            var linkLog = _gl.GetProgramInfoLog(_program);
            error = string.IsNullOrEmpty(error) ? linkLog : error + "\n" + linkLog;
            _gl.DeleteProgram(_program);
            _program = 0;
        }
    }

    public bool IsValid => _program != 0;

    public void Use() => _gl.UseProgram(_program);

    public int GetUniformLocation(string name) => _gl.GetUniformLocation(_program, name);

    private uint Compile(ShaderType type, string source, ref string? error)
    {
        var s = _gl.CreateShader(type);
        _gl.ShaderSource(s, source);
        _gl.CompileShader(s);
        _gl.GetShader(s, GLEnum.CompileStatus, out var ok);
        if (ok == 0)
        {
            var info = _gl.GetShaderInfoLog(s);
            var sb = new StringBuilder();
            sb.Append(type).Append(" compile failed: ").AppendLine(info);
            error = string.IsNullOrEmpty(error) ? sb.ToString() : error + "\n" + sb;
            _gl.DeleteShader(s);
            return 0;
        }

        return s;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_program != 0)
        {
            _gl.DeleteProgram(_program);
            _program = 0;
        }
    }
}
