using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Character;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

[Pool(typeof(WatcherCardPool))]
public sealed class Choreograph : WatcherRebalanceCard
{
    public Choreograph()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }


    // =========================================================
    // VARIABLES
    // =========================================================
    //
    // Base:    2 cards
    // Upgrade: 3 cards
    // =========================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CardsVar(2)
            .WithUpgrade(1)
    ];


    // =========================================================
    // KEYWORDS
    // =========================================================

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];


    // Choreograph itself does not Retain, but its effect grants
    // Retain to other cards, so give it the Retain explanation.
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(
            CardKeyword.Retain)
    ];


    // =========================================================
    // ON PLAY
    // =========================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        CardPile discardPile =
            PileType.Discard
                .GetPile(Owner);


        int availableCards =
            discardPile.Cards.Count;


        if (availableCards <= 0)
            return;


        int desiredAmount =
            DynamicVars.Cards.IntValue;


        // If the discard pile contains fewer cards than the
        // stated amount, simply take all available cards instead
        // of requiring an impossible selection.
        int amountToSelect =
            Math.Min(
                desiredAmount,
                availableCards);


        IEnumerable<CardModel> selectedCards =
            await CommonActions.SelectCards(
                this,
                new LocString(
                    "cards",
                    "WATCHERREBALANCE-CHOREOGRAPH.selectionScreenPrompt"),
                choiceContext,
                PileType.Discard,
                amountToSelect);


        List<CardModel> cards =
            selectedCards.ToList();


        foreach (CardModel selectedCard in cards)
        {
            // -------------------------------------------------
            // UPGRADE FOR THIS COMBAT
            // -------------------------------------------------
            //
            // Because this is the combat Discard Pile rather
            // than PileType.Deck, CardCmd.Upgrade modifies this
            // combat card rather than permanently smithing the
            // deck copy.
            // -------------------------------------------------

            if (selectedCard.IsUpgradable)
            {
                CardCmd.Upgrade(
                    selectedCard);
            }


            // -------------------------------------------------
            // RETAIN FOR THE REST OF THIS COMBAT
            // -------------------------------------------------

            if (!selectedCard.Keywords.Contains(
                    CardKeyword.Retain))
            {
                CardCmd.ApplyKeyword(
                    selectedCard,
                    CardKeyword.Retain);
            }


            // -------------------------------------------------
            // RETURN TO HAND
            // -------------------------------------------------

            await CardPileCmd.Add(
                selectedCard,
                PileType.Hand,
                CardPilePosition.Bottom,
                this);
        }
    }
}