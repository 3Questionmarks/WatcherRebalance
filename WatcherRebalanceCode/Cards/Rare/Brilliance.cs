using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
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
     * 2 Energy
     * Deal 1 damage 2(3) times.
     * Each hit deals additional damage equal to
     * Mantra gained this combat.
     *
     * No Retain.
     *
     * Therefore:
     *
     * CalculatedDamage =
     *     1 + Mantra gained this combat
     *
     * Repeat =
     *     2(3)
     * =========================================================
     */


    // =========================================================
    // COST
    // =========================================================
    //
    // Original:
    //     1 Energy
    //
    // Rebalanced:
    //     2 Energy
    //
    // The upgrade does not change the cost.
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


        // -----------------------------------------------------
        // Find WatcherCardModel's constructor:
        //
        // WatcherCardModel(
        //     int cost,
        //     CardType type,
        //     CardRarity rarity,
        //     TargetType target,
        //     bool shouldShowInCardLibrary = true)
        //
        // Brilliance originally passes 1 as its cost.
        // -----------------------------------------------------

        ConstructorInfo? watcherConstructor =
            typeof(WatcherCardModel)
                .GetConstructors(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .FirstOrDefault(c =>
                {
                    ParameterInfo[] parameters =
                        c.GetParameters();

                    return
                        parameters.Length == 5 &&
                        parameters[0].ParameterType == typeof(int) &&
                        parameters[1].ParameterType == typeof(CardType) &&
                        parameters[2].ParameterType == typeof(CardRarity) &&
                        parameters[3].ParameterType == typeof(TargetType) &&
                        parameters[4].ParameterType == typeof(bool);
                });


        if (watcherConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "WatcherCardModel(int, CardType, CardRarity, TargetType, bool).");
        }


        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is not ConstructorInfo constructor ||
                constructor != watcherConstructor)
            {
                continue;
            }


            // Immediately before the constructor call:
            //
            // i - 5 = cost
            // i - 4 = CardType
            // i - 3 = CardRarity
            // i - 2 = TargetType
            // i - 1 = shouldShowInCardLibrary
            //
            // Change:
            //     cost 1
            //
            // to:
            //     cost 2

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


        // -----------------------------------------------------
        // Locate:
        //
        // WithCalculatedDamage(
        //     int,
        //     Func<CardModel, Creature?, decimal>,
        //     ValueProp,
        //     int,
        //     int)
        // -----------------------------------------------------

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


            // Replace:
            //
            // card.WithCalculatedDamage(
            //     12,
            //     MantraGainedThisCombat,
            //     ValueProp.Move,
            //     4,
            //     0)
            //
            // with our helper.
            //
            // The helper keeps the original Mantra callback,
            // but changes the base/upgrade values.

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


        // -----------------------------------------------------
        // New calculated damage:
        //
        // Base:
        //     1 + Mantra gained
        //
        // Upgrade:
        //     still 1 + Mantra gained
        //
        // The upgrade improves Repeat instead.
        // -----------------------------------------------------

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
    // ADD REPEAT 2(3)
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
    }


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


        // Base:
        //     2 hits
        //
        // Upgrade:
        //     +1 hit = 3

        withVar.Invoke(
            card,
            [
                "Repeat",
                2,
                1
            ]);
    }


    // =========================================================
    // ON PLAY
    // =========================================================
    //
    // Every hit uses CalculatedDamage:
    //
    //     1 + Mantra gained this combat
    //
    // Hit count:
    //
    //     2(3)
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


        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_attack_slash")
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
            new(
                OpCodes.Ldc_I4,
                value);


        replacement.labels.AddRange(
            original.labels);

        replacement.blocks.AddRange(
            original.blocks);


        code[index] =
            replacement;
    }
}