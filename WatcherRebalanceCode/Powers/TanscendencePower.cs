using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class TranscendencePower : WatcherRebalancePower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Transform),
        WatcherRebalanceTips.Token(),
    ];

    public override async Task AfterPlayerTurnStart(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        if (player != Owner.Player)
            return;

        List<CardModel> selected =
            (await CardSelectCmd.FromHand(
                choiceContext,
                player,
                new CardSelectorPrefs(
                    CardSelectorPrefs.TransformSelectionPrompt,
                    Amount),
                null,
                this))
            .ToList();

        foreach (CardModel original in selected)
        {
            switch (original.Type)
            {
                case CardType.Attack:
                    await CardCmd.TransformTo<Smite>(original);
                    break;

                case CardType.Skill:
                    await CardCmd.TransformTo<Safety>(original);
                    break;

                case CardType.Power:
                    await CardCmd.TransformTo<Insight>(original);
                    break;

                default:
                    await CardCmd.TransformTo<Miracle>(original);
                    break;
            }
        }
    }
}
