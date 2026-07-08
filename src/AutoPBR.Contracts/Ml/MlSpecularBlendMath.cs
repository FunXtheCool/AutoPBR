namespace AutoPBR.Contracts.Ml;

/// <summary>Math function for heuristic↔ML channel mixing.</summary>
public enum MlSpecularBlendMath
{
    Linear = 0,
    SoftLight = 1,
    Overlay = 2,
    Screen = 3,
    BiasGain = 4,
    SigmoidCrossfade = 5
}
