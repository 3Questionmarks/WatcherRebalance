using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class WaveOfTheHandPatch
{
    // =========================================================
    // AMOUNT
    // =========================================================
    //
    // Original:
    //
    //     Apply 1(2) Weak.
    //
    // Rebalanced:
    //
    //     Apply 2 Weak.
    //
    // =========================================================

    [HarmonyPatch(
        typeof(WaveOfTheHand),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        ConstructorTranspiler(
            IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        MethodInfo? watcherWithPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[2].ParameterType ==
                        typeof(bool));


        if (watcherWithPower == null)
        {
            throw new MissingMethodException(
                "Could not find " +
                "WatcherCardModel.WithPower<T>(int, int, bool).");
        }


        MethodInfo waveWithPower =
            watcherWithPower
                .MakeGenericMethod(
                    typeof(WaveOfTheHandPower));


        bool patched =
            false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(waveWithPower))
                continue;


            // Original:
            //
            // WithPower<WaveOfTheHandPower>(
            //     1,
            //     1,
            //     false);
            //
            // New:
            //
            // WithPower<WaveOfTheHandPower>(
            //     2,
            //     0,
            //     false);

            ReplaceInt(
                code,
                i - 3,
                2);


            ReplaceInt(
                code,
                i - 2,
                0);


            patched =
                true;

            break;
        }


        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to patch " +
                "Wave of the Hand's Weak amount.");
        }


        return code;
    }


    // =========================================================
    // EXHAUST
    // =========================================================
    //
    // Base:
    //     Exhaust
    //
    // Upgrade:
    //     Remove Exhaust
    //
    // This is equivalent to:
    //
    // WithKeyword(
    //     CardKeyword.Exhaust,
    //     UpgradeType.Remove);
    //
    // UpgradeType is protected, so we invoke the builder
    // through reflection.
    // =========================================================

    [HarmonyPatch(
        typeof(WaveOfTheHand),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        WaveOfTheHand __instance)
    {
        AddRemovableExhaust(
            __instance);
    }


    private static void AddRemovableExhaust(
        WaveOfTheHand card)
    {
        MethodInfo? withKeyword =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "WithKeyword")
                        return false;


                    ParameterInfo[] parameters =
                        m.GetParameters();


                    return
                        parameters.Length == 2 &&
                        parameters[0].ParameterType ==
                            typeof(CardKeyword) &&
                        parameters[1].ParameterType.IsEnum;
                });


        if (withKeyword == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithKeyword.");
        }


        Type upgradeType =
            withKeyword
                .GetParameters()[1]
                .ParameterType;


        // ConstructedCardModel.UpgradeType:
        //
        // 0 = None
        // 1 = Add
        // 2 = Remove
        //
        // Confirmed from ConstructedCardModel source.

        object remove =
            Enum.ToObject(
                upgradeType,
                2);


        withKeyword.Invoke(
            card,
            [
                CardKeyword.Exhaust,
                remove
            ]);
    }


    // =========================================================
    // IL HELPER
    // =========================================================

    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
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