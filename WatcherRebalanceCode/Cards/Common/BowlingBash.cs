using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using Watcher.Code.Cards.Common;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common;

[HarmonyPatch(typeof(BowlingBash), "OnPlay")]
public static class BowlingBashFxPatch
{
    [HarmonyPrefix]
    static bool Prefix(
        BowlingBash __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result = PlayBowlingBash(
            __instance,
            __0,
            __1);

        return false;
    }

    private static async Task PlayBowlingBash(
        BowlingBash card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        var combatState =
            cardPlay.Target?.CombatState;

        if (combatState == null)
            return;

        int enemyCount =
            combatState.Enemies.Count(
                enemy =>
                    NCombatRoom.Instance
                        ?.GetCreatureNode(enemy)
                        ?.Visible
                    ?? true);

        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitCount(enemyCount)
            .WithHitFx(
                "vfx/vfx_attack_blunt",
                tmpSfx: "blunt_attack.mp3")
            .Execute(
                choiceContext);
    }
}