using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Token;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token;

[HarmonyPatch(typeof(ThroughViolence), "OnPlay")]
public static class ThroughViolencePatch
{
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        ThroughViolence __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result = PlayWithCustomFx(
            __instance,
            __0,
            __1);

        return false;
    }

    private static async Task PlayWithCustomFx(
        ThroughViolence card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target);

        // =====================================================
        // ATTACK
        // Lightning hit + lightning sound
        // =====================================================

        await CommonActions
            .CardAttack(card, cardPlay)
            .WithHitFx(
                "vfx/vfx_attack_lightning",
                tmpSfx: "lightning_orb_evoke.mp3")
            .Execute(choiceContext);

        // =====================================================
        // SECONDARY STARRY IMPACT
        // =====================================================

        await Cmd.Wait(0.2f);

        VfxCmd.PlayOnCreatureCenter(
            cardPlay.Target,
            "vfx/vfx_starry_impact");
    }
}