using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Character;
using Watcher.Code.Core;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

[Pool(typeof(WatcherCardPool))]
public sealed class DivineIntervention
    : WatcherRebalanceCard
{
    public DivineIntervention()
        : base(
            5,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }


    // =========================================================
    // KEYWORDS
    // =========================================================

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Ethereal,
        CardKeyword.Exhaust
    ];


    // =========================================================
    // VARIABLES
    // =========================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Replay", 1)
            .WithUpgrade(1)
    ];


    // =========================================================
    // TOOLTIPS
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherHoverTipFactory
            .FromStance<DivinityStance>(),

        HoverTipFactory.Static(
            StaticHoverTip.ReplayStatic),

        HoverTipFactory
            .FromCard<WealthBeyondMeasure>(),

        HoverTipFactory
            .FromCard<AttainPerfection>(),

        HoverTipFactory
            .FromCard<ClaimYourDestiny>()
    ];


    // =========================================================
    // GOLD GLOW
    // =========================================================
    //
    // Divine Intervention's enhanced Replay effect is active
    // while the player is in Divinity, so highlight the card
    // gold whenever that condition is currently satisfied.
    // =========================================================

    protected override bool ShouldGlowGoldInternal =>
        CombatState != null &&
        Owner.IsInWatcherStance<DivinityStance>();


    // =========================================================
    // REPLAY
    // =========================================================

    public override int ModifyCardPlayCount(
        CardModel card,
        Creature? target,
        int playCount)
    {
        if (!ReferenceEquals(card, this))
        {
            return playCount;
        }

        if (!Owner.IsInWatcherStance<DivinityStance>())
        {
            return playCount;
        }

        return playCount +
               DynamicVars["Replay"].IntValue;
    }


    // =========================================================
    // PLAY
    // =========================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // Create mutable versions of the three hidden
        // Divine Intervention choice cards.

        List<CardModel> cardsToChoose =
        [
            (CardModel)ModelDb
                .Card<WealthBeyondMeasure>()
                .MutableClone(),

            (CardModel)ModelDb
                .Card<AttainPerfection>()
                .MutableClone(),

            (CardModel)ModelDb
                .Card<ClaimYourDestiny>()
                .MutableClone()
        ];


        // The temporary choice cards still need an owner so
        // their effects and previews have the correct player.

        foreach (CardModel card in cardsToChoose)
        {
            card.Owner = Owner;
        }


        // Use the same general pattern as Wish: display three
        // card choices and return the chosen card model.

        CardModel? chosenCard =
            await CardSelectCmd.FromChooseACardScreen(
                choiceContext,
                cardsToChoose,
                Owner);


        if (chosenCard is DivineInterventionChoice choice)
        {
            await choice.ResolveChoice(
                choiceContext,
                cardPlay,
                this);
        }


        // Divine Intervention always ends the player's turn
        // after resolving its choice.

        PlayerCmd.EndTurn(
            Owner,
            canBackOut: false);
    }
}