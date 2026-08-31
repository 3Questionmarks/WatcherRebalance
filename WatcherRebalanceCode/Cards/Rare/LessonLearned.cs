using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Cards.Rare;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


[HarmonyPatch(
    typeof(LessonLearned),
    MethodType.Constructor)]
public static class LessonLearnedPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
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


        if (withDamage == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithDamage(int, int).");
        }


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withDamage))
                continue;


            // Original:
            //
            //     WithDamage(10, 3)
            //
            // New:
            //
            //     WithDamage(10, 0)
            //
            ReplaceInt(
                code,
                i - 1,
                0);


            break;
        }


        return code;
    }


    // =========================================================
    // UPGRADE-ONLY RETAIN
    // =========================================================
    //
    // Add:
    //
    //     WithKeyword(Retain, UpgradeType.Add)
    //
    // UpgradeType is protected inside ConstructedCardModel,
    // so locate the helper reflectively and pass enum value 1.
    //
    // UpgradeType.Add == 1
    // =========================================================

    [HarmonyPostfix]
    private static void ConstructorPostfix(
        LessonLearned __instance)
    {
        MethodInfo? withKeyword =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "WithKeyword")
                        return false;


                    ParameterInfo[] parameters =
                        method.GetParameters();


                    return
                        parameters.Length == 2 &&
                        parameters[0].ParameterType ==
                        typeof(CardKeyword);
                });


        if (withKeyword == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithKeyword(CardKeyword, UpgradeType).");
        }


        Type upgradeType =
            withKeyword
                .GetParameters()[1]
                .ParameterType;


        object addUpgrade =
            Enum.ToObject(
                upgradeType,
                1);


        withKeyword.Invoke(
            __instance,
            [
                CardKeyword.Retain,
                addUpgrade
            ]);
    }


    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        OpCode opcode =
            value switch
            {
                0 => OpCodes.Ldc_I4_0,
                1 => OpCodes.Ldc_I4_1,
                2 => OpCodes.Ldc_I4_2,
                3 => OpCodes.Ldc_I4_3,
                4 => OpCodes.Ldc_I4_4,
                5 => OpCodes.Ldc_I4_5,
                6 => OpCodes.Ldc_I4_6,
                7 => OpCodes.Ldc_I4_7,
                8 => OpCodes.Ldc_I4_8,
                _ => OpCodes.Ldc_I4
            };


        object? operand =
            value is >= 0 and <= 8
                ? null
                : value;


        CodeInstruction replacement =
            new(
                opcode,
                operand);


        replacement.labels.AddRange(
            code[index].labels);

        replacement.blocks.AddRange(
            code[index].blocks);


        code[index] =
            replacement;
    }
}