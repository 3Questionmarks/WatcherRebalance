using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Events;
using Watcher.Code.Powers;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class StudyPowerPatch
{
    /*
     * STUDY
     *
     * Whenever you change Stances,
     * shuffle an Insight into your Draw Pile.
     *
     * Multiple Study powers stack through Amount.
     */


    // =========================================================
    // DISABLE ORIGINAL END-OF-TURN EFFECT
    // =========================================================

    [HarmonyPatch(
        typeof(StudyPower),
        "AfterSideTurnEnd")]
    [HarmonyPrefix]
    private static bool DisableOriginalAfterSideTurnEnd(
        ref Task __result)
    {
        __result =
            Task.CompletedTask;

        return false;
    }


    // =========================================================
    // STANCE CHANGE
    // =========================================================

    [HarmonyPatch(
        typeof(WatcherHook),
        "OnStanceChange")]
    [HarmonyPostfix]
    private static void OnStanceChangePostfix(
        PlayerChoiceContext __0,
        Player __1,
        WatcherStanceModel __2,
        WatcherStanceModel __3,
        ref Task __result)
    {
        Task originalTask =
            __result;

        __result =
            HandleStanceChange(
                originalTask,
                __0,
                __1,
                __2,
                __3);
    }


    private static async Task HandleStanceChange(
        Task originalTask,
        PlayerChoiceContext ctx,
        Player player,
        WatcherStanceModel oldStance,
        WatcherStanceModel newStance)
    {
        // Preserve everything else attached to WatcherHook.
        await originalTask;


        // Don't trigger if this wasn't actually a stance change.
        if (oldStance.GetType() == newStance.GetType())
            return;


        StudyPower? studyPower =
            player.Creature
                .GetPower<StudyPower>();

        if (studyPower == null)
            return;

        if (studyPower.Amount <= 0)
            return;


        // One Insight per Study stack.
        await WatcherCmd.GiveCards<Insight>(
            player,
            studyPower.Amount,
            PileType.Draw,
            CardPilePosition.Random);
    }
}