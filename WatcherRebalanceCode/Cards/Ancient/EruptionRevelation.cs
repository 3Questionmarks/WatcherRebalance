using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Character;
using Watcher.Code.Commands;
using Watcher.Code.Core;
using Watcher.Code.Powers;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Cards;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Ancient;

// Rebalance alternative to the Watcher's Vigilance -> Revelation transformation.
// Archaic Tooth selects this card when UseEruptionRevelation is enabled.
[Pool(typeof(WatcherCardPool))]
public sealed class EruptionRevelation : WatcherRebalanceCard
{
    // Disabled choice remains outside the Ancient pool and library.
    // When enabled, Archaic Tooth excludes its transformation target from
    // the Dusty Tome Ancient rewards via TranscendenceCards.
    public EruptionRevelation()
        : base(1, CardType.Attack,
            Config.UseEruptionRevelation ? CardRarity.Ancient : CardRarity.Token,
            TargetType.AnyEnemy, Config.UseEruptionRevelation)
    {
    }

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(15m, ValueProp.Move),
        new DynamicVar(nameof(MantraPower), 3).WithUpgrade(2)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherHoverTipFactory.FromStance<WrathStance>(),
        HoverTipFactory.FromPower<MantraPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CommonActions.CardAttack(this, cardPlay)
            .WithHitFx("vfx/vfx_starry_impact", tmpSfx: "blunt_attack.mp3")
            .Execute(choiceContext);
        await StanceCmd.EnterWrath(choiceContext, Owner, cardPlay.Card);
        await PowerCmd.Apply<MantraPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(MantraPower)].IntValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(5m);
    }
}
