using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Common;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class EmptyBodyPatch
{
    [HarmonyPatch(typeof(EmptyBody), MethodType.Constructor)]
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

        MethodInfo? withBlock =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithBlock",
                [typeof(int), typeof(int)]);

        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel constructor.");
        }

        if (withBlock == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithBlock.");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                // Common -> Uncommon
                ReplaceInt(
                    code,
                    i - 3,
                    (int)CardRarity.Uncommon);

                continue;
            }

            if (code[i].Calls(withBlock))
            {
                // Original: WithBlock(7, 3)
                // New:      WithBlock(5, 3)
                ReplaceInt(code, i - 2, 5);
                ReplaceInt(code, i - 1, 3);
            }
        }

        return code;
    }

    [HarmonyPatch(typeof(EmptyBody), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        EmptyBody __instance)
    {
        AddPlatingVar(__instance);
    }

    [HarmonyPatch(typeof(EmptyBody), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        EmptyBody __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result = NewOnPlay(
            __instance,
            ctx,
            cardPlay);

        return false;
    }
    
    // Glow if in stance
    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not EmptyBody card)
            return;

        __result = IsInStance(card);
    }

    private static async Task NewOnPlay(
        EmptyBody card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        // Always gain 5(8) Block.
        await CommonActions.CardBlock(
            card,
            cardPlay);

        // Only gain Plating if currently in a stance.
        if (IsInStance(card))
        {
            await CommonActions.ApplySelf<PlatingPower>(
                ctx,
                card);
        }

        // Exit after receiving the stance-dependent bonus.
        await StanceCmd.ExitStance(
            ctx,
            card.Owner,
            cardPlay.Card);
    }

    private static bool IsInStance(
        EmptyBody card)
    {
        return
            card.Owner.IsInWatcherStance<CalmStance>() ||
            card.Owner.IsInWatcherStance<WrathStance>() ||
            card.Owner.IsInWatcherStance<DivinityStance>();
    }

    private static void AddPlatingVar(
        EmptyBody card)
    {
        MethodInfo? withPower =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "WithPower" ||
                        !m.IsGenericMethodDefinition)
                    {
                        return false;
                    }

                    ParameterInfo[] parameters =
                        m.GetParameters();

                    return
                        parameters.Length == 3 &&
                        parameters[0].ParameterType == typeof(int) &&
                        parameters[1].ParameterType == typeof(int) &&
                        parameters[2].ParameterType == typeof(bool);
                });

        if (withPower == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel.WithPower<T>(int, int, bool).");
        }

        // Plating 2(3), with tooltip.
        withPower
            .MakeGenericMethod(typeof(PlatingPower))
            .Invoke(
                card,
                [2, 1, true]);
    }

    private static void ReplaceInt(
        List<CodeInstruction> code,
        int index,
        int value)
    {
        CodeInstruction original = code[index];

        var replacement =
            new CodeInstruction(
                OpCodes.Ldc_I4,
                value);

        replacement.labels.AddRange(original.labels);
        replacement.blocks.AddRange(original.blocks);

        code[index] = replacement;
    }
}