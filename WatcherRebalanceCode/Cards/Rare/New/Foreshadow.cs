using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Character;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

[Pool(typeof(WatcherCardPool))]
public sealed class Foreshadow : WatcherRebalanceCard
{
    public Foreshadow()
        : base(
            1,
            CardType.Skill,
            CardRarity.Rare,
            TargetType.Self)
    {
    }


    // =========================================================
    // DYNAMIC VARIABLES
    // =========================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar(
                "Cards",
                1)
            .WithUpgrade(1)
    ];


    // =========================================================
    // PLAY
    // =========================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        // Store how many cards should be replayed next turn.
        //
        // Base:      1
        // Upgraded:  2

        await PowerCmd.Apply<ForeshadowingPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["Cards"].IntValue,
            Owner.Creature,
            this);


        // Forshadow immediately ends the current turn.
        PlayerCmd.EndTurn(
            Owner,
            canBackOut: false);
    }
}