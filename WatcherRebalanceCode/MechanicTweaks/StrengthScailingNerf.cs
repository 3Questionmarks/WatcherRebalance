using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace WatcherRebalance.WatcherRebalanceCode.Patches;


// ============================================================================
// REBALANCED WATCHER STRENGTH SCALING
// ============================================================================
//
// Normal Watcher:
//
//     (Base Damage + Strength) * Stance Multiplier
//
// Rebalanced:
//
//     Base Damage * Stance Multiplier + Strength
//
// This now uses the ACTUAL configured stance multiplier.
//
// Example:
//
// Base damage = 6
// Strength = 3
// Wrath multiplier = 1.5
//
// Normal:
//     (6 + 3) * 1.5 = 13.5
//
// Rebalanced:
//     6 * 1.5 + 3 = 12
//
// We accomplish this by dividing Strength's additive contribution by the
// outgoing stance multiplier BEFORE the damage system subsequently applies
// that multiplier.
// ============================================================================

[HarmonyPatch(
    typeof(StrengthPower),
    nameof(StrengthPower.ModifyDamageAdditive))]
public static class WatcherStrengthScalingPatch
{
    [HarmonyPostfix]
    private static void ModifyDamageAdditivePostfix(
        StrengthPower __instance,
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay,
        ref decimal __result)
    {
        // =====================================================
        // CONFIG DISABLED
        // =====================================================
        //
        // Leave Strength completely untouched, restoring normal
        // Watcher behaviour.
        // =====================================================

        if (!Config.RebalancedStrengthScaling)
            return;


        if (__result == 0m)
            return;


        var player =
            __instance.Owner.Player;


        if (player == null)
            return;


        // StrengthPower itself already ensures this Strength
        // belongs to the damage dealer, but this is an additional
        // safety guard.
        if (dealer != __instance.Owner)
            return;


        decimal stanceMultiplier =
            StanceConfigMath.GetConfiguredOutgoingMultiplier(
                player,
                __instance.Owner.CombatState);


        // Neutral/no multiplier means nothing needs changing.
        if (stanceMultiplier == 1m)
            return;


        // Defensive protection if a future config range allows 0.
        if (stanceMultiplier == 0m)
            return;


        __result /=
            stanceMultiplier;
    }
}