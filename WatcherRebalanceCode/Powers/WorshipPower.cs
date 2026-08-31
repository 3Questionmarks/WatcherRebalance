using BaseLib.Extensions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Events;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class WorshipPower
    : WatcherRebalancePower,
      IOnStanceChange
{
    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Single;


    protected override IEnumerable<IHoverTip>
        ExtraHoverTips =>
    [
        WatcherHoverTipFactory
            .FromStance<DivinityStance>(),

        HoverTipFactory.FromCard<Smite>(),
        HoverTipFactory.FromCard<Miracle>(),
        HoverTipFactory.FromCard<Insight>()
    ];


    // =========================================================
    // ENTER DIVINITY
    // =========================================================

    public async Task OnStanceChange(
        PlayerChoiceContext choiceContext,
        Player player,
        WatcherStanceModel oldStance,
        WatcherStanceModel newStance)
    {
        // Only trigger for the owner of Worship.

        if (player.Creature != Owner)
            return;


        // Only trigger when actually ENTERING Divinity.

        if (newStance is not DivinityStance)
            return;


        Flash();


        // -----------------------------------------------------
        // BUILD THE THREE CHOICES
        // -----------------------------------------------------
        //
        // This follows the Watcher mod's native Wish pattern:
        //
        // canonical model
        // -> MutableClone()
        // -> assign Owner
        // -> FromChooseACardScreen()
        // -----------------------------------------------------

        List<CardModel> cardsToChoose =
            new CardModel[]
            {
                ModelDb.Card<Smite>(),
                ModelDb.Card<Miracle>(),
                ModelDb.Card<Insight>()
            }
            .Select(model =>
                (CardModel)model.MutableClone())
            .ToList();


        foreach (CardModel card in cardsToChoose)
        {
            card.Owner =
                player;
        }


        CardModel? choice =
            await CardSelectCmd
                .FromChooseACardScreen(
                    choiceContext,
                    cardsToChoose,
                    player);


        // -----------------------------------------------------
        // GENERATE THE ACTUAL CHOSEN CARD
        // -----------------------------------------------------
        //
        // Don't insert the preview clone itself.
        //
        // WatcherCmd.GiveCard creates/registers a proper
        // generated combat card.
        // -----------------------------------------------------

        switch (choice)
        {
            case Smite:

                await WatcherCmd.GiveCard<Smite>(
                    player,
                    PileType.Hand,
                    CardPilePosition.Top);

                break;


            case Miracle:

                await WatcherCmd.GiveCard<Miracle>(
                    player,
                    PileType.Hand,
                    CardPilePosition.Top);

                break;


            case Insight:

                await WatcherCmd.GiveCard<Insight>(
                    player,
                    PileType.Hand,
                    CardPilePosition.Top);

                break;
        }


        // The Worship+ reward can only happen once.

        RemoveInternal();
    }


    // =========================================================
    // EXPIRE AT END OF TURN
    // =========================================================
    //
    // Worship+ says:
    //
    // "If you enter Divinity THIS TURN..."
    //
    // If Divinity wasn't entered, remove the marker.
    // =========================================================

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return Task.CompletedTask;
        }


        RemoveInternal();


        return Task.CompletedTask;
    }
}