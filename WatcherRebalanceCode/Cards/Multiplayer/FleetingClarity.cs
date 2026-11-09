using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using Watcher.Code.Character;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;

// The upstream FleetingClarity card and MultiplayerCardUncommonPower
// were removed in Watcher 1.5.13. Keep this as an independent card.
[Pool(typeof(WatcherCardPool))]
public sealed class FleetingClarity() : WatcherRebalanceCard(
    1,
    CardType.Skill,
    Config.UseLegacyMultiplayerStanceCards ? CardRarity.Uncommon : CardRarity.Token,
    TargetType.AnyAlly,
    Config.UseLegacyMultiplayerStanceCards)
{
    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherHoverTipFactory.FromStance<CalmStance>()
    ];

    protected override bool ShouldGlowGoldInternal =>
        Owner.IsInWatcherStance<CalmStance>();

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState == null)
            return;

        List<Creature> targets;

        if (Owner.IsInWatcherStance<CalmStance>())
        {
            targets = CombatState.PlayerCreatures
                .Where(creature => creature.IsAlive &&
                                   creature != Owner.Creature &&
                                   creature.Player != null)
                .ToList();
        }
        else
        {
            Creature? target = cardPlay.Target;
            if (target == null || !target.IsAlive ||
                target == Owner.Creature || target.Player == null)
                return;

            targets = [target];
        }

        if (targets.Count == 0)
            return;

        // Replaces the removed Watcher MultiplayerCardUncommonPower.
        await CommonActions.Apply<FleetingClarityPower>(
            choiceContext, targets, this, true);

        foreach (Creature creature in targets)
        {
            if (creature.Player != null)
            {
                await StanceCmd.EnterCalm(
                    choiceContext, creature.Player, this);
            }
        }
    }

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}
