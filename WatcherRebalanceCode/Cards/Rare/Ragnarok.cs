using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Extensions;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Watcher.Code.Cards.Rare;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


// =============================================================
// RAGNAROK CONSTRUCTOR
// =============================================================

[HarmonyPatch(
    typeof(Ragnarok),
    MethodType.Constructor)]
public static class RagnarokConstructorPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        MethodInfo? withDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithDamage" &&
                    method.GetParameters().Length == 2 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(int) &&
                    method.GetParameters()[1].ParameterType ==
                    typeof(int));


        MethodInfo? withVars =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithVars" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(DynamicVar[]));


        MethodInfo? replacement =
            AccessTools.Method(
                typeof(RagnarokConstructorPatch),
                nameof(ReplaceRepeatVar));


        if (withDamage == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithDamage.");
        }


        if (withVars == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithVars.");
        }


        if (replacement == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find Ragnarok Repeat replacement.");
        }


        bool patchedDamage = false;
        bool patchedRepeat = false;


        for (int i = 0; i < code.Count; i++)
        {
            // =================================================
            // DAMAGE
            // =================================================
            //
            // Original:
            //
            //     WithDamage(5, 1)
            //
            // New:
            //
            //     WithDamage(5, 0)
            // =================================================

            if (code[i].Calls(withDamage))
            {
                ReplaceInt(
                    code,
                    i - 1,
                    0);

                patchedDamage = true;
                continue;
            }


            // =================================================
            // REPEAT
            // =================================================
            //
            // Original:
            //
            //     WithVars(
            //         new RepeatVar(5)
            //             .WithUpgrade(1))
            //
            // Replace the whole WithVars call with our helper.
            // =================================================

            if (code[i].Calls(withVars))
            {
                CodeInstruction original =
                    code[i];


                CodeInstruction newInstruction =
                    new(
                        OpCodes.Call,
                        replacement);


                newInstruction.labels.AddRange(
                    original.labels);

                newInstruction.blocks.AddRange(
                    original.blocks);


                code[i] =
                    newInstruction;


                patchedRepeat = true;
            }
        }


        if (!patchedDamage)
        {
            throw new Exception(
                "WatcherRebalance: Failed to remove Ragnarok's damage upgrade.");
        }


        if (!patchedRepeat)
        {
            throw new Exception(
                "WatcherRebalance: Failed to remove Ragnarok's Repeat upgrade.");
        }


        return code;
    }


    private static ConstructedCardModel ReplaceRepeatVar(
        ConstructedCardModel card,
        DynamicVar[] ignoredOriginalVars)
    {
        MethodInfo? withVars =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithVars" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(DynamicVar[]));


        if (withVars == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not invoke ConstructedCardModel.WithVars.");
        }


        DynamicVar[] vars =
        [
            new RepeatVar(5)
        ];


        object? result =
            withVars.Invoke(
                card,
                [vars]);


        if (result is not ConstructedCardModel constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: Unexpected result from ConstructedCardModel.WithVars.");
        }


        return constructedCard;
    }


    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original =
            code[index];


        CodeInstruction replacement =
            value switch
            {
                0 => new CodeInstruction(OpCodes.Ldc_I4_0),
                1 => new CodeInstruction(OpCodes.Ldc_I4_1),
                2 => new CodeInstruction(OpCodes.Ldc_I4_2),
                _ => new CodeInstruction(
                    OpCodes.Ldc_I4,
                    value)
            };


        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);


        code[index] =
            replacement;
    }
}


// =============================================================
// RAGNAROK ON PLAY
// =============================================================

[HarmonyPatch(
    typeof(Ragnarok),
    "OnPlay")]
public static class RagnarokOnPlayPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        Ragnarok __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                __0,
                __1);


        return false;
    }


    private static async Task NewOnPlay(
        Ragnarok card,
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // =====================================================
        // BASE
        // =====================================================
        //
        // Preserve the original random-enemy behavior.
        // =====================================================

        if (!card.IsUpgraded)
        {
            await CommonActions
                .CardAttack(
                    card,
                    cardPlay)
                .WithHitCount(5)
                .WithHitFx(
                    "vfx/vfx_attack_slash")
                .Execute(
                    choiceContext);


            return;
        }


        // =====================================================
        // UPGRADE
        // =====================================================
        //
        // Deal 5 damage to ALL enemies, five times.
        // =====================================================

        if (card.CombatState == null)
            return;


        await DamageCmd
            .Attack(
                card.DynamicVars.Damage.BaseValue)
            .WithHitCount(5)
            .FromCard(
                card,
                cardPlay)
            .TargetingAllOpponents(
                card.CombatState)
            .WithHitFx(
                "vfx/vfx_attack_slash")
            .Execute(
                choiceContext);
    }
}