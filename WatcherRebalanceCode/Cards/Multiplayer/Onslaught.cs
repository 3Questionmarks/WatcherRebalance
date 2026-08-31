using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Watcher.Code.Character;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;

[Pool(typeof(WatcherCardPool))]
public sealed class Onslaught()
    : WatcherRebalanceCard(
        2,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.AnyAlly)
{
    // ================================================================
    // POWER VARIABLE
    // ================================================================
    //
    // Used when applying OnslaughtPower.
    //
    // ================================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
            nameof(OnslaughtPower),
            1)
    ];


    // ================================================================
    // KEYWORDS
    // ================================================================
    //
    // Base:
    // Exhaust.
    //
    // Upgrade:
    // Remove Exhaust.
    //
    // ================================================================

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
    [
        CardKeyword.Exhaust
    ];


    // ================================================================
    // TOOLTIPS
    // ================================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherHoverTipFactory.FromStance<WrathStance>()
    ];


    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;


    // ================================================================
    // GOLD GLOW
    // ================================================================

    protected override bool ShouldGlowGoldInternal =>
        Owner.IsInWatcherStance<WrathStance>();


    // ================================================================
    // ON PLAY
    // ================================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState == null)
            return;


        List<Creature> targets;


        // ============================================================
        // ALREADY IN WRATH
        // ============================================================
        //
        // Affect every OTHER living player.
        //
        // ============================================================

        if (Owner.IsInWatcherStance<WrathStance>())
        {
            targets =
                CombatState
                    .PlayerCreatures
                    .Where(creature =>
                        creature.IsAlive &&
                        creature != Owner.Creature &&
                        creature.Player != null)
                    .ToList();
        }


        // ============================================================
        // NOT IN WRATH
        // ============================================================
        //
        // Affect only the selected other player.
        //
        // ============================================================

        else
        {
            Creature? target =
                cardPlay.Target;


            if (target == null ||
                !target.IsAlive ||
                target == Owner.Creature ||
                target.Player == null)
            {
                return;
            }


            targets =
            [
                target
            ];
        }


        if (targets.Count == 0)
            return;


        // ============================================================
        // APPLY NEXT-TURN WRATH EXIT POWER
        // ============================================================

        await CommonActions.Apply<OnslaughtPower>(
            choiceContext,
            targets,
            this,
            true);


        // ============================================================
        // ENTER WRATH
        // ============================================================

        foreach (Creature creature in targets)
        {
            if (creature.Player == null)
                continue;


            await StanceCmd.EnterWrath(
                choiceContext,
                creature.Player,
                this);
        }
    }


    // ================================================================
    // UPGRADE
    // ================================================================
    //
    // Remove Exhaust.
    //
    // ================================================================

    protected override void OnUpgrade()
    {
        RemoveKeyword(CardKeyword.Exhaust);
    }
}