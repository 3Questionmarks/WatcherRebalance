/*Disabling Till I fix

using BaseLib.Cards.Variables;
using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
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

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new ScryVar(3).WithUpgrade(2)
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


        Player cardOwner =
            Owner;


        Player[] players =
            CombatState.Players
                .Where(player =>
                    player.Creature.IsAlive)
                .ToArray();


        // ============================================================
        // 1. APPLY EVERY PLAYER'S MARKER POWER
        // ============================================================
        //
        // No Scry happens during PowerCmd.Apply anymore.
        // ============================================================

        List<ManipulateRealityPower> powers =
            new();


        foreach (Player player in players)
        {
            ManipulateRealityPower? power =
                await PowerCmd.Apply<ManipulateRealityPower>(
                    choiceContext,
                    player.Creature,
                    scryAmount,
                    cardOwner.Creature,
                    this);


            if (power != null)
            {
                powers.Add(power);
            }
        }


        // ============================================================
        // 2. FIND THE CARD OWNER'S POWER
        // ============================================================

        ManipulateRealityPower? ownerPower =
            powers.FirstOrDefault(power =>
                power.Owner.Player == cardOwner);


        // ============================================================
        // 3. START EVERY OTHER PLAYER
        // ============================================================
        //
        // Each OTHER player receives their own HookPlayerChoiceContext.
        //
        // We only wait until the effect:
        //
        //     A) completes immediately
        //
        // or
        //
        //     B) reaches Scry and pauses.
        //
        // We deliberately DO NOT WaitForCompletion afterward.
        //
        // Once a player choice creates a GenericHookGameAction, that
        // effect is now independently synchronized through that
        // player's action queue.
        // ============================================================

        foreach (ManipulateRealityPower power in powers)
        {
            Player? affectedPlayer =
                power.Owner.Player;


            if (affectedPlayer == null)
                continue;


            // The card owner's effect MUST stay on the original
            // PlayCardAction.
            if (affectedPlayer == cardOwner)
                continue;


            var remoteContext =
                new HookPlayerChoiceContext(
                    power,
                    affectedPlayer,
                    localNetId.Value,
                    GameActionType.Combat);


            Task remoteTask =
                power.Resolve(
                    remoteContext);


            await remoteContext
                .AssignTaskAndWaitForPauseOrCompletion(
                    remoteTask);


            // IMPORTANT:
            //
            // Do NOT:
            //
            //     await remoteContext.WaitForCompletion();
            //
            // The whole point is that this player's resulting
            // GenericHookGameAction should now live independently.
        }


        // ============================================================
        // 4. RESOLVE THE CARD OWNER
        // ============================================================
        //
        // The owner uses the ORIGINAL card choice context.
        //
        // When Scry requests a player choice, the normal PlayCardAction
        // pauses.
        //
        // That pause frees ActionExecutor to start all the remote
        // GenericHookGameActions we queued above.
        //
        // Result:
        //
        //     Owner PlayCardAction -> GatheringPlayerChoice
        //     Player B HookAction  -> GatheringPlayerChoice
        //     Player C HookAction  -> GatheringPlayerChoice
        //
        // simultaneously.
        // ============================================================

        if (ownerPower != null)
        {
            await ownerPower.Resolve(
                choiceContext);
        }


        // ============================================================
        // DONE
        // ============================================================
        //
        // We intentionally do NOT wait for remote players here.
        //
        // Their effects now belong to their own synchronized hook
        // actions.
        //
        // If the card owner had no cards available to Scry, their
        // Resolve() completes immediately and this PlayCardAction ends,
        // allowing the already-enqueued remote hook actions to begin.
        // ============================================================
    }


    protected override void OnUpgrade()
    {
    }
}
*/