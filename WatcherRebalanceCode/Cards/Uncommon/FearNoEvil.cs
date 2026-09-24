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
            // -----------------------------------------------------
            // COST
            //
            // Original: 1
            // New:      2
            // -----------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                ReplaceInt(code, i - 5, 2);
                continue;
            }

            // -----------------------------------------------------
            // DAMAGE
            //
            // Original: WithDamage(8, 3)
            // New:      WithDamage(6, 0)
            // -----------------------------------------------------

            if (code[i].Calls(withDamage))
            {
                ReplaceInt(code, i - 2, 6);
                ReplaceInt(code, i - 1, 0);
            }
        }

        return code;
    }


    // =========================================================
    // CONSTRUCTOR ADDITIONS
    // =========================================================

    [HarmonyPatch(typeof(FearNoEvil), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void ConstructorPostfix(
        FearNoEvil __instance)
    {
        AddBlock(__instance);
        AddCostUpgrade(__instance);
        AddWrathTooltip(__instance);
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch(typeof(FearNoEvil), "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        FearNoEvil __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result = NewOnPlay(
            __instance,
            __0,
            __1);

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


        // =====================================================
        // STANCE EFFECT
        // =====================================================

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
            // Enter Wrath before attacking so the attack
            // benefits from Wrath's damage multiplier.
            await StanceCmd.EnterWrath(
                ctx,
                card.Owner,
                cardPlay.Card);
        }


        // =====================================================
        // ATTACK
        // =====================================================

        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_attack_blunt",
                tmpSfx: "blunt_attack.mp3")
            .Execute(
                ctx);
    }


    // =========================================================
    // BLOCK
    // =========================================================

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

        // 6 Block.
        // No Block increase on upgrade.
        withBlock.Invoke(
            card,
            [6, 0]);
    }


    // =========================================================
    // COST UPGRADE
    // =========================================================

    private static void AddCostUpgrade(
        FearNoEvil card)
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
                "Could not find ConstructedCardModel.WithCostUpgradeBy(int).");
        }

        // 2 Energy -> 1 Energy.
        withCostUpgradeBy.Invoke(
            card,
            [-1]);
    }


    // =========================================================
    // WRATH TOOLTIP
    // =========================================================

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


    // =========================================================
    // INTEGER REPLACEMENT
    // =========================================================

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