using BaseLib.Cards.Variables;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using Watcher.Code.Character;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;

[Pool(typeof(WatcherCardPool))]
public sealed class ManipulateReality()
    : WatcherRebalanceCard(
        1,
        CardType.Skill,
        CardRarity.Uncommon,
        TargetType.None)
{
    // ================================================================
    // SCRY
    // ================================================================
    //
    // Base:    3
    // Upgrade: 5
    //
    // ================================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ScryVar(3).WithUpgrade(2)
    ];


    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;


    // ================================================================
    // ON PLAY
    // ================================================================
    //
    // The card itself does NOT perform anybody's Scry.
    //
    // Instead, every living player receives their own
    // ManipulateRealityPower.
    //
    // Each copy of that power immediately resolves for its own
    // player and then removes itself.
    //
    // ================================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState == null)
            return;


        int scryAmount =
            DynamicVars.Scry().IntValue;


        Player[] players =
            CombatState.Players
                .Where(player =>
                    player.Creature.IsAlive)
                .ToArray();


        // ============================================================
        // APPLY ALL PLAYER POWERS AT ONCE
        // ============================================================
        //
        // Do NOT await them one at a time.
        //
        // Each player's power application can therefore proceed
        // independently through the multiplayer choice context.
        //
        // ============================================================

        Task[] applications =
            players
                .Select(player =>
                    ApplyToPlayer(
                        choiceContext,
                        player,
                        scryAmount))
                .ToArray();


        await Task.WhenAll(applications);
    }


    // ================================================================
    // APPLY TO ONE PLAYER
    // ================================================================

    private async Task ApplyToPlayer(
        PlayerChoiceContext choiceContext,
        Player player,
        int scryAmount)
    {
        await PowerCmd.Apply<ManipulateRealityPower>(
            choiceContext,
            player.Creature,

            // The power amount stores the Scry amount.
            scryAmount,

            // The Watcher remains the source/applier of the effect.
            Owner.Creature,

            // Card source.
            this);
    }


    protected override void OnUpgrade()
    {
    }
}