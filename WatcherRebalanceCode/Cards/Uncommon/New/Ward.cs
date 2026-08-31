using BaseLib.Extensions;
using BaseLib.Hooks;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Character;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Uncommon.New;

[Pool(typeof(WatcherCardPool))]
public sealed class Ward() :
    WatcherRebalanceCard(
        0,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.Self),
    IAfterScryed
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(4, ValueProp.Move)
            .WithUpgrade(2)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        [];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(
            BaseLibTip.Scry)
    ];


    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CommonActions.CardBlock(
            this,
            cardPlay);
    }


    public async Task AfterScryed(
        PlayerChoiceContext choiceContext,
        Player player,
        int scryAmount,
        int discardAmount,
        List<CardModel> seen,
        List<CardModel> discarded)
    {
        if (player != Owner)
            return;

        CardPile discardPile =
            PileType.Discard.GetPile(player);

        if (!discardPile.Cards.Contains(this))
            return;

        await CardPileCmd.Add(
            this,
            PileType.Hand);
    }


    protected override void OnUpgrade()
    {
    }
}