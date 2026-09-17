using BaseLib.Cards.Variables;
using BaseLib.Commands;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Character;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Multiplayer;

[Pool(typeof(WatcherCardPool))]
public sealed class ManipulateReality()
    : WatcherRebalanceCard(
        1,
        CardType.Skill,

        // Disabled Manipulate Reality becomes a Token card.
        //
        // This prevents it from appearing as a normal Uncommon Watcher reward
        Config.EnableManipulateReality
            ? CardRarity.Uncommon
            : CardRarity.Token,

        TargetType.None,

        // Also hide it from the card library while disabled.
        Config.EnableManipulateReality)
{
    // ================================================================
    // SCRY
    // ================================================================
    //
    // The upgrade no longer increases Scry.
    //
    // Base:
    //     Scry 3.
    //
    // Upgraded:
    //     Scry 3.
    //     The card drawn by each player gains Retain for this turn.
    // ================================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ScryVar(3)
    ];


    public override CardMultiplayerConstraint MultiplayerConstraint =>
        CardMultiplayerConstraint.MultiplayerOnly;


    // ================================================================
    // ON PLAY
    // ================================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        if (CombatState == null)
            return;


        ulong? localNetId =
            LocalContext.NetId;


        if (!localNetId.HasValue)
            return;


        int scryAmount =
            DynamicVars.Scry().IntValue;


        // The upgrade now gives temporary Retain to the card
        // drawn by Manipulate Reality.
        bool retainDrawnCard =
            IsUpgraded;


        Player cardOwner =
            Owner;


        Player[] players =
            CombatState.Players
                .Where(player =>
                    player.Creature.IsAlive)
                .ToArray();


        // ============================================================
        // 1. START EVERY OTHER PLAYER
        // ============================================================
        //
        // Each other player receives their own HookPlayerChoiceContext.
        //
        // If Scry requires a choice, HookPlayerChoiceContext creates a
        // GenericHookGameAction belonging to that player.
        //
        // AssignTaskAndWaitForPauseOrCompletion waits until:
        //
        //     A) the effect completes immediately
        //
        // or
        //
        //     B) Scry pauses for a player choice and the remainder of
        //        the task is transferred to GenericHookGameAction.
        //
        // We deliberately do NOT call WaitForCompletion().
        // ============================================================

        foreach (Player player in players)
        {
            // The card owner's effect stays on the original
            // PlayCardAction and PlayerChoiceContext.
            if (player == cardOwner)
                continue;


            var remoteContext =
                new HookPlayerChoiceContext(
                    this,
                    player,
                    localNetId.Value,
                    GameActionType.Combat);


            Task remoteTask =
                ResolveForPlayer(
                    remoteContext,
                    player,
                    scryAmount,
                    retainDrawnCard);


            await remoteContext
                .AssignTaskAndWaitForPauseOrCompletion(
                    remoteTask);


            // IMPORTANT:
            //
            // Do NOT:
            //
            //     await remoteContext.WaitForCompletion();
            //
            // If Scry required a choice, GenericHookGameAction now
            // owns the remainder of remoteTask.
        }


        // ============================================================
        // 2. RESOLVE THE CARD OWNER
        // ============================================================
        //
        // The owner continues using the original PlayCardAction's
        // PlayerChoiceContext.
        // ============================================================

        await ResolveForPlayer(
            choiceContext,
            cardOwner,
            scryAmount,
            retainDrawnCard);
    }


    // ================================================================
    // RESOLVE ONE PLAYER
    // ================================================================

    private static async Task ResolveForPlayer(
        PlayerChoiceContext choiceContext,
        Player player,
        int scryAmount,
        bool retainDrawnCard)
    {
        if (!player.Creature.IsAlive)
            return;


        // ============================================================
        // SCRY
        // ============================================================

        await ScryCmd.Execute(
            choiceContext,
            player,
            scryAmount);


        if (!player.Creature.IsAlive)
            return;


        // ============================================================
        // DRAW 1
        // ============================================================
        //
        // Get the actual card that Manipulate Reality drew so the
        // upgraded version can give Retain specifically to that card.
        // ============================================================

        CardModel? drawnCard =
            await CardPileCmd.Draw(
                choiceContext,
                player);


        // ============================================================
        // UPGRADE: TEMPORARY RETAIN
        // ============================================================
        //
        // Only the card drawn by Manipulate Reality receives Retain.
        // GiveSingleTurnRetain means the change is temporary rather
        // than permanently adding Retain to the CardModel.
        // ============================================================

        if (retainDrawnCard &&
            drawnCard != null)
        {
            drawnCard.GiveSingleTurnRetain();
        }
    }


    // ================================================================
    // UPGRADE
    // ================================================================
    //
    // No DynamicVar changes are needed.
    //
    // IsUpgraded is checked during play to enable the temporary
    // Retain effect.
    // ================================================================

    protected override void OnUpgrade()
    {
    }
}