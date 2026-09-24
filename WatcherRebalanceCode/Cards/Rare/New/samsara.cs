using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Character;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

[Pool(typeof(WatcherCardPool))]
public sealed class Samsara : WatcherRebalanceCard
{
    public Samsara()
        : base(
            2,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherRebalanceTips.Token()
    ];

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState != null)
        {
            List<CardModel> tokens =
                Owner.PlayerCombatState
                    .ExhaustPile
                    .Cards
                    .Where(card =>
                        card.Rarity == CardRarity.Token &&
                        !card.Keywords.Contains(CardKeyword.Unplayable))
                    .ToList();

            foreach (CardModel token in tokens)
            {
                await CardCmd.AutoPlay(
                    choiceContext,
                    token,
                    null);
            }
        }
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}