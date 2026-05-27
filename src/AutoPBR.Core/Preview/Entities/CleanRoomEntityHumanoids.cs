using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Humanoid, piglin, and illager rigs.

    /// <summary>Preview stand-in for vanilla <c>AbstractIllager.IllagerArmPose</c> (<c>IllagerModel.setupAnim</c>, 26.1.2 <c>client.jar</c>).</summary>
    private enum IllagerPreviewArmPoseKind
    {
        Crossed,
        AttackingEmptyHands,
        AttackingWeapon,
        Spellcasting,
        BowAndArrow,
        CrossbowHold,
        CrossbowCharge,
        Celebrating,
    }

}
