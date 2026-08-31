using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class FearNoEvilPatch
{
    [HarmonyPatch(typeof(FearNoEvil), MethodType.Constructor)]
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

        MethodInfo? withDamage =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithDamage",
                [typeof(int), typeof(int)]);

        if (watcherCardConstructor == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel constructor.");
        }

        if (withDamage == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithDamage.");
        }

        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                // Cost 1 -> 2
                ReplaceInt(code, i - 5, 2);
                continue;
            }

            if (code[i].Calls(withDamage))
            {
                // Original: WithDamage(8, 3)
                // New:      WithDamage(6, 3)
                ReplaceInt(code, i - 2, 6);
                ReplaceInt(code, i - 1, 3);
            }
        }

        return code;
    }

    [HarmonyPatch(typeof(FearNoEvil), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        FearNoEvil __instance)
    {
        AddBlock(__instance);
        AddWrathTooltip(__instance);
    }

    [HarmonyPatch(typeof(FearNoEvil), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        FearNoEvil __instance,
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

    private static async Task NewOnPlay(
        FearNoEvil card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        if (cardPlay.Target == null)
            return;

        bool hasAttackIntent =
            cardPlay.Target.Monster?.IntendsToAttack
            ?? false;

        if (hasAttackIntent)
        {
            // Attack intent:
            // Enter Calm and gain Block.
            await StanceCmd.EnterCalm(
                ctx,
                card.Owner,
                cardPlay.Card);

            await CommonActions.CardBlock(
                card,
                cardPlay);
        }
        else
        {
            // Non-attack intent:
            // Enter Wrath first so the attack benefits
            // from Wrath's damage multiplier.
            await StanceCmd.EnterWrath(
                ctx,
                card.Owner,
                cardPlay.Card);
        }

        // Always deal damage after the stance change.
        await CommonActions
            .CardAttack(card, cardPlay)
            .Execute(ctx);
    }

    private static void AddBlock(
        FearNoEvil card)
    {
        MethodInfo? withBlock =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithBlock",
                [typeof(int), typeof(int)]);

        if (withBlock == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithBlock.");
        }

        // 6(9) Block.
        withBlock.Invoke(
            card,
            [6, 3]);
    }

    private static void AddWrathTooltip(
        FearNoEvil card)
    {
        MethodInfo? withStanceTip =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithStanceTip" &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 0);

        if (withStanceTip == null)
        {
            throw new MissingMethodException(
                "Could not find WatcherCardModel.WithStanceTip.");
        }

        withStanceTip
            .MakeGenericMethod(typeof(WrathStance))
            .Invoke(card, null);
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