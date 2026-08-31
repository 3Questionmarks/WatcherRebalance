using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token;

[HarmonyPatch(typeof(Beta))]
public static class BetaPatch
{
    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();

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
                "WatcherRebalance: Could not find ConstructedCardModel.WithCostUpgradeBy.");
        }

        bool patched = false;

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withCostUpgradeBy))
                continue;

            // Original:
            // WithCostUpgradeBy(-1)
            //
            // New:
            // WithCostUpgradeBy(0)
            ReplaceInt(
                code,
                i - 1,
                0);

            patched = true;
            break;
        }

        if (!patched)
        {
            throw new Exception(
                "WatcherRebalance: Failed to remove Beta's cost upgrade.");
        }

        return code;
    }

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        Beta __instance)
    {
        AddDraw(__instance);
        AddUpgradeRetain(__instance);
    }

    private static void AddDraw(
        Beta card)
    {
        MethodInfo? withCards =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithCards" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                    typeof(int));

        if (withCards == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithCards.");
        }

        withCards.Invoke(
            card,
            [
                2,
                0
            ]);
    }

    private static void AddUpgradeRetain(
        Beta card)
    {
        MethodInfo? withKeyword =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithKeyword" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType ==
                    typeof(CardKeyword));

        if (withKeyword == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ConstructedCardModel.WithKeyword.");
        }

        Type upgradeType =
            withKeyword.GetParameters()[1].ParameterType;

        // UpgradeType.Add = 1.
        object add =
            Enum.ToObject(
                upgradeType,
                1);

        withKeyword.Invoke(
            card,
            [
                CardKeyword.Retain,
                add
            ]);
    }

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        Beta __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            PlayRebalancedBeta(
                __instance,
                __0);

        return false;
    }

    private static async Task PlayRebalancedBeta(
        Beta card,
        PlayerChoiceContext choiceContext)
    {
        await WatcherCmd.GiveCard<Omega>(
            card.Owner,
            PileType.Draw,
            CardPilePosition.Random);

        await CommonActions.Draw(
            card,
            choiceContext);
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

        code[index] =
            replacement;
    }
}