using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Events;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Patches;


// ============================================================================
// WATCHER STANCE CONFIGURATION
// ============================================================================
//
// Every main Watcher stance has:
//
// - Energy on Enter
// - Energy on Exit
// - Damage Dealt Multiplier
// - Damage Taken Multiplier
//
// Defaults reproduce the original Watcher behaviour.
//
// All values are read at runtime.
// ============================================================================


// ============================================================================
// CALM EXIT ENERGY
// ============================================================================
//
// Original Calm:
//
//     WatcherHook.ModifyCalmEnergyGain(..., 2)
//
// We replace only the original base value 2 with:
//
//     Config.CalmEnergyOnExit
//
// This preserves Watcher's existing ModifyCalmEnergyGain hook.
// ============================================================================

[HarmonyPatch]
public static class CalmExitEnergyConfigPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method =
            AccessTools.Method(
                typeof(CalmStance),
                nameof(CalmStance.OnExitStance));

        if (method == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find CalmStance.OnExitStance.");
        }

        AsyncStateMachineAttribute? stateMachine =
            method.GetCustomAttribute<AsyncStateMachineAttribute>();

        if (stateMachine == null)
        {
            throw new Exception(
                "WatcherRebalance: CalmStance.OnExitStance has no async state machine.");
        }

        MethodInfo? moveNext =
            AccessTools.Method(
                stateMachine.StateMachineType,
                "MoveNext");

        if (moveNext == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find CalmStance.OnExitStance state-machine MoveNext.");
        }

        return moveNext;
    }


    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        MethodInfo? modifyCalmEnergyGain =
            AccessTools.Method(
                typeof(Watcher.Code.Events.WatcherHook),
                nameof(Watcher.Code.Events.WatcherHook.ModifyCalmEnergyGain));


        MethodInfo? configGetter =
            AccessTools.PropertyGetter(
                typeof(Config),
                nameof(Config.CalmEnergyOnExit));


        if (modifyCalmEnergyGain == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherHook.ModifyCalmEnergyGain.");
        }


        if (configGetter == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find Config.CalmEnergyOnExit getter.");
        }


        bool patched =
            false;


        for (int i = 0; i < code.Count; i++)
        {
            // We specifically want:
            //
            // WatcherHook.ModifyCalmEnergyGain(
            //     combatState,
            //     player,
            //     2)
            //
            // So find the ModifyCalmEnergyGain call and replace
            // its immediately preceding integer argument.

            if (!code[i].Calls(modifyCalmEnergyGain))
                continue;


            if (i < 1)
            {
                throw new Exception(
                    "WatcherRebalance: Invalid CalmStance.OnExitStance IL.");
            }


            CodeInstruction original =
                code[i - 1];


            CodeInstruction replacement =
                new(
                    System.Reflection.Emit.OpCodes.Call,
                    configGetter);


            replacement.labels.AddRange(
                original.labels);

            replacement.blocks.AddRange(
                original.blocks);


            code[i - 1] =
                replacement;


            patched =
                true;

            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Calm exit Energy.");
        }


        return code;
    }
}


// ============================================================================
// DIVINITY ENTER ENERGY
// ============================================================================
//
// Original:
//
//     player.PlayerCombatState!.GainEnergy(3)
//
// New:
//
//     player.PlayerCombatState!.GainEnergy(
//         Config.DivinityEnergyOnEnter)
// ============================================================================

[HarmonyPatch(
    typeof(DivinityStance),
    nameof(DivinityStance.OnEnterStance))]
public static class DivinityEnterEnergyConfigPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Player __1)
    {
        var combatState =
            __1.PlayerCombatState;

        if (combatState == null)
            return;


        int difference =
            Config.DivinityEnergyOnEnter - 3;


        // -----------------------------------------------------
        // MORE THAN VANILLA
        // -----------------------------------------------------

        if (difference > 0)
        {
            combatState.GainEnergy(
                difference);

            return;
        }


        // -----------------------------------------------------
        // LESS THAN VANILLA
        // -----------------------------------------------------

        if (difference < 0)
        {
            combatState.LoseEnergy(
                -difference);
        }
    }
}


// ============================================================================
// ADDITIONAL ENTER ENERGY
// ============================================================================
//
// Calm and Wrath do not have native Energy-on-entry effects.
//
// WatcherStanceModel.OnEnterStance is called for all three stances.
//
// Divinity is skipped here because its native GainEnergy call was already
// patched above.
// ============================================================================

[HarmonyPatch(
    typeof(WatcherStanceModel),
    nameof(WatcherStanceModel.OnEnterStance))]
public static class StanceEnterEnergyConfigPatch
{
    [HarmonyPostfix]
    private static async Task Postfix(
        Task __result,
        WatcherStanceModel __instance,
        Player __1)
    {
        await __result;


        int energy =
            __instance switch
            {
                CalmStance =>
                    Config.CalmEnergyOnEnter,

                WrathStance =>
                    Config.WrathEnergyOnEnter,

                // Divinity's native GainEnergy is patched directly.
                DivinityStance =>
                    0,

                _ =>
                    0
            };


        if (energy <= 0)
            return;


        await PlayerCmd.GainEnergy(
            energy,
            __1);
    }
}


// ============================================================================
// ADDITIONAL EXIT ENERGY
// ============================================================================
//
// Calm is skipped because its existing exit-Energy implementation is patched
// directly above, preserving WatcherHook.ModifyCalmEnergyGain.
//
// Wrath and Divinity inherit WatcherStanceModel.OnExitStance, so we add their
// configurable exit Energy here.
// ============================================================================

[HarmonyPatch(
    typeof(WatcherStanceModel),
    nameof(WatcherStanceModel.OnExitStance))]
public static class StanceExitEnergyConfigPatch
{
    [HarmonyPostfix]
    private static async Task Postfix(
        Task __result,
        WatcherStanceModel __instance,
        Player __1)
    {
        await __result;


        int energy =
            __instance switch
            {
                // Calm is handled by its native patched method.
                CalmStance =>
                    0,

                WrathStance =>
                    Config.WrathEnergyOnExit,

                DivinityStance =>
                    Config.DivinityEnergyOnExit,

                _ =>
                    0
            };


        if (energy <= 0)
            return;


        await PlayerCmd.GainEnergy(
            energy,
            __1);
    }
}


// ============================================================================
// DAMAGE MULTIPLIERS
// ============================================================================
//
// We DO NOT replace Hook.ModifyDamage.
//
// Instead, the original game calculates damage normally and then we adjust
// the native stance contribution:
//
// Calm:
//     Native ×1 -> configured multiplier.
//
// Wrath outgoing:
//     Native = 2 + WatcherHook.ModifyWrathDamage(...)
//     Desired = configured Wrath multiplier + same Watcher hook.
//
// Wrath incoming:
//     Native ×2 -> configured multiplier.
//
// Divinity outgoing:
//     Native ×3 -> configured multiplier.
//
// Divinity incoming:
//     Native ×1 -> configured multiplier.
//
// Using desired / native preserves every other damage multiplier.
// ============================================================================

[HarmonyPatch(
    typeof(Hook),
    nameof(Hook.ModifyDamage))]
public static class WatcherStanceDamageConfigPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        ICombatState? combatState,
        Creature? target,
        Creature? dealer,
        ValueProp props,
        ModifyDamageHookType modifyDamageHookType,
        ref decimal __result)
    {
        // Stances only affect normal powered damage.
        if (props.HasFlag(ValueProp.Unpowered))
            return;


        if (!modifyDamageHookType.HasFlag(
                ModifyDamageHookType.Multiplicative))
        {
            return;
        }


        // =====================================================
        // OUTGOING DAMAGE
        // =====================================================

        Player? dealerPlayer =
            dealer?.Player;


        if (dealerPlayer != null)
        {
            decimal nativeMultiplier =
                StanceConfigMath.GetNativeOutgoingMultiplier(
                    dealerPlayer,
                    combatState);


            decimal configuredMultiplier =
                StanceConfigMath.GetConfiguredOutgoingMultiplier(
                    dealerPlayer,
                    combatState);


            ApplyRatio(
                ref __result,
                nativeMultiplier,
                configuredMultiplier);
        }


        // =====================================================
        // INCOMING DAMAGE
        // =====================================================

        Player? targetPlayer =
            target?.Player;


        if (targetPlayer != null)
        {
            decimal nativeMultiplier =
                StanceConfigMath.GetNativeIncomingMultiplier(
                    targetPlayer);


            decimal configuredMultiplier =
                StanceConfigMath.GetConfiguredIncomingMultiplier(
                    targetPlayer);


            ApplyRatio(
                ref __result,
                nativeMultiplier,
                configuredMultiplier);
        }
    }


    private static void ApplyRatio(
        ref decimal result,
        decimal nativeMultiplier,
        decimal configuredMultiplier)
    {
        // No stance effect.
        if (nativeMultiplier == 1m &&
            configuredMultiplier == 1m)
        {
            return;
        }


        // Defensive protection against division by zero.
        if (nativeMultiplier == 0m)
            return;


        result *=
            configuredMultiplier /
            nativeMultiplier;
    }
}


// ============================================================================
// SHARED STANCE MULTIPLIER MATH
// ============================================================================
//
// Used both by the damage configuration patch and by the Strength scaling
// patch.
// ============================================================================

internal static class StanceConfigMath
{
    public static decimal GetNativeOutgoingMultiplier(
        Player player,
        ICombatState? combatState)
    {
        if (player.IsInWatcherStance<CalmStance>())
            return 1m;


        if (player.IsInWatcherStance<WrathStance>())
        {
            if (combatState == null)
                return 1m;


            decimal wrathHook =
                WatcherHook.ModifyWrathDamage(
                    combatState,
                    player,
                    0);


            return
                2m +
                wrathHook;
        }


        if (player.IsInWatcherStance<DivinityStance>())
            return 3m;


        return 1m;
    }


    public static decimal GetConfiguredOutgoingMultiplier(
        Player player,
        ICombatState? combatState)
    {
        if (player.IsInWatcherStance<CalmStance>())
        {
            return
                (decimal)Config.CalmDamageMultiplier;
        }


        if (player.IsInWatcherStance<WrathStance>())
        {
            if (combatState == null)
                return 1m;


            decimal wrathHook =
                WatcherHook.ModifyWrathDamage(
                    combatState,
                    player,
                    0);


            return
                (decimal)Config.WrathDamageMultiplier +
                wrathHook;
        }


        if (player.IsInWatcherStance<DivinityStance>())
        {
            return
                (decimal)Config.DivinityDamageMultiplier;
        }


        return 1m;
    }


    public static decimal GetNativeIncomingMultiplier(
        Player player)
    {
        if (player.IsInWatcherStance<WrathStance>())
            return 2m;


        // Calm and Divinity normally take ×1.
        return 1m;
    }


    public static decimal GetConfiguredIncomingMultiplier(
        Player player)
    {
        if (player.IsInWatcherStance<CalmStance>())
        {
            return
                (decimal)Config.CalmDamageTakenMultiplier;
        }


        if (player.IsInWatcherStance<WrathStance>())
        {
            return
                (decimal)Config.WrathDamageTakenMultiplier;
        }


        if (player.IsInWatcherStance<DivinityStance>())
        {
            return
                (decimal)Config.DivinityDamageTakenMultiplier;
        }


        return 1m;
    }
}