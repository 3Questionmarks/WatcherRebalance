using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class MentalFortressPatch
{
    [HarmonyPatch(typeof(MentalFortress), MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code = instructions.ToList();

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

        MethodInfo? withPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithPower" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[0].ParameterType == typeof(int) &&
                    m.GetParameters()[1].ParameterType == typeof(int) &&
                    m.GetParameters()[2].ParameterType == typeof(bool));

        MethodInfo? withTip =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithTip" &&
                    m.GetParameters().Length == 1);

        MethodInfo? removeTip =
            AccessTools.Method(
                typeof(MentalFortressPatch),
                nameof(RemoveOriginalBlockTip));

        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel constructor.");
        }

        if (withPower == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel.WithPower<T>(int, int, bool).");
        }

        if (withTip == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithTip.");
        }

        if (removeTip == null)
        {
            throw new MissingMethodException(
                "Could not find RemoveOriginalBlockTip.");
        }

        MethodInfo mentalFortressWithPower =
            withPower.MakeGenericMethod(
                typeof(MentalFortressPower));

        for (int i = 0; i < code.Count; i++)
        {
            // Base cost:
            //
            // Original Mental Fortress:
            // 1 Energy
            //
            // New:
            // 2 Energy
            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                ReplaceInt(
                    code,
                    i - 5,
                    2);

                continue;
            }

            // Original:
            // WithPower<MentalFortressPower>(4, 2, false)
            //
            // New:
            // WithPower<MentalFortressPower>(1, 0, false)
            //
            // Amount now represents how many Token cards
            // are returned each turn.
            if (code[i].Calls(mentalFortressWithPower))
            {
                ReplaceInt(
                    code,
                    i - 3,
                    1);

                ReplaceInt(
                    code,
                    i - 2,
                    0);

                continue;
            }

            // Original Mental Fortress explicitly adds
            // a Block tooltip.
            //
            // The new version no longer has anything to do
            // with Block, so remove that tooltip completely.
            if (code[i].Calls(withTip))
            {
                CodeInstruction original =
                    code[i];

                var replacement =
                    new CodeInstruction(
                        OpCodes.Call,
                        removeTip);

                replacement.labels.AddRange(
                    original.labels);

                replacement.blocks.AddRange(
                    original.blocks);

                code[i] = replacement;
            }
        }

        return code;
    }

    [HarmonyPatch(typeof(MentalFortress), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        MentalFortress __instance)
    {
        // Upgrade:
        // 2 Energy -> 1 Energy
        MethodInfo? withCostUpgradeBy =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithCostUpgradeBy" &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(int));

        if (withCostUpgradeBy == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithCostUpgradeBy(int).");
        }

        withCostUpgradeBy.Invoke(
            __instance,
            [-1]);

        WatcherRebalanceTips.AddTokenTip(__instance);
    }

    private static ConstructedCardModel RemoveOriginalBlockTip(
        ConstructedCardModel card,
        TooltipSource ignored)
    {
        // Consume the original tooltip argument and simply
        // return the card without registering it.
        return card;
    }

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

        code[index] = replacement;
    }
}