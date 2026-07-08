using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Preview.Entities;

internal sealed partial class EntityModelRuntime
{

    /// <summary>Vanilla <c>AnimationUtils.bobModelPart</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerBobModelPart(ref float xRot, ref float zRot, float ageInTicks, float mult)
    {
        zRot += mult * ((MathF.Cos(ageInTicks * 0.09f) * 0.05f) + 0.05f);
        xRot += mult * (MathF.Sin(ageInTicks * 0.067f) * 0.05f);
    }

    /// <summary>Vanilla <c>AnimationUtils.bobArms</c> (26.1.2 <c>client.jar</c>): right mult <c>+1</c>, left mult <c>-1</c>.</summary>
    private static void IllagerBobArms(ref float rightArmX, ref float rightArmZ, ref float leftArmX, ref float leftArmZ, float ageInTicks)
    {
        IllagerBobModelPart(ref rightArmX, ref rightArmZ, ageInTicks, 1f);
        IllagerBobModelPart(ref leftArmX, ref leftArmZ, ageInTicks, -1f);
    }

    /// <summary>Vanilla <c>AnimationUtils.swingWeaponDown</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerSwingWeaponDown(
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        bool mainHandIsRight,
        float attackAnim,
        float ageInTicks)
    {
        var f5 = MathF.Sin(attackAnim * MathF.PI);
        var inner = 1f - (1f - attackAnim) * (1f - attackAnim);
        var f6 = MathF.Sin(inner * MathF.PI);
        rightArmZ = 0f;
        leftArmZ = 0f;
        rightArmY = 0.15707964f;
        leftArmY = -0.15707964f;
        if (mainHandIsRight)
        {
            rightArmX = -1.8849558f + (MathF.Cos(ageInTicks * 0.09f) * 0.15f);
            leftArmX = 0f + (MathF.Cos(ageInTicks * 0.19f) * 0.5f);
            rightArmX += f5 * 2.2f - f6 * 0.4f;
            leftArmX += f5 * 1.2f - f6 * 0.4f;
        }
        else
        {
            rightArmX = 0f + (MathF.Cos(ageInTicks * 0.19f) * 0.5f);
            leftArmX = -1.8849558f + (MathF.Cos(ageInTicks * 0.09f) * 0.15f);
            rightArmX += f5 * 1.2f - f6 * 0.4f;
            leftArmX += f5 * 2.2f - f6 * 0.4f;
        }

        IllagerBobArms(ref rightArmX, ref rightArmZ, ref leftArmX, ref leftArmZ, ageInTicks);
    }

    /// <summary>Vanilla <c>AnimationUtils.animateZombieArms</c> for illager empty-hand attacks (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerAnimateZombieArms(
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        bool useFifteenDivisor,
        bool swingIsStab,
        float attackTime,
        float ageInTicks)
    {
        if (swingIsStab)
        {
            IllagerBobArms(ref rightArmX, ref rightArmZ, ref leftArmX, ref leftArmZ, ageInTicks);
            return;
        }

        var div = useFifteenDivisor ? 1.5f : 2.25f;
        var f6 = -MathF.PI / div;
        var f7 = MathF.Sin(attackTime * MathF.PI);
        var inner = 1f - (1f - attackTime) * (1f - attackTime);
        var f8 = MathF.Sin(inner * MathF.PI);
        rightArmZ = 0f;
        rightArmY = -0.1f + 0.6f * f7;
        rightArmX = f6;
        rightArmX += f7 * 1.2f - f8 * 0.4f;
        leftArmZ = 0f;
        leftArmY = -0.1f + 0.6f * f7;
        leftArmX = f6;
        leftArmX += f7 * 1.2f - f8 * 0.4f;
        IllagerBobArms(ref rightArmX, ref rightArmZ, ref leftArmX, ref leftArmZ, ageInTicks);
    }

    /// <summary>Vanilla <c>AnimationUtils.animateCrossbowHold</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerAnimateCrossbowHold(
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        float headYawRad,
        float headPitchRad,
        bool rightHanded)
    {
        if (rightHanded)
        {
            rightArmY += -0.3f + headYawRad;
            leftArmY += 0.6f + headYawRad;
            rightArmX = -1.5707964f + headPitchRad + 0.1f;
            leftArmX = -1.5f + headPitchRad;
        }
        else
        {
            leftArmY += -0.3f + headYawRad;
            rightArmY += 0.6f + headYawRad;
            leftArmX = -1.5707964f + headPitchRad + 0.1f;
            rightArmX = -1.5f + headPitchRad;
        }
    }

    /// <summary>Vanilla <c>AnimationUtils.animateCrossbowCharge</c> (26.1.2 <c>client.jar</c>).</summary>
    private static void IllagerAnimateCrossbowCharge(
        ref float rightArmX,
        ref float rightArmY,
        ref float rightArmZ,
        ref float leftArmX,
        ref float leftArmY,
        ref float leftArmZ,
        float maxCrossbowChargeDuration,
        float ticksUsingItem,
        bool rightHanded)
    {
        var denom = Math.Max(1f, maxCrossbowChargeDuration);
        var t = Math.Clamp(ticksUsingItem, 0f, denom) / denom;
        var sign = rightHanded ? 1f : -1f;
        if (rightHanded)
        {
            rightArmY = -0.8f;
            rightArmX = -0.97079635f;
            leftArmX = rightArmX;
            leftArmY = (0.4f + (0.85f - 0.4f) * t) * sign;
            leftArmX = leftArmX + (-1.5707964f - leftArmX) * t;
        }
        else
        {
            leftArmY = 0.8f;
            leftArmX = -0.97079635f;
            rightArmX = leftArmX;
            rightArmY = (0.4f + (0.85f - 0.4f) * t) * -1f;
            rightArmX = rightArmX + (-1.5707964f - rightArmX) * t;
        }
    }

}
