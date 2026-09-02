using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Rare;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;


[HarmonyPatch]
public static class LessonLearnedPatch
{
    // =========================================================
    // CONSTRUCTOR
    // =========================================================

    [HarmonyPatch(
        typeof(LessonLearned),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // ---------------------------------------------------------
        // Find WatcherCardModel constructor.
        // ---------------------------------------------------------

        ConstructorInfo? watcherCardConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]);


        // ---------------------------------------------------------
        // Find WithDamage(int, int).
        // ---------------------------------------------------------

        MethodInfo? withDamage =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithDamage" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                    typeof(int));


        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel constructor.");
        }


        if (withDamage == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithDamage(int, int).");
        }


        // =========================================================
        // APPLY CHANGES
        // =========================================================

        for (int i = 0; i < code.Count; i++)
        {
            // -----------------------------------------------------
            // COST
            //
            // Original:
            //
            //     base(
            //         2,
            //         CardType.Attack,
            //         CardRarity.Rare,
            //         TargetType.AnyEnemy)
            //
            // New:
            //
            //     base(
            //         3,
            //         CardType.Attack,
            //         CardRarity.Rare,
            //         TargetType.AnyEnemy)
            //
            // This is the exact same method used by the working
            // Mental Fortress patch.
            // -----------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                ReplaceInt(
                    code,
                    i - 5,
                    3);

                continue;
            }


            // -----------------------------------------------------
            // DAMAGE
            //
            // Original:
            //
            //     WithDamage(10, 3)
            //
            // New:
            //
            //     WithDamage(10, 0)
            //
            // So Lesson Learned stays at 10 damage when upgraded.
            // -----------------------------------------------------

            if (code[i].Calls(withDamage))
            {
                ReplaceInt(
                    code,
                    i - 1,
                    0);

                continue;
            }
        }


        return code;
    }


    // =========================================================
    // UPGRADE-ONLY RETAIN
    // =========================================================

    [HarmonyPatch(
        typeof(LessonLearned),
        MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        LessonLearned __instance)
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


        // UpgradeType.Add == 1
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


    // =========================================================
    // INTEGER REPLACEMENT
    // =========================================================
    //
    // Deliberately copied from the working Mental Fortress
    // implementation.
    // =========================================================

    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original =
            code[index];


        var replacement =
            new CodeInstruction(
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