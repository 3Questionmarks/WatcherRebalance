using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class EstablishmentPowerPatch
{
    /*
     * ESTABLISHMENT FIX
     *
     * Establishment should trigger when:
     *
     * - A card is individually Retained.
     * - The Hand is Retained by RetainHandPower
     *   (Equilibrium / upgraded Simmering Fury).
     *
     * It should NOT trigger merely because the Hand wasn't
     * discarded, such as:
     *
     * - Runic Pyramid
     * - Well-Laid Plans
     */


    [HarmonyPatch(
        typeof(EstablishmentPower),
        "AfterFlush")]
    [HarmonyPrefix]
    private static bool AfterFlushPrefix(
        EstablishmentPower __instance,
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyCollection<CardModel> flushedCards,
        IReadOnlyCollection<CardModel> retainedCards,
        ref Task __result)
    {
        __result =
            HandleAfterFlush(
                __instance,
                player,
                retainedCards);

        return false;
    }


    private static Task HandleAfterFlush(
        EstablishmentPower power,
        Player player,
        IReadOnlyCollection<CardModel> retainedCards)
    {
        if (player.Creature != power.Owner)
            return Task.CompletedTask;


        // RetainHandPower is specifically the game's
        // "Retain your Hand this turn" effect.
        //
        // Runic Pyramid and Well-Laid Plans do not use this
        // power, so they will not satisfy this condition.
        bool retainEntireHand =
            player.Creature
                .GetPower<RetainHandPower>() != null;


        foreach (CardModel card in retainedCards)
        {
            // Individual Retain:
            //
            // - Retain keyword
            // - Meditate's GiveSingleTurnRetain()
            //
            // OR
            //
            // Entire Hand retained by RetainHandPower.
            if (!card.ShouldRetainThisTurn &&
                !retainEntireHand)
            {
                continue;
            }


            card.EnergyCost.AddThisCombat(
                -power.Amount);
        }


        return Task.CompletedTask;
    }
}