using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using BaseLib.Patches.Hooks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using Watcher.Code.Relics;

namespace WatcherRebalance.WatcherRebalanceCode.Relics;

/// <summary>
/// Shared hand-size mechanic used by Pure Water and Holy Water.
///
/// A limited number of Token-rarity cards in hand do not count
/// toward the player's normal maximum hand size.
///
/// Pure Water:
///     +2 free Token cards per copy.
///
/// Holy Water:
///     +5 free Token cards per copy.
///
/// Multiple copies stack with themselves and with each other.
/// </summary>
internal static class FreeTokenCards
{
    // ========================================================================
    // ADD CONTEXT
    // ========================================================================
    //
    // BaseLib's max-hand-size calculation receives:
    //
    //     Player
    //     base hand-size limit
    //
    // but when CardPileCmd.Add checks whether the Hand is full,
    // we additionally need to know WHICH card is currently being added.
    //
    // Example:
    //
    // Pure Water
    //
    //     10 normal cards
    //      1 Token card
    //
    // The physical hand contains 11 cards.
    //
    // Existing Token adjustment makes the effective maximum 11.
    // A second incoming Token should still be allowed because Pure Water
    // permits two free Tokens.
    //
    // To distinguish that incoming Token from an incoming normal card,
    // CardPileCmd.Add stores its current add operation here.
    // ========================================================================

    private static readonly AsyncLocal<AddContext?> CurrentAddContext = new();


    internal sealed class AddContext
    {
        internal CardPile TargetPile { get; }

        private readonly IReadOnlyList<CardModel> _cards;

        private int _index;


        internal AddContext(
            CardPile targetPile,
            IReadOnlyList<CardModel> cards)
        {
            TargetPile = targetPile;
            _cards = cards;
        }


        /// <summary>
        /// Returns the next card which will actually reach CardPileCmd.Add's
        /// hand-capacity check.
        ///
        /// CardPileCmd.Add creates failed results for removed cards, dead
        /// owners, and cards which have lost their combat state. Those results
        /// never reach the hand-size comparison, so skip the same cases here.
        /// </summary>
        internal CardModel? TakeNextCard(Player player)
        {
            while (_index < _cards.Count)
            {
                CardModel card = _cards[_index++];

                if (card.Owner != player)
                    continue;

                if (card.HasBeenRemovedFromState)
                    continue;

                if (card.Owner.Creature.IsDead)
                    continue;

                if (card.IsInCombat &&
                    card.Owner.Creature.CombatState == null)
                {
                    continue;
                }

                return card;
            }

            return null;
        }
    }


    // ========================================================================
    // ALLOWANCE
    // ========================================================================

    /// <summary>
    /// Calculates the player's total free-Token allowance.
    ///
    /// Every individual relic instance contributes separately.
    ///
    /// Examples:
    ///
    /// 1 Pure Water
    ///     2
    ///
    /// 2 Pure Waters
    ///     4
    ///
    /// 1 Pure Water + 1 Holy Water
    ///     7
    ///
    /// 2 Holy Waters
    ///     10
    /// </summary>
    internal static int GetAllowance(Player player)
    {
        IRunState runState =
            player.RunState ??
            NullRunState.Instance;

        var combatState =
            player.Creature.CombatState;

        int totalAllowance = 0;

        foreach (AbstractModel listener in
                 runState.IterateHookListeners(combatState))
        {
            if (listener is HolyWater holyWater &&
                holyWater.Owner == player)
            {
                totalAllowance +=
                    HolyWaterFreeTokenCards.Amount;
            }

            if (listener is PureWater pureWater &&
                pureWater.Owner == player)
            {
                totalAllowance +=
                    PureWaterFreeTokenCards.Amount;
            }
        }

        return totalAllowance;
    }


    // ========================================================================
    // TOKEN HELPERS
    // ========================================================================

    internal static bool IsToken(CardModel card)
    {
        return card.Rarity == CardRarity.Token;
    }


    internal static int GetTokenCount(Player player)
    {
        CardPile hand =
            PileType.Hand.GetPile(player);

        return hand.Cards.Count(IsToken);
    }


    /// <summary>
    /// Number of Tokens currently in hand which should not consume
    /// normal hand capacity.
    /// </summary>
    internal static int GetIgnoredTokenCount(Player player)
    {
        int allowance =
            GetAllowance(player);

        if (allowance <= 0)
            return 0;

        return Math.Min(
            GetTokenCount(player),
            allowance);
    }


    /// <summary>
    /// Whether another Token can enter without consuming a normal
    /// hand slot.
    /// </summary>
    internal static bool HasFreeTokenSlot(Player player)
    {
        int allowance =
            GetAllowance(player);

        if (allowance <= 0)
            return false;

        return GetTokenCount(player) < allowance;
    }


    // ========================================================================
    // BASE MAXIMUM MODIFICATION
    // ========================================================================

    /// <summary>
    /// Adds the number of currently-ignored Tokens to the calculated
    /// physical maximum.
    ///
    /// Example:
    ///
    /// Base maximum = 10
    ///
    /// Hand:
    ///     8 normal
    ///     2 free Tokens
    ///
    /// Reported physical maximum = 12.
    ///
    /// This still leaves two normal slots available.
    /// </summary>
    internal static void ModifyCalculatedMaximum(
        Player player,
        ref int maximum)
    {
        maximum +=
            GetIgnoredTokenCount(player);
    }


    // ========================================================================
    // ADD-SPECIFIC MAXIMUM
    // ========================================================================

    /// <summary>
    /// Calculates hand capacity while CardPileCmd.Add is processing an
    /// incoming card.
    ///
    /// Tokens can receive one temporary additional physical slot when
    /// the relic still has unused Token allowance.
    ///
    /// Normal cards never receive that temporary slot.
    /// </summary>
    internal static int GetMaximumForAdd(
        Player player,
        int baseLimit)
    {
        // This calls BaseLib's normal maximum calculation.
        //
        // FreeTokenCardsMaximumPatch below subsequently adds the number
        // of free Tokens which are already in the hand.

        int maximum =
            MaxHandSizePatch.GetMaxHandSize(
                player,
                baseLimit);

        AddContext? context =
            CurrentAddContext.Value;

        if (context == null)
            return maximum;

        CardModel? incomingCard =
            context.TakeNextCard(player);

        if (incomingCard == null)
            return maximum;


        // Only hand additions need this mechanic.

        if (context.TargetPile.Type != PileType.Hand)
            return maximum;


        // If a card is somehow being re-added to the same Hand it already
        // occupies, do not reject it simply because the Hand is full.

        if (ReferenceEquals(
                incomingCard.Pile,
                context.TargetPile))
        {
            return maximum + 1;
        }


        // Unused Token capacity is exclusively for Token-rarity cards.

        if (!IsToken(incomingCard))
            return maximum;


        // Allow this specific incoming Token if there is still allowance.

        if (HasFreeTokenSlot(player))
            return maximum + 1;

        return maximum;
    }


    // ========================================================================
    // DRAW-SPECIFIC MAXIMUM
    // ========================================================================

    /// <summary>
    /// Calculates hand capacity during a draw.
    ///
    /// If normal hand capacity is full, drawing is permitted only when
    /// the actual top card is a Token and unused Token allowance remains.
    ///
    /// The game does not search deeper into the Draw Pile for a Token.
    /// </summary>
    internal static int GetMaximumForDraw(
        Player player,
        int baseLimit)
    {
        int maximum =
            MaxHandSizePatch.GetMaxHandSize(
                player,
                baseLimit);

        if (!HasFreeTokenSlot(player))
            return maximum;

        CardPile drawPile =
            PileType.Draw.GetPile(player);

        CardModel? topCard =
            drawPile.Cards.FirstOrDefault();


        // There is already a card ready to draw.
        //
        // Only grant the extra temporary slot when THAT card is a Token.

        if (topCard != null)
        {
            return IsToken(topCard)
                ? maximum + 1
                : maximum;
        }


        // DrawInternal checks hand capacity before ShuffleIfNecessary.
        //
        // If Draw is empty but Discard contains cards, let the draw process
        // advance to the shuffle.
        //
        // Once shuffled, DrawInternal checks again. At that point the real
        // top card exists and the block above decides whether it can enter.

        CardPile discardPile =
            PileType.Discard.GetPile(player);

        if (discardPile.Cards.Count > 0)
            return maximum + 1;


        return maximum;
    }


    // ========================================================================
    // ADD CONTEXT MANAGEMENT
    // ========================================================================

    internal static AddContext? BeginAdd(
        ref IEnumerable<CardModel> cards,
        CardPile newPile)
    {
        AddContext? previous =
            CurrentAddContext.Value;

        if (newPile.Type != PileType.Hand)
            return previous;


        // Materialize once so CardPileCmd.Add and our context inspect
        // exactly the same cards in exactly the same order.

        List<CardModel> list =
            cards as List<CardModel>
            ?? cards.ToList();

        cards = list;

        CurrentAddContext.Value =
            new AddContext(
                newPile,
                list);

        return previous;
    }


    internal static void RestoreAddContext(
        AddContext? previous)
    {
        CurrentAddContext.Value =
            previous;
    }
}


// ============================================================================
// EXISTING TOKEN HAND-SIZE PATCH
// ============================================================================
//
// BaseLib changes the game's hand-size constants into:
//
//     MaxHandSizePatch.GetMaxHandSize(player, baseLimit)
//
// We postfix that calculation and add one physical slot for each Token
// currently being ignored by Pure/Holy Water.
//
// Because this happens after BaseLib calculates its maximum, other hand-size
// modifiers remain intact.
// ============================================================================

[HarmonyPatch(
    typeof(MaxHandSizePatch),
    nameof(MaxHandSizePatch.GetMaxHandSize),
    [
        typeof(Player),
        typeof(int)
    ])]
internal static class FreeTokenCardsMaximumPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Player player,
        ref int __result)
    {
        FreeTokenCards.ModifyCalculatedMaximum(
            player,
            ref __result);
    }
}


// ============================================================================
// CARDPILECMD.ADD CONTEXT PATCH
// ============================================================================
//
// Records the cards currently being processed by CardPileCmd.Add so the
// maximum-hand-size helper can distinguish an incoming Token from an incoming
// normal card.
// ============================================================================

[HarmonyPatch]
internal static class FreeTokenCardsAddContextPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method =
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Add),
                [
                    typeof(IEnumerable<CardModel>),
                    typeof(CardPile),
                    typeof(CardPilePosition),
                    typeof(AbstractModel),
                    typeof(bool),
                    typeof(bool)
                ]);


        // Compatibility with versions of CardPileCmd.Add from before the
        // isChangingOwners argument existed.

        method ??=
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Add),
                [
                    typeof(IEnumerable<CardModel>),
                    typeof(CardPile),
                    typeof(CardPilePosition),
                    typeof(AbstractModel),
                    typeof(bool)
                ]);


        return method
            ?? throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "CardPileCmd.Add(IEnumerable<CardModel>, CardPile, ...).");
    }


    [HarmonyPrefix]
    private static void Prefix(
        ref IEnumerable<CardModel> cards,
        CardPile newPile,
        out FreeTokenCards.AddContext? __state)
    {
        __state =
            FreeTokenCards.BeginAdd(
                ref cards,
                newPile);
    }


    [HarmonyPostfix]
    private static void Postfix(
        FreeTokenCards.AddContext? __state)
    {
        FreeTokenCards.RestoreAddContext(
            __state);
    }
}


// ============================================================================
// CARDPILECMD.ADD MAX-HAND-SIZE PATCH
// ============================================================================
//
// BaseLib's Add patch converts the vanilla hand-size constant into:
//
//     MaxHandSizePatch.GetMaxHandSize(player, baseLimit)
//
// Run after BaseLib and replace that call with:
//
//     FreeTokenCards.GetMaximumForAdd(player, baseLimit)
//
// The method signature is identical, so the stack remains unchanged.
// ============================================================================

[HarmonyPatch]
internal static class FreeTokenCardsAddMaxPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method =
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Add),
                [
                    typeof(IEnumerable<CardModel>),
                    typeof(CardPile),
                    typeof(CardPilePosition),
                    typeof(AbstractModel),
                    typeof(bool),
                    typeof(bool)
                ]);


        method ??=
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Add),
                [
                    typeof(IEnumerable<CardModel>),
                    typeof(CardPile),
                    typeof(CardPilePosition),
                    typeof(AbstractModel),
                    typeof(bool)
                ]);


        if (method == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find CardPileCmd.Add.");
        }


        return AccessTools.AsyncMoveNext(method);
    }


    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo baseLibMethod =
            AccessTools.Method(
                typeof(MaxHandSizePatch),
                nameof(MaxHandSizePatch.GetMaxHandSize),
                [
                    typeof(Player),
                    typeof(int)
                ])
            ?? throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "BaseLib MaxHandSizePatch.GetMaxHandSize(Player, int).");


        MethodInfo replacement =
            AccessTools.Method(
                typeof(FreeTokenCards),
                nameof(FreeTokenCards.GetMaximumForAdd))
            ?? throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "FreeTokenCards.GetMaximumForAdd.");


        int replacements = 0;


        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(baseLibMethod))
            {
                instruction.opcode =
                    OpCodes.Call;

                instruction.operand =
                    replacement;

                replacements++;
            }

            yield return instruction;
        }


        if (replacements == 0)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: Could not find BaseLib's " +
                "max-hand-size call inside CardPileCmd.Add.");
        }
    }
}


// ============================================================================
// CARDPILECMD.DRAWINTERNAL MAX-HAND-SIZE PATCH
// ============================================================================
//
// BaseLib replaces each vanilla maximum-hand-size reference in DrawInternal
// with MaxHandSizePatch.GetMaxHandSize.
//
// Replace those calls with our top-card-aware calculation.
// ============================================================================

[HarmonyPatch]
internal static class FreeTokenCardsDrawMaxPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method =
            AccessTools.Method(
                typeof(CardPileCmd),
                "DrawInternal",
                [
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool)
                ]);


        // Older versions may expose this as Draw rather than DrawInternal.

        method ??=
            AccessTools.Method(
                typeof(CardPileCmd),
                nameof(CardPileCmd.Draw),
                [
                    typeof(PlayerChoiceContext),
                    typeof(decimal),
                    typeof(Player),
                    typeof(bool)
                ]);


        if (method == null)
        {
            throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "CardPileCmd.DrawInternal.");
        }


        return AccessTools.AsyncMoveNext(method);
    }


    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo baseLibMethod =
            AccessTools.Method(
                typeof(MaxHandSizePatch),
                nameof(MaxHandSizePatch.GetMaxHandSize),
                [
                    typeof(Player),
                    typeof(int)
                ])
            ?? throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "BaseLib MaxHandSizePatch.GetMaxHandSize(Player, int).");


        MethodInfo replacement =
            AccessTools.Method(
                typeof(FreeTokenCards),
                nameof(FreeTokenCards.GetMaximumForDraw))
            ?? throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "FreeTokenCards.GetMaximumForDraw.");


        int replacements = 0;


        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(baseLibMethod))
            {
                instruction.opcode =
                    OpCodes.Call;

                instruction.operand =
                    replacement;

                replacements++;
            }

            yield return instruction;
        }


        if (replacements == 0)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: Could not find BaseLib's " +
                "max-hand-size calls inside CardPileCmd.DrawInternal.");
        }
    }
}


// ============================================================================
// CHECKIFDRAWISPOSSIBLEANDSHOWTHOUGHTBUBBLEIFNOT PATCH
// ============================================================================
//
// DrawInternal calls this helper independently of its own hand-size checks.
//
// BaseLib also transforms this helper to call GetMaxHandSize, so substitute
// the same top-card-aware calculation here.
// ============================================================================

[HarmonyPatch(
    typeof(CardPileCmd),
    "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot")]
internal static class FreeTokenCardsDrawPossiblePatch
{
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo baseLibMethod =
            AccessTools.Method(
                typeof(MaxHandSizePatch),
                nameof(MaxHandSizePatch.GetMaxHandSize),
                [
                    typeof(Player),
                    typeof(int)
                ])
            ?? throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "BaseLib MaxHandSizePatch.GetMaxHandSize(Player, int).");


        MethodInfo replacement =
            AccessTools.Method(
                typeof(FreeTokenCards),
                nameof(FreeTokenCards.GetMaximumForDraw))
            ?? throw new MissingMethodException(
                "WatcherRebalance: Could not find " +
                "FreeTokenCards.GetMaximumForDraw.");


        int replacements = 0;


        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(baseLibMethod))
            {
                instruction.opcode =
                    OpCodes.Call;

                instruction.operand =
                    replacement;

                replacements++;
            }

            yield return instruction;
        }


        if (replacements == 0)
        {
            throw new InvalidOperationException(
                "WatcherRebalance: Could not find BaseLib's " +
                "max-hand-size call inside " +
                "CheckIfDrawIsPossibleAndShowThoughtBubbleIfNot.");
        }
    }
}