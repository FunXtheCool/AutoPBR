using AutoPBR.Core.Models;
using AutoPBR.Core.Atlas;
using Colourful;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AutoPBR.Core;

/// <summary>
/// Generates LabPBR-compatible specular (_s) textures from diffuse inputs.
/// </summary>
internal static partial class SpecularGenerator
{
    internal static double BlendChannel(double heuristic, double ml, float mix, MlSpecularBlendMath math)
    {
        var h = Math.Clamp(heuristic / 255.0, 0.0, 1.0);
        var m = Math.Clamp(ml / 255.0, 0.0, 1.0);
        var t = Math.Clamp(mix, 0f, 1f);
        if (t <= 0f)
        {
            return heuristic;
        }

        static double Lerp(double a, double b, double u) => a + (b - a) * u;
        static double ToByte(double unit) => Math.Clamp(unit * 255.0, 0.0, 255.0);
        static double SoftLight(double baseVal, double blendVal)
        {
            if (blendVal <= 0.5)
            {
                return baseVal - (1.0 - 2.0 * blendVal) * baseVal * (1.0 - baseVal);
            }

            var d = baseVal <= 0.25
                ? ((16.0 * baseVal - 12.0) * baseVal + 4.0) * baseVal
                : Math.Sqrt(baseVal);
            return baseVal + (2.0 * blendVal - 1.0) * (d - baseVal);
        }

        static double Overlay(double baseVal, double blendVal) =>
            baseVal < 0.5 ? 2.0 * baseVal * blendVal : 1.0 - 2.0 * (1.0 - baseVal) * (1.0 - blendVal);
        static double Screen(double baseVal, double blendVal) => 1.0 - (1.0 - baseVal) * (1.0 - blendVal);
        static double Bias(double x, double b)
        {
            var bb = Math.Clamp(b, 0.001, 0.999);
            return x / ((((1.0 / bb) - 2.0) * (1.0 - x)) + 1.0);
        }

        static double Gain(double x, double g)
        {
            if (x < 0.5)
            {
                return 0.5 * Bias(2.0 * x, g);
            }

            return 1.0 - 0.5 * Bias(2.0 - 2.0 * x, g);
        }

        static double Logit(double x)
        {
            var xx = Math.Clamp(x, 0.001, 0.999);
            return Math.Log(xx / (1.0 - xx));
        }

        static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));

        var blended = math switch
        {
            MlSpecularBlendMath.SoftLight => Lerp(h, SoftLight(h, m), t),
            MlSpecularBlendMath.Overlay => Lerp(h, Overlay(h, m), t),
            MlSpecularBlendMath.Screen => Lerp(h, Screen(h, m), t),
            MlSpecularBlendMath.BiasGain => Lerp(h, Gain(h, m), t),
            MlSpecularBlendMath.SigmoidCrossfade => Sigmoid(Lerp(Logit(h), Logit(m), t)),
            _ => Lerp(h, m, t)
        };

        return ToByte(blended);
    }
}
