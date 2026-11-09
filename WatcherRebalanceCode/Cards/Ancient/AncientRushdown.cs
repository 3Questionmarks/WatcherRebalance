using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Watcher.Code.Character;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Cards;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Ancient;

// Standalone replacement for Clairvoyance. Keep the original Uncommon Rushdown removed.
[Pool(typeof(WatcherCardPool))]
public sealed class AncientRushdown : WatcherRebalanceCard
{
    public AncientRushdown() : base(
        1, CardType.Power,
        Config.UseAncientRushdown ? CardRarity.Ancient : CardRarity.Token,
        TargetType.Self,
        Config.UseAncientRushdown)
    {
    }

    // Share Clairvoyance's original artwork regardless of Ancient card selection.
    public override string CustomPortraitPath =>
        "Watcher/images/atlases/card_atlas.sprites/clairvoyance.tres";

    public override string PortraitPath =>
        "Watcher/images/atlases/card_atlas.sprites/clairvoyance.tres";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(nameof(RushdownPower), 2).WithUpgrade(1)
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<RushdownPower>()
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<RushdownPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[nameof(RushdownPower)].IntValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        // DynamicVars handles the 2 -> 3 upgrade.
    }
}
