using BaseLib.Commands;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Character;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Common.New;

[Pool(typeof(WatcherCardPool))]
public sealed class PathToVictory() : WatcherRebalanceCard(
    1,
    CardType.Skill,
    CardRarity.Common,
    TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Scry", 0).WithUpgrade(2)
    ];

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        IsUpgraded
            ? [HoverTipFactory.Static(BaseLibTip.Scry)]
            : [];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (IsUpgraded)
        {
            await ScryCmd.Execute(
                choiceContext,
                Owner,
                DynamicVars["Scry"].IntValue);
        }

        CardModel? drawnCard =
            await CardPileCmd.Draw(choiceContext, Owner);

        drawnCard?.SetToFreeThisTurn();
    }

    protected override void OnUpgrade()
    {
    }
}