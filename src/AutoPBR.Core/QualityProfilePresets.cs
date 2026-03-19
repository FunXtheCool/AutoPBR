using AutoPBR.Core.Models;

namespace AutoPBR.Core;

public static class QualityProfilePresets
{
    public static NormalSettings GetNormalSettings(QualityProfile profile) =>
        profile switch
        {
            QualityProfile.LowRes => new NormalSettings(
                NormalOperator: NormalOperator.ScharrVc,
                NormalKernelSize: NormalKernelSize.K3,
                NormalDerivative: NormalDerivative.ColorLuminanceMax),
            QualityProfile.HiRes => new NormalSettings(
                NormalOperator: NormalOperator.SobelVc,
                NormalKernelSize: NormalKernelSize.K5,
                NormalDerivative: NormalDerivative.ColorLuminanceBlend),
            _ => new NormalSettings(
                NormalOperator: NormalOperator.SobelVc,
                NormalKernelSize: NormalKernelSize.K3,
                NormalDerivative: NormalDerivative.Luminance)
        };

    public readonly record struct NormalSettings(
        NormalOperator NormalOperator,
        NormalKernelSize NormalKernelSize,
        NormalDerivative NormalDerivative);
}

