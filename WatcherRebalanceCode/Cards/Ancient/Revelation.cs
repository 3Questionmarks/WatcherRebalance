using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Ancient;
using Watcher.Code.Commands;
using Watcher.Code.Powers;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Ancient;


// ============================================================================
// REVELATION
// ============================================================================
//
// Final card:
//
// Revelation
// Ancient Attack
// 1 Energy
//
// Deal 15(20) damage.
// Enter Wrath.
// Gain 3(5) Mantra.
//
// Changes from original:
//
// - Cost: 2 -> 1
// - Damage: 12 -> 15(20)
// - Divinity -> Wrath
// - Removes Exhaust completely
// - Removes the old -1 Energy upgrade
// - Adds 3(5) Mantra
// ============================================================================

[HarmonyPatch(typeof(AncientCard2))]
public static class RevelationPatch
{
    // ========================================================================
    // CONSTRUCTOR
    // ========================================================================

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> code =
            instructions.ToList();


        // --------------------------------------------------------------------
        // WatcherCardModel constructor
        //
        // Original:
        // 2 Energy
        //
        // New:
        // 1 Energy
        // --------------------------------------------------------------------

        ConstructorInfo? watcherConstructor =
            AccessTools.Constructor(
                typeof(WatcherCardModel),
                [
                    typeof(int),
                    typeof(CardType),
                    typeof(CardRarity),
                    typeof(TargetType),
                    typeof(bool)
                ]);

        if (watcherConstructor == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find WatcherCardModel constructor.");
        }


        // --------------------------------------------------------------------
        // WithDamage(int baseVal, int upgrade)
        //
        // Original:
        // WithDamage(12)
        //
        // Compiles as:
        // WithDamage(12, 0)
        //
        // New:
        // WithDamage(15, 5)
        // --------------------------------------------------------------------

        MethodInfo? withDamage =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithDamage",
                [
                    typeof(int),
                    typeof(int)
                ]);

        if (withDamage == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithDamage(int, int).");
        }


        // --------------------------------------------------------------------
        // Original:
        //
        // WithKeywords(CardKeyword.Exhaust)
        //
        // We replace the call entirely.
        //
        // Our helper consumes the original CardKeyword[] but does NOT add
        // Exhaust. It also adds the new Mantra variable while the card is
        // still being constructed.
        // --------------------------------------------------------------------

        MethodInfo? withKeywords =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithKeywords",
                [
                    typeof(CardKeyword[])
                ]);

        if (withKeywords == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithKeywords(CardKeyword[]).");
        }


        MethodInfo? replaceKeywords =
            AccessTools.Method(
                typeof(RevelationPatch),
                nameof(ReplaceKeywordsWithMantra));

        if (replaceKeywords == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ReplaceKeywordsWithMantra.");
        }


        // --------------------------------------------------------------------
        // Original stance tooltip:
        //
        // WithStanceTip<DivinityStance>()
        //
        // Replace with:
        //
        // WithStanceTip<WrathStance>()
        // --------------------------------------------------------------------

        MethodInfo? stanceTipDefinition =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithStanceTip" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 0);

        if (stanceTipDefinition == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "WatcherCardModel.WithStanceTip<T>().");
        }


        MethodInfo divinityTip =
            stanceTipDefinition.MakeGenericMethod(
                typeof(DivinityStance));


        MethodInfo? replaceStanceTip =
            AccessTools.Method(
                typeof(RevelationPatch),
                nameof(ReplaceDivinityTipWithWrath));

        if (replaceStanceTip == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ReplaceDivinityTipWithWrath.");
        }


        // --------------------------------------------------------------------
        // Original upgrade:
        //
        // WithCostUpgradeBy(-1)
        //
        // New:
        //
        // WithCostUpgradeBy(0)
        //
        // Therefore Revelation stays at 1 Energy when upgraded.
        // --------------------------------------------------------------------

        MethodInfo? withCostUpgrade =
            AccessTools.Method(
                typeof(ConstructedCardModel),
                "WithCostUpgradeBy",
                [
                    typeof(int)
                ]);

        if (withCostUpgrade == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "ConstructedCardModel.WithCostUpgradeBy(int).");
        }


        // ====================================================================
        // APPLY PATCHES
        // ====================================================================

        for (int i = 0; i < code.Count; i++)
        {
            // ----------------------------------------------------------------
            // COST
            //
            // 2 -> 1
            // ----------------------------------------------------------------

            if (code[i].operand is ConstructorInfo constructor &&
                constructor == watcherConstructor)
            {
                // Constructor stack:
                //
                // this
                // cost
                // type
                // rarity
                // target
                // shouldShowInCardLibrary

                ReplaceInt(
                    code,
                    i - 5,
                    1);

                continue;
            }


            // ----------------------------------------------------------------
            // DAMAGE
            //
            // 12 -> 15
            // Upgrade +0 -> +5
            // ----------------------------------------------------------------

            if (code[i].Calls(withDamage))
            {
                ReplaceInt(
                    code,
                    i - 2,
                    15);

                ReplaceInt(
                    code,
                    i - 1,
                    5);

                continue;
            }


            // ----------------------------------------------------------------
            // REMOVE EXHAUST + ADD MANTRA
            // ----------------------------------------------------------------

            if (code[i].Calls(withKeywords))
            {
                CodeInstruction original =
                    code[i];

                var replacement =
                    new CodeInstruction(
                        OpCodes.Call,
                        replaceKeywords);

                replacement.labels.AddRange(
                    original.labels);

                replacement.blocks.AddRange(
                    original.blocks);

                code[i] = replacement;

                continue;
            }


            // ----------------------------------------------------------------
            // DIVINITY TOOLTIP -> WRATH TOOLTIP
            // ----------------------------------------------------------------

            if (code[i].Calls(divinityTip))
            {
                CodeInstruction original =
                    code[i];

                var replacement =
                    new CodeInstruction(
                        OpCodes.Call,
                        replaceStanceTip);

                replacement.labels.AddRange(
                    original.labels);

                replacement.blocks.AddRange(
                    original.blocks);

                code[i] = replacement;

                continue;
            }


            // ----------------------------------------------------------------
            // REMOVE COST UPGRADE
            //
            // -1 -> 0
            // ----------------------------------------------------------------

            if (code[i].Calls(withCostUpgrade))
            {
                ReplaceInt(
                    code,
                    i - 1,
                    0);
            }
        }


        return code;
    }


    // ========================================================================
    // REMOVE EXHAUST + ADD MANTRA
    // ========================================================================
    //
    // This replaces:
    //
    // WithKeywords(CardKeyword.Exhaust)
    //
    // We deliberately ignore the original keyword array, which means
    // Exhaust is never registered in the first place.
    //
    // While the card is still being constructed, we then add:
    //
    // Mantra 3
    // Upgrade +2
    //
    // giving 3(5) Mantra.
    // ========================================================================

    private static ConstructedCardModel ReplaceKeywordsWithMantra(
        ConstructedCardModel card,
        CardKeyword[] ignoredKeywords)
    {
        if (card is not WatcherCardModel watcherCard)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: Revelation was not a WatcherCardModel.");
        }


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
                    m.GetParameters()[0].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[1].ParameterType ==
                        typeof(int) &&
                    m.GetParameters()[2].ParameterType ==
                        typeof(bool));

        if (withPower == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "WatcherCardModel.WithPower<T>(int, int, bool).");
        }


        object? result =
            withPower
                .MakeGenericMethod(
                    typeof(MantraPower))
                .Invoke(
                    watcherCard,
                    [
                        3,      // Base Mantra
                        2,      // Upgrade: +2 -> 5
                        true    // Show Mantra tooltip
                    ]);


        if (result is not ConstructedCardModel constructedCard)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: " +
                "WithPower<MantraPower> returned an unexpected result.");
        }


        return constructedCard;
    }


    // ========================================================================
    // DIVINITY TOOLTIP -> WRATH TOOLTIP
    // ========================================================================

    private static WatcherCardModel ReplaceDivinityTipWithWrath(
        WatcherCardModel card)
    {
        MethodInfo? withStanceTip =
            typeof(WatcherCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithStanceTip" &&
                    m.IsGenericMethodDefinition &&
                    m.GetGenericArguments().Length == 1 &&
                    m.GetParameters().Length == 0);

        if (withStanceTip == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "WatcherCardModel.WithStanceTip<T>().");
        }


        object? result =
            withStanceTip
                .MakeGenericMethod(
                    typeof(WrathStance))
                .Invoke(
                    card,
                    null);


        if (result is not WatcherCardModel watcherCard)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: " +
                "WithStanceTip<WrathStance> returned an unexpected result.");
        }


        return watcherCard;
    }


    // ========================================================================
    // ON PLAY
    // ========================================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        AncientCard2 __instance,
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
        AncientCard2 card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        // --------------------------------------------------------------------
        // Deal 15(20) damage.
        // --------------------------------------------------------------------

        await CommonActions
            .CardAttack(
                card,
                cardPlay)
            .WithHitFx(
                "vfx/vfx_attack_slash")
            .Execute(ctx);


        // --------------------------------------------------------------------
        // Enter Wrath.
        // --------------------------------------------------------------------

        await StanceCmd.EnterWrath(
            ctx,
            card.Owner,
            cardPlay.Card);


        // --------------------------------------------------------------------
        // Gain 3(5) Mantra.
        // --------------------------------------------------------------------

        await CommonActions.ApplySelf<MantraPower>(
            ctx,
            card);
    }


    // ========================================================================
    // IL HELPER
    // ========================================================================

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