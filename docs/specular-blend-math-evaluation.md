# Specular Blend Math Evaluation

This note defines how to quickly evaluate new `MlSpecularBlendMath` modes and decide which ones to keep.

## Purpose

The goal is to keep only blend modes that provide a useful visual difference from `Linear` without introducing unstable artifacts.

## Evaluation Set

- Include mixed texture resolutions: at least one 16x pack and one 32x pack.
- Include material variety: stone, metal, foliage, emissive-like assets.
- Test both channel routing modes:
  - `SmoothnessOnly`
  - `Full`

## Pass/Fail Criteria

For each candidate mode:

1. **Useful Difference vs Linear**
   - Visually distinct from `Linear` on at least 2 material categories.
   - Difference should improve control (not just random contrast drift).

2. **Artifact Safety**
   - No frequent harsh clipping/banding in smoothness (R).
   - No persistent halo/noise amplification around transparent edges.

3. **Low-Res Stability**
   - Pixel-art textures should not become muddy or over-sharpened.
   - Result should remain consistent across 16x and 32x samples.

4. **Channel Predictability**
   - In `SmoothnessOnly`, impact should primarily stay in R.
   - In `Full`, multi-channel behavior should remain intuitive and controllable.

## Pruning Rule

Remove a candidate if any of the following are true:

- It is visually too close to `Linear` across the evaluation set.
- It introduces obvious artifacts in more than one sample pack.
- It behaves inconsistently between low-res test packs without clear benefit.

Keep only modes that pass all criteria and have a clear artistic use case.
