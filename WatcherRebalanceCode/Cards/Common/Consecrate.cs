using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(Consecrate), "OnPlay")]
public static class ConsecrateVfxPatch
{
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Consecrate __instance,
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
        Consecrate card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions
            .CardAttack(card, cardPlay)
            .WithHitFx("vfx/stabby_line_particles")
            .Execute(choiceContext);
    }
}