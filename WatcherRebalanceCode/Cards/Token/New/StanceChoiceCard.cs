using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

public abstract class StanceChoiceCard()
    : WatcherRebalanceCard(
        -1,
        CardType.Skill,
        CardRarity.Token,
        TargetType.None,
        shouldShowInCardLibrary: false)
{
    public override bool CanBeGeneratedInCombat => false;

    public abstract Task ResolveChoice(
        PlayerChoiceContext choiceContext,
        CardModel source);
}