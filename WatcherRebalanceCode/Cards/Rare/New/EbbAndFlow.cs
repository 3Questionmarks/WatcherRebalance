using BaseLib.Extensions;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Character;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Events;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;


// =============================================================
// EBB AND FLOW
// =============================================================
//
// 1 Energy
//
// Gain 8 (12) Block.
//
// If this is the first time this card has been played this turn,
// enter Calm.
//
// Whenever you switch Stances, return this from your
// Discard Pile to your Hand.
//
// Special case:
// If Ebb and Flow itself causes the stance change while it is
// being played, let the card naturally enter Discard first.
// It is then returned to Hand from Discard so the normal
// discard -> hand animation is shown.
// =============================================================

[Pool(typeof(WatcherCardPool))]
public sealed class EbbAndFlow
    : WatcherRebalanceCard, IOnStanceChange
{
    // Set when a stance change happens while this card is
    // currently resolving in the Play pile.
    //
    // The OnPlayWrapper patch below consumes this flag after
    // normal card-play cleanup has put the card into Discard.
    private bool _returnAfterPlay;


    public EbbAndFlow()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }


    // =========================================================
    // BLOCK
    // =========================================================

    public override bool GainsBlock => true;


    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(
                8,
                ValueProp.Move)
            .WithUpgrade(4)
    ];


    // =========================================================
    // TOOLTIPS
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherHoverTipFactory
            .FromStance<CalmStance>()
    ];


    // =========================================================
    // PLAY
    // =========================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // Always gain 8 / 12 Block.
        await CommonActions.CardBlock(
            this,
            cardPlay);


        // Fetch-style check:
        //
        // On the first play this turn, the current play has not
        // yet generated a CardPlayFinishedEntry, so this is false.
        //
        // On later plays of this exact card instance during the
        // same turn, it is true.
        if (HasBeenPlayedThisTurn)
        {
            return;
        }


        // Only the first play each turn enters Calm.
        await StanceCmd.EnterCalm(
            choiceContext,
            Owner,
            cardPlay.Card);
    }


    // =========================================================
    // STANCE CHANGE
    // =========================================================

    public async Task OnStanceChange(
        PlayerChoiceContext choiceContext,
        Player player,
        WatcherStanceModel oldStance,
        WatcherStanceModel newStance)
    {
        // Ignore stance changes belonging to another player.
        if (newStance.Owner != Owner)
        {
            return;
        }


        // -----------------------------------------------------
        // NORMAL CASE
        // -----------------------------------------------------
        //
        // Ebb and Flow is already sitting in Discard when the
        // stance changes.
        //
        // Fetch it immediately using the normal pile command.
        // -----------------------------------------------------

        if (Pile?.Type == PileType.Discard)
        {
            await CardPileCmd.Add(
                this,
                PileType.Hand);

            return;
        }


        // -----------------------------------------------------
        // SELF-RETURN CASE
        // -----------------------------------------------------
        //
        // If Ebb and Flow itself just entered Calm, the stance
        // change happens while Ebb and Flow is still in Play.
        //
        // Do NOT move it directly to Hand here.
        //
        // Mark it for retrieval after normal card resolution has
        // moved it into Discard.
        // -----------------------------------------------------

        if (Pile?.Type == PileType.Play)
        {
            _returnAfterPlay = true;
        }
    }


    // =========================================================
    // FIRST PLAY THIS TURN
    // =========================================================

    private bool HasBeenPlayedThisTurn =>
        CombatManager.Instance
            .History
            .CardPlaysFinished
            .Any(entry =>
                entry.CardPlay.Card == this &&
                entry.HappenedThisTurn(CombatState));


    // =========================================================
    // DELAYED DISCARD -> HAND RETURN
    // =========================================================

    internal async Task ReturnAfterOwnPlayIfNeeded()
    {
        if (!_returnAfterPlay)
        {
            return;
        }


        // Consume the flag before doing anything asynchronous.
        _returnAfterPlay = false;


        // OnPlayWrapper should now have completed its ordinary
        // Play -> Discard movement.
        //
        // Only retrieve the card if it genuinely reached Discard.
        if (Pile?.Type != PileType.Discard)
        {
            return;
        }


        await CardPileCmd.Add(
            this,
            PileType.Hand);
    }
}


// =============================================================
// AFTER-PLAY PATCH
// =============================================================
//
// CardModel.OnPlayWrapper does this:
//
//     Hand
//       -> Play
//       -> execute card
//       -> result pile (normally Discard)
//       -> finish
//
// We wrap the returned Task. Once the original Task completes,
// Ebb and Flow is genuinely in Discard, allowing CardPileCmd.Add
// to produce the normal Discard -> Hand retrieval visual.
// =============================================================

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
public static class EbbAndFlowAfterPlayPatch
{
    [HarmonyPostfix]
    private static Task Postfix(
        Task __result,
        CardModel __instance)
    {
        // Do absolutely nothing for every other card.
        if (__instance is not EbbAndFlow ebbAndFlow)
        {
            return __result;
        }


        return FinishEbbAndFlowPlay(
            __result,
            ebbAndFlow);
    }


    private static async Task FinishEbbAndFlowPlay(
        Task originalTask,
        EbbAndFlow card)
    {
        // First allow the entire normal card-play process to
        // finish, including its move from Play -> Discard.
        await originalTask;


        // If a stance change happened while this copy was in
        // Play, retrieve it from its now-real Discard pile.
        await card.ReturnAfterOwnPlayIfNeeded();
    }
}