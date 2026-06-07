using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Avalonia.OpenGL;
using Avalonia.Platform;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>
public sealed partial class OpenGlPreviewBackend
{
    private void SetMatrixOnProgram(GlShaderProgram program, string name, Matrix4x4 m)
    {
        var loc = program.GetUniformLocation(name);
        if (loc < 0)
        {
            return;
        }

        var mt = Matrix4x4.Transpose(m);
        _gl!.UniformMatrix4(loc, 1, false, in mt.M11);
    }

    private void SetMatrixOnProgram(GlProceduralSkyProgram program, string name, Matrix4x4 m)
    {
        var loc = program.GetUniformLocation(name);
        if (loc < 0)
        {
            return;
        }

        var mt = Matrix4x4.Transpose(m);
        _gl!.UniformMatrix4(loc, 1, false, in mt.M11);
    }

    private void SetVec3OnProgram(GlShaderProgram program, string name, Vector3 v)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform3(loc, v.X, v.Y, v.Z);
        }
    }

    private void SetVec3OnProgram(GlProceduralSkyProgram program, string name, Vector3 v)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform3(loc, v.X, v.Y, v.Z);
        }
    }

    private void SetVec2OnProgram(GlProceduralSkyProgram program, string name, Vector2 v)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform2(loc, v.X, v.Y);
        }
    }

    private void SetFloatOnProgram(GlShaderProgram program, string name, float v)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform1(loc, v);
        }
    }

    private void SetFloatOnProgram(GlProceduralSkyProgram program, string name, float v)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform1(loc, v);
        }
    }

    private void SetIntOnProgram(GlShaderProgram program, string name, int v)
    {
        var loc = program.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform1(loc, v);
        }
    }

    private void SetMatrix(string name, Matrix4x4 m)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc < 0)
        {
            return;
        }

        var mt = Matrix4x4.Transpose(m);
        _gl!.UniformMatrix4(loc, 1, false, in mt.M11);
    }

    private void SetVec2(string name, Vector2 v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform2(loc, v.X, v.Y);
        }
    }

    private void SetVec3(string name, Vector3 v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform3(loc, v.X, v.Y, v.Z);
        }
    }

    private void SetFloat(string name, float v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform1(loc, v);
        }
    }

    private void SetInt(string name, int v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform1(loc, v);
        }
    }
}
