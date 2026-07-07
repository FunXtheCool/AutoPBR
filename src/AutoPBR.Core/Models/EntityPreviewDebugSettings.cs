namespace AutoPBR.Core.Models;



/// <summary>Override vanilla LER fold policy for live Explore A/B.</summary>

public enum EntityPreviewLerBasisOverride

{

    PolicyDefault = 0,

    StandardWorldRoot = 1,

    RightComposeLocalChain = 2,

    Skip = 3,

}



/// <summary>

/// Live debug toggles for baby entity preview trial/error (Explore 3D). Global; read by Core preview + OpenGL backend.

/// </summary>

public static class EntityPreviewDebugSettings

{

    /// <summary>Bumped when a toggle affects tessellation / bone matrices; backend clears GPU bind cache.</summary>

    public static int Revision { get; private set; }



    /// <summary>Emit entity draw-contract and runtime diagnostic lines every frame (bypass rate limiting).</summary>

    public static bool LogDrawContractEveryFrame { get; set; }



    /// <summary>Replace <see cref="Preview.EntityModelRuntime"/> LER policy with a fixed basis.</summary>

    public static EntityPreviewLerBasisOverride LerBasisOverride { get; set; } = EntityPreviewLerBasisOverride.PolicyDefault;



    /// <summary>Legacy <c>T × Er</c> at texel scale (pre–2026-05-28 adult regression). Entity Debug A/B only — production uses ModelPart block stack; see <c>docs/runtime-ir-preview-plan.md</c>.</summary>

    public static bool UseLegacyTranslationTimesRotationPartPose { get; set; }



    /// <summary>Skip all <see cref="Preview.GeometryIrPartTreeRepair"/> before parity emit.</summary>

    public static bool SkipAllPartTreeRepair { get; set; }



    /// <summary>False-color preview draw batches by <see cref="PreviewDepthLayerKind"/> (Explore 3D debug).</summary>

    public static bool ShowDepthLayerDebug { get; set; }



    /// <summary>Ears, head→head_parts, goat horns, breeze wind stack, etc.</summary>

    public static bool RepairGlobalReparentRules { get; set; } = true;



    /// <summary>Reparent quadruped legs under <c>body</c> when not a flat root bake.</summary>

    public static bool RepairQuadrupedLegReparent { get; set; } = true;



    /// <summary>Force leg reparent even when flat quadruped bake would skip (fox/cow/chicken baby class).</summary>

    public static bool RepairForceLegReparentOnFlatBake { get; set; }



    /// <summary>When head stack is nested under body, reparent flat root legs (baby equine class).</summary>

    public static bool RepairHeadStackLegReparent { get; set; } = true;



    /// <summary>Drop root duplicate when the same part id appears nested.</summary>

    public static bool RepairRemoveDuplicateRootSiblings { get; set; } = true;



    /// <summary>Collapse <c>inner_body</c> under <c>body</c> and trim fleece cuboid.</summary>

    public static bool RepairCollapseInnerBody { get; set; } = true;



    /// <summary>Remove duplicate part ids within one parent's children.</summary>

    public static bool RepairDeduplicateNestedPartIds { get; set; } = true;



    /// <summary>Zero createBodyLayer/createBabyLayer root offset for equine hosts.</summary>

    public static bool RepairZeroEquineRootOffset { get; set; } = true;



    public static void NotifyMeshAffectingChange() => Revision++;



    public static bool RequiresMeshRebuild(int cachedRevision) => cachedRevision != Revision;

}

