using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Commands;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Starter;

[HarmonyPatch]
public static class EruptionPatch
{
    static MethodBase TargetMethod()
    {
        var eruptionType = AccessTools.TypeByName(
            "Watcher.Code.Cards.Basic.Eruption"
        );

        if (eruptionType == null)
            throw new Exception(
                "WatcherRebalance: Could not find Watcher.Code.Cards.Basic.Eruption"
            );

        var constructor = AccessTools.Constructor(eruptionType);

        if (constructor == null)
            throw new Exception(
                "WatcherRebalance: Could not find Eruption constructor"
            );

        return constructor;
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();

        var withDamage = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithDamage",
            new[] { typeof(int), typeof(int) }
        );

        var withCostUpgrade = AccessTools.Method(
            typeof(ConstructedCardModel),
            "WithCostUpgradeBy",
            new[] { typeof(int) }
        );

        if (withDamage == null)
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithDamage"
            );

        if (withCostUpgrade == null)
            throw new Exception(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCostUpgradeBy"
            );

        bool damagePatched = false;
        bool costPatched = false;

        for (int i = 0; i < code.Count; i++)
        {
            // Eruption:
            // WithDamage(9)
            //
            // Change the upgrade amount from 0 to 5.
            if (code[i].Calls(withDamage))
            {
                code[i - 1] =
                    new CodeInstruction(
                        OpCodes.Ldc_I4_5);

                damagePatched = true;
                continue;
            }

            // Eruption:
            // WithCostUpgradeBy(-1)
            //
            // Replace -1 with 0 so the upgrade no longer
            // reduces Eruption's Energy cost.
            if (code[i].Calls(withCostUpgrade))
            {
                code[i - 1] =
                    new CodeInstruction(
                        OpCodes.Ldc_I4_0);

                costPatched = true;
            }
        }

        if (!damagePatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Eruption damage upgrade"
            );
        }

        if (!costPatched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch Eruption cost upgrade"
            );
        }

        return code;
    }
}


// ============================================================================
// ERUPTION VFX + SFX
// ============================================================================
//
// Replaces Eruption's original OnPlay so we can explicitly set:
//
// VFX:
//     vfx/vfx_molten_fist
//
// SFX:
//     blunt_attack.mp3
//
// The original behaviour of entering Wrath after attacking is preserved.
// ============================================================================

[HarmonyPatch]
public static class EruptionAttackFxPatch
{
    static MethodBase TargetMethod()
    {
        var eruptionType =
            AccessTools.TypeByName(
                "Watcher.Code.Cards.Basic.Eruption");

        if (eruptionType == null)
        {
            throw new Exception(
                "WatcherRebalance: Could not find Watcher.Code.Cards.Basic.Eruption"
            );
        }

        return AccessTools.Method(
                   eruptionType,
                   "OnPlay")
               ?? throw new Exception(
                   "WatcherRebalance: Could not find Eruption.OnPlay"
               );
    }


    static bool Prefix(
        CardModel __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayEruption(
                __instance,
                __0,
                __1);

        return false;
    }


    private static async Task PlayEruption(
        CardModel card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_molten_fist",
                tmpSfx: "blunt_attack.mp3")
            .Execute(
                choiceContext);

        await StanceCmd.EnterWrath(
            choiceContext,
            card.Owner,
            cardPlay.Card);
    }
}