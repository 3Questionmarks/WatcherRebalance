using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

public abstract class DivineInterventionChoice(CardType type)
    : WatcherRebalanceCard(
        -1,
        type,
        CardRarity.Token,
        TargetType.None,
        shouldShowInCardLibrary: true)
{
    /// <summary>
    /// Divine Intervention choices exist only as hidden
    /// choice cards and should never be randomly generated.
    /// </summary>
    public override bool CanBeGeneratedInCombat => false;


    /// <summary>
    /// Resolves the effect represented by this choice card.
    /// </summary>
    public abstract Task ResolveChoice(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        DivineIntervention source);
}