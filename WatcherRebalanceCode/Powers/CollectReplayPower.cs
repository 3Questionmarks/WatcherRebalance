using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Cards.Token;
using Watcher.Code.Cards.Uncommon;
using Watcher.Code.Commands;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

public sealed class CollectReplayPower :
    WatcherRebalancePower
{
    public override PowerType Type =>
        PowerType.Buff;


    public override PowerStackType StackType =>
        PowerStackType.Counter;


    // =========================================================
    // TOOLTIPS
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            /*
             * Show Miracle.
             */
            
            CardModel collect =
                ModelDb.Card<Collect>()
                    .ToMutable();
            
            CardModel miracle =
                ModelDb.Card<Miracle>()
                    .ToMutable();

            miracle.UpgradeInternal();


            return new IHoverTip[]
            {
                HoverTipFactory.FromCard(miracle),
                HoverTipFactory.FromCard<Miracle>(false),

                HoverTipFactory.Static(
                    StaticHoverTip.ReplayStatic)
            };
        }
    }


    // =========================================================
    // START OF TURN
    // =========================================================

    public override async Task BeforeHandDraw(
        Player player,
        PlayerChoiceContext choiceContext,
        ICombatState combatState)
    {
        if (player != Owner.Player)
            return;


        if (Owner.Player == null)
            return;


        /*
         * Remember every card that was already in the Hand.
         *
         * WatcherCmd.GiveCard does not need to return the generated
         * card for us to identify it: after creation we can find
         * the new Hand entry by reference.
         */

        HashSet<CardModel> cardsBefore =
            PileType.Hand
                .GetPile(Owner.Player)
                .Cards
                .ToHashSet();


        // Create the normal Miracle.
        await WatcherCmd.GiveCard<Miracle>(
            Owner.Player,
            PileType.Hand,
            CardPilePosition.Top,
            upgraded: false,
            skipAnimation: true);


        /*
         * Find the newly-created Miracle.
         */

        CardModel? newMiracle =
            PileType.Hand
                .GetPile(Owner.Player)
                .Cards
                .FirstOrDefault(card =>
                    card is Miracle &&
                    !cardsBefore.Contains(card));


        /*
         * Add native Replay.
         *
         * BaseReplayCount++ is the game's actual Replay
         * mechanism and is already working in Carve Reality.
         */

        if (newMiracle != null)
        {
            newMiracle.BaseReplayCount++;
        }


        // One fewer future turn remaining.
        await PowerCmd.Decrement(this);
    }
}