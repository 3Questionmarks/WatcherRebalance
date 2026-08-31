using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class BlasphemerPowerPatch
{
    // =========================================================
    // REMOVE ORIGINAL "DIE NEXT TURN" EFFECT
    // =========================================================

    [HarmonyPatch(
        typeof(BlasphemerPower),
        nameof(BlasphemerPower.BeforeHandDraw))]
    [HarmonyPrefix]
    private static bool BeforeHandDrawPrefix(
        ref Task __result)
    {
        // Blasphemer now lasts for the entire combat.
        __result = Task.CompletedTask;

        return false;
    }


    // =========================================================
    // PREVENT MANTRA FROM BEING APPLIED AT ALL
    // =========================================================
    //
    // Current PowerCmd overload:
    //
    // PowerCmd.Apply(
    //     PlayerChoiceContext choiceContext,
    //     PowerModel power,
    //     Creature target,
    //     decimal amount,
    //     Creature? applier,
    //     CardModel? cardSource,
    //     bool silent)
    //
    // We change the incoming amount to 0 BEFORE PowerCmd
    // modifies MantraPower.
    //
    // Because the power never increases:
    //
    // - MantraGainedTracker does not increment
    // - Brilliance does not scale
    // - Prostrate does not scale
    // - Divinity is not triggered
    // =========================================================

    [HarmonyPatch(
        typeof(PowerCmd),
        nameof(PowerCmd.Apply),
        [
            typeof(PlayerChoiceContext),
            typeof(PowerModel),
            typeof(Creature),
            typeof(decimal),
            typeof(Creature),
            typeof(CardModel),
            typeof(bool)
        ])]
    [HarmonyPrefix]
    private static void ApplyPrefix(
        PowerModel power,
        Creature target,
        ref decimal amount)
    {
        // Only block positive Mantra gain.
        if (amount <= 0)
            return;

        // Ignore every other power.
        if (power is not MantraPower)
            return;

        // Only block Mantra for a creature that currently
        // has Blasphemer.
        if (target.GetPower<BlasphemerPower>() == null)
            return;

        // The Mantra application never happens.
        amount = 0;
    }


    // =========================================================
    // PREVENT DIRECT POSITIVE MODIFICATION TOO
    // =========================================================
    //
    // Some effects may increase an existing MantraPower through
    // PowerCmd.ModifyAmount rather than applying a fresh power.
    //
    // Catch that route as well, BEFORE its amount changes.
    // =========================================================

    [HarmonyPatch(
        typeof(PowerCmd),
        nameof(PowerCmd.ModifyAmount),
        [
            typeof(PlayerChoiceContext),
            typeof(PowerModel),
            typeof(decimal),
            typeof(Creature),
            typeof(CardModel),
            typeof(bool)
        ])]
    [HarmonyPrefix]
    private static void ModifyAmountPrefix(
        PowerModel power,
        ref decimal offset)
    {
        // Negative changes remain completely normal.
        if (offset <= 0)
            return;

        if (power is not MantraPower mantra)
            return;

        if (mantra.Owner
                .GetPower<BlasphemerPower>() == null)
        {
            return;
        }

        // Prevent the increase before it happens.
        offset = 0;
    }
}