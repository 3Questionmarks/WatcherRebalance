using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch(typeof(TalkToTheHand), "OnPlay")]
public static class TalkToTheHandPatch
{
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        TalkToTheHand __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result = PlayWithCustomFx(__instance, __0, __1);
        return false;
    }

    private static async Task PlayWithCustomFx(
        TalkToTheHand card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions.CardAttack(card, cardPlay)
            .WithHitFx("vfx/vfx_attack_blunt", tmpSfx: "blunt_attack.mp3")
            .Execute(choiceContext);

        await CommonActions.Apply<BlockReturnPower>(
            choiceContext,
            card,
            cardPlay);
    }
}