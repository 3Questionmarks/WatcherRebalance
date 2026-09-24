
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Commands;
using Watcher.Code.Powers;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.MechanicTweaks;


// ============================================================================
// SCALING MANTRA / DIVINITY REQUIREMENT
// ============================================================================
//
// ENABLED:
//
//     First Divinity:   10 Mantra
//     Second Divinity:  15 Mantra
//     Third Divinity:   20 Mantra
//     Fourth Divinity:  25 Mantra
//     etc.
//
// Every actual entry into Divinity increases the NEXT requirement by 5.
//
// DISABLED:
//
//     Original fixed 10-Mantra system.
//
// ============================================================================

public static class ScalingMantra
{
    public const int BaseRequirement =
        10;

    public const int IncreasePerDivinity =
        5;


    private sealed class CombatData
    {
        public int DivinityEntries;
    }


    // PlayerCombatState is recreated for each combat, so this state naturally
    // resets whenever a new room/combat begins.
    private static readonly ConditionalWeakTable<
        PlayerCombatState,
        CombatData> Data =
        new();


    private static CombatData? GetData(
        Player player)
    {
        PlayerCombatState? combatState =
            player.PlayerCombatState;

        if (combatState == null)
            return null;

        return Data.GetOrCreateValue(
            combatState);
    }


    // ========================================================================
    // CURRENT REQUIREMENT
    // ========================================================================

    public static int GetRequirement(
        Player player)
    {
        if (!Config.ScalingDivinityRequirement)
            return BaseRequirement;

        CombatData? data =
            GetData(player);

        if (data == null)
            return BaseRequirement;

        return
            BaseRequirement +
            data.DivinityEntries *
            IncreasePerDivinity;
    }


    // ========================================================================
    // RECORD DIVINITY ENTRY
    // ========================================================================

    public static void RecordDivinityEntry(
        Player player)
    {
        if (!Config.ScalingDivinityRequirement)
            return;

        CombatData? data =
            GetData(player);

        if (data == null)
            return;

        data.DivinityEntries++;
    }


    // ========================================================================
    // BLASPHEMY MANTRA GAIN
    // ========================================================================
    //
    // Returns exactly how much Mantra Blasphemy needs to add to reach the
    // current Divinity requirement.
    //
    // Requirement 10, current 0 -> 10
    // Requirement 15, current 6 -> 9
    // Requirement 20, current 7 -> 13
    //
    // When the config is disabled this returns 10.
    //
    // ========================================================================

    public static int GetBlasphemyMantraGain(
        Player player)
    {
        if (!Config.ScalingDivinityRequirement)
            return 10;

        int requirement =
            GetRequirement(
                player);

        int currentMantra =
            player.Creature
                .GetPower<MantraPower>()
                ?.Amount
            ?? 0;

        return Math.Max(
            0,
            requirement - currentMantra);
    }
}


// ============================================================================
// MANTRA POWER
// ============================================================================
//
// CONFIG OFF:
//
//     Do not interfere.
//     Watcher's original fixed-10 implementation runs.
//
// CONFIG ON:
//
//     Replace the original threshold with the escalating requirement.
//
// ============================================================================

[HarmonyPatch(
    typeof(MantraPower),
    nameof(MantraPower.AfterPowerAmountChanged))]
public static class ScalingMantraPowerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        MantraPower __instance,
        PlayerChoiceContext __0,
        PowerModel __1,
        decimal __2,
        Creature? __3,
        CardModel? __4,
        ref Task __result)
    {
        // ====================================================================
        // CONFIG DISABLED
        // ====================================================================
        //
        // Run Watcher's original Mantra implementation completely unchanged.
        // ====================================================================

        if (!Config.ScalingDivinityRequirement)
            return true;


        // ====================================================================
        // CONFIG ENABLED
        // ====================================================================

        __result =
            NewAfterPowerAmountChanged(
                __instance,
                __0,
                __1,
                __2,
                __3,
                __4);


        return false;
    }


    private static async Task NewAfterPowerAmountChanged(
        MantraPower mantra,
        PlayerChoiceContext choiceContext,
        PowerModel changedPower,
        decimal amountChanged,
        Creature? applier,
        CardModel? cardSource)
    {
        Player? player =
            mantra.Owner.Player;


        // Preserve Watcher's original trigger conditions.
        if (changedPower is not MantraPower ||
            amountChanged <= 0 ||
            applier != mantra.Owner ||
            player == null)
        {
            return;
        }


        // Preserve original Mantra gain SFX.
        SfxCmd.Play(
            "res://Watcher/audio/mantra_gain.ogg");


        int requirement =
            ScalingMantra.GetRequirement(
                player);


        // Not enough Mantra yet.
        if (mantra.Amount < requirement)
            return;


        // ====================================================================
        // PAY CURRENT REQUIREMENT
        // ====================================================================

        await PowerCmd.ModifyAmount(
            choiceContext,
            mantra,
            -requirement,
            mantra.Owner,
            cardSource);


        // ====================================================================
        // ENTER DIVINITY
        // ====================================================================
        //
        // The Divinity entry patch below records the entry.
        //
        // Therefore, if this entry required 10 Mantra, the next one will
        // require 15.
        // ====================================================================

        await StanceCmd.EnterDivinity(
            choiceContext,
            player,
            cardSource);
    }
}


// ============================================================================
// DIVINITY ENTRY TRACKING
// ============================================================================
//
// Count actual entries into Divinity.
//
// This means the requirement increases regardless of whether Divinity was
// entered through Mantra, Blasphemy, or another effect.
//
// ============================================================================

[HarmonyPatch(
    typeof(DivinityStance),
    nameof(DivinityStance.OnEnterStance))]
public static class ScalingDivinityEntryPatch
{
    [HarmonyPostfix]
    private static async Task Postfix(
        Task __result,
        Player __1)
    {
        await __result;


        // No tracking when the mechanic is disabled.
        if (!Config.ScalingDivinityRequirement)
            return;


        ScalingMantra.RecordDivinityEntry(
            __1);
    }
}


// ============================================================================
// MANTRA LOCALISATION
// ============================================================================
//
// Power descriptions use two separate LocStrings:
//
//     Description
//         Generic/non-combat description, such as the compendium.
//
//     SmartDescription
//         In-combat description. This version receives the live Mantra
//         requirement.
//
// Both properties are declared on PowerModel, not MantraPower. Therefore we
// patch the PowerModel getters and filter to MantraPower inside each postfix.
//
// CONFIG OFF:
//     Both descriptions remain completely vanilla.
//
// CONFIG ON:
//     Both descriptions use WatcherRebalance localisation.
//
// ============================================================================


// ============================================================================
// GENERIC DESCRIPTION
// ============================================================================

[HarmonyPatch(
    typeof(PowerModel),
    nameof(PowerModel.Description),
    MethodType.Getter)]
public static class ScalingMantraDescriptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        PowerModel __instance,
        ref LocString __result)
    {
        if (__instance is not MantraPower)
            return;

        if (!Config.ScalingDivinityRequirement)
            return;

        __result =
            new LocString(
                "powers",
                "WATCHERREBALANCE-MANTRA_SCALING.description");
    }
}


// ============================================================================
// SMART / IN-COMBAT DESCRIPTION
// ============================================================================

[HarmonyPatch(
    typeof(PowerModel),
    nameof(PowerModel.SmartDescription),
    MethodType.Getter)]
public static class ScalingMantraSmartDescriptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        PowerModel __instance,
        ref LocString __result)
    {
        if (__instance is not MantraPower mantra)
            return;

        if (!Config.ScalingDivinityRequirement)
            return;

        int requirement =
            ScalingMantra.BaseRequirement;

        try
        {
            requirement =
                ScalingMantra.GetRequirement(
                    mantra.Owner.Player!);
        }
        catch
        {
            requirement =
                ScalingMantra.BaseRequirement;
        }

        LocString smartDescription =
            new(
                "powers",
                "WATCHERREBALANCE-MANTRA_SCALING.smartDescription");

        smartDescription.Add(
            "MantraRequirement",
            (decimal)requirement);

        __result =
            smartDescription;
    }
}
