using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Stances;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

[Pool(typeof(ColorlessCardPool))]
public sealed class CalmCard : StanceChoiceCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherHoverTipFactory.FromStance<CalmStance>()
    ];

    public override async Task ResolveChoice(
        PlayerChoiceContext choiceContext,
        CardModel source)
    {
        await StanceCmd.EnterCalm(
            choiceContext,
            Owner,
            source);
    }
}