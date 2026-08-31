using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using Watcher.Code.Cards.Rare;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare;

[HarmonyPatch(
    typeof(MasterReality),
    MethodType.Constructor)]
public static class MasterRealityPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // =====================================================
        // REMOVE OLD COST UPGRADE
        // =====================================================
        //
        // Original:
        //
        //     WithCostUpgradeBy(-1)
        //
        // New:
        //
        //     WithCostUpgradeBy(0)
        //
        // So Master Reality remains 1 Energy after upgrading.
        // =====================================================

        MethodInfo? withCostUpgradeBy =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "WithCostUpgradeBy" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType ==
                    typeof(int));


        if (withCostUpgradeBy == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCostUpgradeBy(int).");
        }


        bool patchedCost = false;


        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withCostUpgradeBy))
                continue;


            ReplaceInt(
                code,
                i - 1,
                0);


            patchedCost = true;
            break;
        }


        if (!patchedCost)
        {
            throw new Exception(
                "WatcherRebalance: Could not remove Master Reality's cost upgrade.");
        }


        return code;
    }


    [HarmonyPostfix]
    private static void Postfix(
        MasterReality __instance)
    {
        // =====================================================
        // UPGRADE-ONLY INNATE
        // =====================================================

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
                "WatcherRebalance: Could not find ConstructedCardModel.WithKeyword.");
        }


        Type upgradeType =
            withKeyword
                .GetParameters()[1]
                .ParameterType;


        // ConstructedCardModel.UpgradeType.Add == 1
        object add =
            Enum.ToObject(
                upgradeType,
                1);


        withKeyword.Invoke(
            __instance,
            [
                CardKeyword.Innate,
                add
            ]);
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