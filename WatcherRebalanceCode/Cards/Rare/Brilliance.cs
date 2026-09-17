using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Rare;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;

[HarmonyPatch]
public static class BrilliancePatch
{
    /*
     * =========================================================
     * BRILLIANCE REBALANCE
     * =========================================================
     *
     * Original:
     *
     * 1 Energy
     * Deal 12(16) damage.
     * Additional damage equal to Mantra gained this combat.
     *
     * Rebalanced:
     *
     * 2(1) Energy
     * Deal 1 damage 2 times.
     * Each hit deals additional damage equal to
     * Mantra gained this combat.
     *
     * No Retain.
     *
     * CalculatedDamage:
     *     1 + Mantra gained this combat
     *
     * Repeat:
     *     2
     *
     * Upgrade:
     *     Reduce cost by 1.
     * =========================================================
     */

    // =========================================================
    // COST
    // =========================================================

    [HarmonyPatch(
        typeof(Brilliance),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        CostTranspiler(
            IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();

        ConstructorInfo? watcherConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]);

        if (watcherConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "WatcherCardModel constructor.");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor ||
                constructor != watcherConstructor)
            {
                continue;
            }

            if (i < 5)
            {
                throw new Exception(
                    "WatcherRebalance: Invalid Brilliance constructor IL.");
            }

            // Original:
            //     1 Energy
            //
            // New:
            //     2 Energy

            ReplaceInt(
                code,
                i - 5,
                2);

            return code;
        }

        throw new Exception(
            "WatcherRebalance: Failed to patch Brilliance's Energy cost.");
    }

    // =========================================================
    // CALCULATED DAMAGE
    // =========================================================

    [HarmonyPatch(
        typeof(Brilliance),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        DamageTranspiler(
            IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();

        MethodInfo? withCalculatedDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithCalculatedDamage" &&
                    m.GetParameters().Length == 5 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(Func<CardModel, Creature?, decimal>) &&
                    m.GetParameters()[2].ParameterType ==
                        typeof(ValueProp) &&
                    m.GetParameters()[3].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[4].ParameterType ==
                        typeof(int));

        MethodInfo? replacement =
            AccessTools.Method(
                typeof(BrilliancePatch),
                nameof(ReplaceCalculatedDamage));

        if (withCalculatedDamage == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithCalculatedDamage.");
        }

        if (replacement == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "BrilliancePatch.ReplaceCalculatedDamage.");
        }

        bool patched =
            false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withCalculatedDamage))
                continue;

            CodeInstruction newInstruction =
                new(
                    OpCodes.Call,
                    replacement);

            newInstruction.labels.AddRange(
                code[i].labels);

            newInstruction.blocks.AddRange(
                code[i].blocks);

            code[i] =
                newInstruction;

            patched =
                true;

            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch " +
                "Brilliance's calculated damage.");
        }

        return code;
    }

    // =========================================================
    // REPLACE CALCULATED DAMAGE
    // =========================================================

    private static ConstructedCardModel
        ReplaceCalculatedDamage(
            ConstructedCardModel card,
            int originalBase,
            Func<CardModel, Creature?, decimal> bonus,
            ValueProp props,
            int originalUpgrade,
            int bonusUpgrade)
    {
        MethodInfo? withCalculatedDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithCalculatedDamage" &&
                    m.GetParameters().Length == 5 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(Func<CardModel, Creature?, decimal>) &&
                    m.GetParameters()[2].ParameterType ==
                        typeof(ValueProp) &&
                    m.GetParameters()[3].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[4].ParameterType ==
                        typeof(int));

        if (withCalculatedDamage == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not invoke " +
                "ConstructedCardModel.WithCalculatedDamage.");
        }

        object? result =
            withCalculatedDamage.Invoke(
                card,
                [
                    1,
                    bonus,
                    props,
                    0,
                    0
                ]);

        if (result is not ConstructedCardModel
            constructedCard)
        {
            throw new Exception(
                "WatcherRebalance: " +
                "WithCalculatedDamage returned " +
                "an unexpected result.");
        }

        return constructedCard;
    }

    // =========================================================
    // CONSTRUCTOR POSTFIX
    // =========================================================

    [HarmonyPatch(
        typeof(Brilliance),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Brilliance __instance)
    {
        AddRepeat(
            __instance);

        AddCostUpgrade(
            __instance);
    }

    // =========================================================
    // REPEAT
    // =========================================================

    private static void AddRepeat(
        Brilliance card)
    {
        MethodInfo? withVar =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithVar" &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(string) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[2].ParameterType ==
                        typeof(int));

        if (withVar == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithVar(string, int, int).");
        }

        withVar.Invoke(
            card,
            [
                "Repeat",
                2,
                0
            ]);
    }

    // =========================================================
    // COST UPGRADE
    // =========================================================

    private static void AddCostUpgrade(
        Brilliance card)
    {
        MethodInfo? withCostUpgradeBy =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithCostUpgradeBy" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(int));

        if (withCostUpgradeBy == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithCostUpgradeBy(int).");
        }

        withCostUpgradeBy.Invoke(
            card,
            [-1]);
    }

    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch(
        typeof(Brilliance),
        "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Brilliance __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayRebalancedBrilliance(
                __instance,
                __0,
                __1);

        return false;
    }

    private static async Task
        PlayRebalancedBrilliance(
            Brilliance card,
            PlayerChoiceContext choiceContext,
            CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
            return;

        int hitCount =
            card.DynamicVars["Repeat"]
                .IntValue;

        // Use the Watcher's normal Mantra-gain sound.
        SfxCmd.Play(
            "res://Watcher/audio/mantra_gain.ogg");

        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_starry_impact",
                tmpSfx: "blunt_attack.mp3")
            .WithHitCount(
                hitCount)
            .Execute(
                choiceContext);
    }

    // =========================================================
    // IL HELPERS
    // =========================================================

    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        if (index < 0 ||
            index >= code.Count)
        {
            throw new Exception(
                "WatcherRebalance: Invalid IL index while " +
                "patching Brilliance.");
        }

        CodeInstruction original =
            code[index];

        CodeInstruction replacement =
            value switch
            {
                -1 => new CodeInstruction(
                    OpCodes.Ldc_I4_M1),

                0 => new CodeInstruction(
                    OpCodes.Ldc_I4_0),

                1 => new CodeInstruction(
                    OpCodes.Ldc_I4_1),

                2 => new CodeInstruction(
                    OpCodes.Ldc_I4_2),

                3 => new CodeInstruction(
                    OpCodes.Ldc_I4_3),

                4 => new CodeInstruction(
                    OpCodes.Ldc_I4_4),

                5 => new CodeInstruction(
                    OpCodes.Ldc_I4_5),

                6 => new CodeInstruction(
                    OpCodes.Ldc_I4_6),

                7 => new CodeInstruction(
                    OpCodes.Ldc_I4_7),

                8 => new CodeInstruction(
                    OpCodes.Ldc_I4_8),

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