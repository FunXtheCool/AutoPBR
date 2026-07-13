using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private readonly record struct GenesisProgramCacheKey(GenesisShaderFeatureMask Mask, bool Tessellation);

    private const int MaxGenesisProgramCacheEntries = 32;

    private readonly Dictionary<GenesisProgramCacheKey, GlShaderProgram> _genesisPrograms = new();
    private readonly LinkedList<GenesisProgramCacheKey> _genesisProgramLru = new();
    private GenesisProgramCacheKey _activeGenesisProgramKey;

    private void DisableGenesisTessellationCompile(string? reason)
    {
        if (_genesisTessellationCompileDisabled)
        {
            return;
        }

        _genesisTessellationCompileDisabled = true;
        if (_genesisTessellationFailureLogged)
        {
            return;
        }

        _genesisTessellationFailureLogged = true;
        var detail = string.IsNullOrWhiteSpace(reason) ? "compile failed" : TrimTessellationFailureReason(reason);
        EmitDiagnostic("[3D preview] Genesis tessellation disabled for this session. " + detail);
    }

    private static string TrimTessellationFailureReason(string reason)
    {
        var oneLine = reason.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= 240 ? oneLine : oneLine[..240] + "...";
    }

    private void ResetGenesisTessellationCompileState()
    {
        _genesisTessellationCompileDisabled = false;
        _genesisTessellationFailureLogged = false;
    }

    private void EnsureGenesisProgramForFrame(ref GlRenderFrame frame)
    {
        if (_shaderCtx is null)
        {
            return;
        }

        var mask = GenesisShaderFeatureMaskBuilder.Build(frame.Settings, frame.EntityEmulatedPreview);
        var wantsTessellation = !_useOpenGlEs &&
                                !_genesisTessellationCompileDisabled &&
                                frame.EnableTessellationDisplacementEff;
        var cacheKey = new GenesisProgramCacheKey(mask, wantsTessellation);
        if (_program is { IsValid: true } && cacheKey == _activeGenesisProgramKey)
        {
            return;
        }

        if (!TryGetOrCreateGenesisProgram(cacheKey, out var program, out var err))
        {
            if (wantsTessellation)
            {
                DisableGenesisTessellationCompile(err);
                var fallbackKey = cacheKey with { Tessellation = false };
                if (!TryGetOrCreateGenesisProgram(fallbackKey, out program, out _))
                {
                    return;
                }

                cacheKey = fallbackKey;
            }
            else
            {
                return;
            }
        }

        if (_program is not null && !_genesisPrograms.ContainsValue(_program))
        {
            _program.Dispose();
        }

        _program = program;
        _activeGenesisProgramKey = cacheKey;
        _mainProgramUsesTessellation = cacheKey.Tessellation;
        _mainEntityUniformLocs = ResolveEntitySkinningUniformLocs(_program);
        _mainUniformLocs = ResolveMainProgramUniformLocs(_program);
        _lastMainPassAppliedSettingsRevision = -1;
    }

    private bool TryGetOrCreateGenesisProgram(
        GenesisProgramCacheKey cacheKey,
        out GlShaderProgram program,
        out string? error)
    {
        error = null;
        if (cacheKey.Tessellation && _genesisTessellationCompileDisabled)
        {
            program = new GlShaderProgram(_gl!, 0);
            error = "Tessellation compile previously failed.";
            return false;
        }

        if (_genesisPrograms.TryGetValue(cacheKey, out var cached) && cached.IsValid)
        {
            TouchGenesisProgramLru(cacheKey);
            program = cached;
            return true;
        }

        var defines = GenesisShaderFeatureMaskBuilder.ToDefines(cacheKey.Mask);
        if (cacheKey.Tessellation)
        {
            program = CreatePreviewProgram(
                "genesis.vert",
                "genesis.tcs",
                "genesis.tes",
                "genesis.frag",
                out error,
                $"genesis+tess+{(byte)cacheKey.Mask:X2}",
                defines);
        }
        else
        {
            program = CreatePreviewProgram(
                "genesis.vert",
                "genesis.frag",
                out error,
                $"genesis+{(byte)cacheKey.Mask:X2}",
                defines);
        }

        if (!program.IsValid)
        {
            program.Dispose();
            program = new GlShaderProgram(_gl!, 0);
            return false;
        }

        if (_genesisPrograms.TryGetValue(cacheKey, out var old) && !ReferenceEquals(old, program))
        {
            old.Dispose();
        }

        _genesisPrograms[cacheKey] = program;
        TouchGenesisProgramLru(cacheKey);
        EvictGenesisProgramsIfNeeded();
        return true;
    }

    private void TouchGenesisProgramLru(GenesisProgramCacheKey key)
    {
        _genesisProgramLru.Remove(key);
        _genesisProgramLru.AddFirst(key);
    }

    private void EvictGenesisProgramsIfNeeded()
    {
        while (_genesisPrograms.Count > MaxGenesisProgramCacheEntries && _genesisProgramLru.Last is not null)
        {
            var evict = _genesisProgramLru.Last.Value;
            _genesisProgramLru.RemoveLast();
            if (_genesisPrograms.Remove(evict, out var disposed) && !ReferenceEquals(disposed, _program))
            {
                disposed.Dispose();
            }
        }
    }

    private void DestroyGenesisProgramCache()
    {
        foreach (var entry in _genesisPrograms.Values)
        {
            if (!ReferenceEquals(entry, _program))
            {
                entry.Dispose();
            }
        }

        _genesisPrograms.Clear();
        _genesisProgramLru.Clear();
        _activeGenesisProgramKey = default;
        ResetGenesisTessellationCompileState();
    }

    private void PrewarmCommonGenesisProgramsOnGpu()
    {
        if (_shaderCtx is null || _useOpenGlEs)
        {
            return;
        }

        var masks = new[]
        {
            GenesisShaderFeatureMask.None,
            GenesisShaderFeatureMask.Shadow | GenesisShaderFeatureMask.Ibl,
            GenesisShaderFeatureMask.All,
        };

        foreach (var mask in masks)
        {
            var cacheKey = new GenesisProgramCacheKey(mask, Tessellation: false);
            if (_genesisPrograms.ContainsKey(cacheKey))
            {
                continue;
            }

            _ = TryGetOrCreateGenesisProgram(cacheKey, out _, out _);
        }
    }
}
