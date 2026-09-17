using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;


// ============================================================================
// FLURRY OF BLOWS VFX + SFX
// ============================================================================
//
// VFX:
//     vfx/vfx_attack_blunt
//
// SFX:
//     blunt_attack.mp3
// ============================================================================

[HarmonyPatch(typeof(FlurryOfBlows), "OnPlay")]
public static class FlurryOfBlowsAttackFxPatch
{
    [HarmonyPrefix]
    static bool Prefix(
        FlurryOfBlows __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayFlurryOfBlows(
                __instance,
                __0,
                __1);

        return false;
    }


    private static async Task PlayFlurryOfBlows(
        FlurryOfBlows card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_attack_blunt",
                tmpSfx: "blunt_attack.mp3")
            .Execute(
                choiceContext);
    }
}