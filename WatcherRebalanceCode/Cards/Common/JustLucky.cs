using BaseLib.Commands;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(JustLucky), "OnPlay")]
public static class JustLuckyFxPatch
{
    [HarmonyPrefix]
    static bool Prefix(
        JustLucky __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result = PlayJustLucky(
            __instance,
            __0,
            __1);

        return false;
    }

    private static async Task PlayJustLucky(
        JustLucky card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // Preserve original order:
        // Block -> Scry -> Attack

        await CommonActions.CardBlock(
            card,
            cardPlay);

        await ScryCmd.Execute(
            choiceContext,
            card);

        // Extra coin sound immediately before the attack.
        // The attack itself supplies "silent_attack".
        SfxCmd.Play("event:/sfx/ui/gold/gold_1");

        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_coin_explosion_regular",
                tmpSfx: "silent_attack")
            .Execute(
                choiceContext);
    }
}