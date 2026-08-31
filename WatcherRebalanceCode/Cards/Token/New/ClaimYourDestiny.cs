using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;
using WatcherRebalance.WatcherRebalanceCode.Powers;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Token.New;

[Pool(typeof(TokenCardPool))]
public sealed class ClaimYourDestiny
    : DivineInterventionChoice
{
    public ClaimYourDestiny()
        : base(CardType.Power)
    {
    }


    public override async Task ResolveChoice(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay,
        DivineIntervention source)
    {
        // =====================================================
        // FIND CURRENT COMBAT ROOM
        // =====================================================

        if (source.CombatState?.RunState.CurrentRoom
            is not CombatRoom combatRoom)
        {
            return;
        }


        // =====================================================
        // ADD EXTRA CARD REWARD
        // =====================================================
        //
        // Same basic implementation as The Hunt.
        // =====================================================

        combatRoom.AddExtraReward(
            Owner,
            new CardReward(
                CardCreationOptions.ForRoom(
                    Owner,
                    combatRoom.RoomType),
                3,
                Owner));


        // =====================================================
        // VISUAL INDICATOR
        // =====================================================
        //
        // This Power does not create the reward.
        //
        // It exists purely as a visible counter, just like
        // TheHuntPower.
        //
        // Selecting this option repeatedly will therefore show:
        //
        //     Divine Intervention 1
        //     Divine Intervention 2
        //     Divine Intervention 3
        //     ...
        // =====================================================

        await PowerCmd.Apply<DestinyPower>(
            choiceContext,
            Owner.Creature,
            1M,
            Owner.Creature,
            source);
    }
}