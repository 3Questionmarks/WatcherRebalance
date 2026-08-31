using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Common;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon;

[HarmonyPatch]
public static class EmptyFistPatch
{
    // =========================================================
    // CONSTRUCTOR
    // =========================================================
    //
    // Original:
    //
    // Common
    // Deal 9(14) damage.
    //
    // Rebalanced:
    //
    // Uncommon
    // Deal 8(10) damage.
    //
    // =========================================================

    [HarmonyPatch(
        typeof(EmptyFist),
        MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction>
        ConstructorTranspiler(
            IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


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
                [
                    typeof(int),
                    typeof(int)
                ]);


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
            // -------------------------------------------------
            // Common -> Uncommon
            // -------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherCardConstructor)
            {
                ReplaceInt(
                    code,
                    i - 3,
                    (int)CardRarity.Uncommon);

                continue;
            }


            // -------------------------------------------------
            // Damage:
            //
            // Original: 9(14)
            // New:      8(10)
            //
            // WithDamage(base, upgradeAmount)
            // therefore:
            //
            // WithDamage(8, 2)
            // -------------------------------------------------

            if (code[i].Calls(withDamage))
            {
                ReplaceInt(
                    code,
                    i - 2,
                    8);

                ReplaceInt(
                    code,
                    i - 1,
                    2);
            }
        }


        return code;
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    [HarmonyPatch(
        typeof(EmptyFist),
        "OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        EmptyFist __instance,
        PlayerChoiceContext ctx,
        CardPlay cardPlay,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                ctx,
                cardPlay);


        return false;
    }


    private static async Task NewOnPlay(
        EmptyFist card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        // -----------------------------------------------------
        // Deal damage BEFORE leaving the current stance.
        //
        // This preserves Wrath's damage multiplier.
        // -----------------------------------------------------

        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_attack_slash")
            .Execute(ctx);


        // -----------------------------------------------------
        // Only gain Strength if Empty Fist actually exits
        // a stance.
        //
        // Base:
        //     Gain 1 Strength this turn.
        //
        // Upgrade:
        //     Gain 1 permanent Strength.
        // -----------------------------------------------------

        if (IsInStance(card))
        {
            if (card.IsUpgraded)
            {
                await PowerCmd.Apply<StrengthPower>(
                    ctx,
                    card.Owner.Creature,
                    1,
                    card.Owner.Creature,
                    card);
            }
            else
            {
                await PowerCmd.Apply<EmptyFistPower>(
                    ctx,
                    card.Owner.Creature,
                    1,
                    card.Owner.Creature,
                    card);
            }
        }


        // -----------------------------------------------------
        // Exit stance after damage + Strength.
        // -----------------------------------------------------

        await StanceCmd.ExitStance(
            ctx,
            card.Owner,
            cardPlay.Card);
    }


    // =========================================================
    // GOLD GLOW
    // =========================================================

    [HarmonyPatch(
        typeof(CardModel),
        "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;


        if (__instance is not EmptyFist card)
            return;


        __result =
            IsInStance(card);
    }


    // =========================================================
    // STANCE CHECK
    // =========================================================

    private static bool IsInStance(
        EmptyFist card)
    {
        return
            card.Owner
                .IsInWatcherStance<CalmStance>() ||

            card.Owner
                .IsInWatcherStance<WrathStance>() ||

            card.Owner
                .IsInWatcherStance<DivinityStance>();
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