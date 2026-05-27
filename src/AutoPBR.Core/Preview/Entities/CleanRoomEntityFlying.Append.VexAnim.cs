using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    private static void VexApplyArmsCharging(
        bool rightHandHoldingItem,
        bool leftHandHoldingItem,
        float f2,
        out float rightX,
        out float rightY,
        out float rightZ,
        out float leftX,
        out float leftY,
        out float leftZ)
    {
        rightX = 0f;
        rightY = 0f;
        rightZ = 0f;
        leftX = 0f;
        leftY = 0f;
        leftZ = 0f;
        if (!rightHandHoldingItem && !leftHandHoldingItem)
        {
            rightX = -1.2217305f;
            rightY = 0.2617994f;
            rightZ = -0.47123888f - f2;
            leftX = -1.2217305f;
            leftY = -0.2617994f;
            leftZ = 0.47123888f + f2;
            return;
        }

        if (rightHandHoldingItem)
        {
            rightX = 3.6651914f;
            rightY = 0.2617994f;
            rightZ = -0.47123888f - f2;
        }

        if (leftHandHoldingItem)
        {
            leftX = 3.6651914f;
            leftY = -0.2617994f;
            leftZ = 0.47123888f + f2;
        }
    }

}
