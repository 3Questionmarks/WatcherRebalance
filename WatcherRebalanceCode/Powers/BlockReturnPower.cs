using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class BlockReturnPowerPatch
{
    private sealed class AttackState
    {
        public Creature? Attacker;
        public bool ResolvedOnDeath;
    }

    private static readonly Dictionary<BlockReturnPower, AttackState>
        AttackStates = new();


    // =========================================================
    // BEFORE ATTACK
    // =========================================================

    [HarmonyPatch(
        typeof(BlockReturnPower),
        "BeforeAttack")]
    [HarmonyPrefix]
    private static bool BeforeAttackPrefix(
        BlockReturnPower __instance,
        AttackCommand command,
        ref Task __result)
    {
        Creature? applier =
            __instance.Applier;

        if (applier == null)
        {
            __result = Task.CompletedTask;
            return false;
        }

        // Any teammate of the creature that originally applied
        // Talk to the Hand may trigger the debuff.
        Creature? attacker = command.Attacker;

        if (attacker != null &&
            attacker.Side == applier.Side)
        {
            AttackStates[__instance] =
                new AttackState
                {
                    Attacker = attacker,
                    ResolvedOnDeath = false
                };
        }
        else
        {
            AttackStates.Remove(__instance);
        }

        __result =
            Task.CompletedTask;

        return false;
    }


    // =========================================================
    // AFTER ATTACK
    // =========================================================

    [HarmonyPatch(
        typeof(BlockReturnPower),
        "AfterAttack")]
    [HarmonyPrefix]
    private static bool AfterAttackPrefix(
        BlockReturnPower __instance,
        PlayerChoiceContext choiceContext,
        AttackCommand command,
        ref Task __result)
    {
        __result =
            HandleAfterAttack(
                __instance,
                command);

        return false;
    }


    private static async Task HandleAfterAttack(
        BlockReturnPower power,
        AttackCommand command)
    {
        if (!AttackStates.TryGetValue(
                power,
                out AttackState? state))
        {
            return;
        }

        AttackStates.Remove(power);

        // If the marked enemy died during the attack,
        // AfterDeath already handled the Block.
        if (state.ResolvedOnDeath)
            return;

        Creature? attacker =
            state.Attacker;

        if (attacker == null)
            return;

        int hitCount =
            command.Results
                .SelectMany(result => result)
                .Count(result =>
                    result.Receiver == power.Owner);

        if (hitCount <= 0)
            return;

        // Preserve the original multi-hit behavior:
        // every hit against the marked enemy grants Block.
        for (int i = 0; i < hitCount; i++)
        {
            await CreatureCmd.GainBlock(
                attacker,
                power.Amount,
                ValueProp.Unpowered,
                null);
        }
    }


    // =========================================================
    // TARGET DIES DURING ATTACK
    // =========================================================

    [HarmonyPatch(
        typeof(BlockReturnPower),
        "AfterDeath")]
    [HarmonyPrefix]
    private static bool AfterDeathPrefix(
        BlockReturnPower __instance,
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength,
        ref Task __result)
    {
        __result =
            HandleAfterDeath(
                __instance,
                creature,
                wasRemovalPrevented);

        return false;
    }


    private static async Task HandleAfterDeath(
        BlockReturnPower power,
        Creature creature,
        bool wasRemovalPrevented)
    {
        if (wasRemovalPrevented)
            return;

        if (creature != power.Owner)
            return;

        if (!AttackStates.TryGetValue(
                power,
                out AttackState? state))
        {
            return;
        }

        Creature? attacker =
            state.Attacker;

        if (attacker == null)
            return;

        state.ResolvedOnDeath = true;

        await CreatureCmd.GainBlock(
            attacker,
            power.Amount,
            ValueProp.Unpowered,
            null);
    }
}