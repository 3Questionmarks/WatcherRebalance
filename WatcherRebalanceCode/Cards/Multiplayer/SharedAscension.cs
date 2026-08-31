using System.Reflection;
using System.Reflection.Emit;
using BaseLib.Abstracts;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Abstract;
using Watcher.Code.Cards.Multiplayer;
using Watcher.Code.Commands;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;


// ============================================================================
// SHARED ASCENSION
// ============================================================================
//
// Patches the existing SharedWisdom model.
//
// Multiplayer Rare Skill
// Cost: 3
//
// Another player enters Divinity.
// If you are in Divinity, all players enter Divinity.
// Exhaust.
//
// Upgrade:
// Retain.
//
// Changes:
//
// - Renamed through localization to Shared Ascension.
// - Removes Ethereal completely.
// - Upgrade adds Retain instead.
// - Exhaust remains permanent.
// - If owner is already in Divinity, ALL living players enter Divinity.
//
// ============================================================================

[HarmonyPatch(typeof(SharedWisdom))]
public static class SharedAscensionPatch
{
    [HarmonyPatch(typeof(CardModel), "get_ShouldGlowGoldInternal")]
    [HarmonyPostfix]
    private static void GlowPostfix(
        CardModel __instance,
        ref bool __result)
    {
        if (__result)
            return;

        if (__instance is not SharedWisdom card)
            return;

        __result =
            card.Owner.IsInWatcherStance<DivinityStance>();
    }
    
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
        // Find WithKeyword(CardKeyword, UpgradeType)
        //
        // Original Shared Wisdom:
        //
        // WithKeyword(
        //     CardKeyword.Ethereal,
        //     UpgradeType.Remove)
        //
        // We replace this with:
        //
        // Retain, UpgradeType.Add
        //
        // without needing to reference the protected UpgradeType enum.
        // --------------------------------------------------------------------

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


        MethodInfo? replaceEthereal =
            AccessTools.Method(
                typeof(SharedAscensionPatch),
                nameof(ReplaceEtherealWithRetain));

        if (replaceEthereal == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find ReplaceEtherealWithRetain.");
        }


        // ====================================================================
        // PATCH IL
        // ====================================================================

        for (int i = 0; i < code.Count; i++)
        {
            if (!code[i].Calls(withKeyword))
            {
                continue;
            }


            // We only want the FIRST WithKeyword call.
            //
            // SharedWisdom constructor order is:
            //
            // 1. Ethereal / Remove
            // 2. Exhaust
            //
            // Therefore this replaces Ethereal while leaving the permanent
            // Exhaust call untouched.

            CodeInstruction original =
                code[i];

            var replacement =
                new CodeInstruction(
                    OpCodes.Call,
                    replaceEthereal);

            replacement.labels.AddRange(
                original.labels);

            replacement.blocks.AddRange(
                original.blocks);

            code[i] = replacement;


            break;
        }


        return code;
    }


    // ========================================================================
    // ETHEREAL -> UPGRADE RETAIN
    // ========================================================================
    //
    // We are executing while the original card constructor is still running,
    // so using the card's constructor builder here is safe.
    //
    // We locate UpgradeType.Add through reflection because UpgradeType is a
    // protected nested enum on ConstructedCardModel.
    //
    // ========================================================================

    private static ConstructedCardModel ReplaceEtherealWithRetain(
        ConstructedCardModel card,
        CardKeyword ignoredKeyword,
        int ignoredUpgradeType)
    {
        Type? upgradeType =
            typeof(ConstructedCardModel)
                .GetNestedType(
                    "UpgradeType",
                    BindingFlags.NonPublic);


        if (upgradeType == null)
        {
            throw new MissingMemberException(
                "WatcherRebalance: Could not find ConstructedCardModel.UpgradeType.");
        }


        object upgradeAdd =
            Enum.Parse(
                upgradeType,
                "Add");


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


        object? result =
            withKeyword.Invoke(
                card,
                [
                    CardKeyword.Retain,
                    upgradeAdd
                ]);


        if (result is not ConstructedCardModel constructedCard)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: WithKeyword(Retain, Add) returned an unexpected result.");
        }


        return constructedCard;
    }


    // ========================================================================
    // ON PLAY
    // ========================================================================

    [HarmonyPatch("OnPlay")]
    [HarmonyPrefix]
    private static bool OnPlayPrefix(
        SharedWisdom __instance,
        PlayerChoiceContext __0,
        CardPlay __1,
        ref Task __result)
    {
        __result =
            NewOnPlay(
                __instance,
                __0,
                __1);

        return false;
    }


    private static async Task NewOnPlay(
        SharedWisdom card,
        PlayerChoiceContext ctx,
        CardPlay cardPlay)
    {
        // ====================================================================
        // OWNER IS IN DIVINITY
        // ====================================================================
        //
        // All living players enter Divinity, INCLUDING the owner.
        //
        // ====================================================================

        if (card.Owner.IsInWatcherStance<DivinityStance>())
        {
            if (card.CombatState == null)
            {
                return;
            }


            List<Creature> players =
                card.CombatState
                    .PlayerCreatures
                    .Where(creature =>
                        creature.IsAlive &&
                        creature.Player != null)
                    .ToList();


            foreach (Creature creature in players)
            {
                if (creature.Player == null)
                {
                    continue;
                }


                await StanceCmd.EnterDivinity(
                    ctx,
                    creature.Player,
                    card);
            }


            return;
        }


        // ====================================================================
        // NORMAL CASE
        // ====================================================================
        //
        // Another player enters Divinity.
        //
        // This preserves Shared Wisdom's original selected-player behavior.
        //
        // ====================================================================

        if (cardPlay.Target?.Player == null)
        {
            return;
        }


        await StanceCmd.EnterDivinity(
            ctx,
            cardPlay.Target.Player,
            card);
    }
}