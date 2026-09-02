using BaseLib.Extensions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using Watcher.Code.Character;
using Watcher.Code.Core;
using Watcher.Code.Extensions;
using Watcher.Code.Stances;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Cards.Rare.New;

[Pool(typeof(WatcherCardPool))]
public sealed class Condemn : WatcherRebalanceCard
{
    public Condemn()
        : base(
            3,
            CardType.Attack,
            CardRarity.Rare,
            TargetType.AnyEnemy)
    {
    }


    // =========================================================
    // VARIABLES
    // =========================================================
    //
    // Deal 7(10) damage.
    //
    // Base:    7
    // Upgrade: +2
    // =========================================================

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(
            7,
            ValueProp.Move)
            .WithUpgrade(2)
    ];


    // =========================================================
    // TOOLTIPS
    // =========================================================

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        WatcherRebalanceTips.Divine()
    ];


    // =========================================================
    // GOLD GLOW
    // =========================================================
    //
    // Glow while in Divinity because the execution effect
    // is currently active.
    // =========================================================

    protected override bool ShouldGlowGoldInternal =>
        CombatState != null &&
        Owner.IsInWatcherStance<DivinityStance>();


    // =========================================================
    // UPGRADE
    // =========================================================
    //
    // Damage is upgraded through DamageVar.WithUpgrade(3).
    //
    // Cost remains 3.
    // =========================================================

    protected override void OnUpgrade()
    {
    }


    // =========================================================
    // ON PLAY
    // =========================================================

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature? target =
            cardPlay.Target;


        if (target == null)
            return;


        bool inDivinity =
            Owner.IsInWatcherStance<DivinityStance>();


        // =====================================================
        // DIVINITY EXECUTION
        // =====================================================
        //
        // Normal enemies:
        //     Instantly killed.
        //
        // Elite encounters:
        //     All enemies protected.
        //
        // Boss encounters:
        //     Primary Boss protected.
        //
        //     Secondary/minion enemies can still be executed.
        // =====================================================

        if (inDivinity &&
            !IsProtectedFromCondemn(target))
        {
            // -------------------------------------------------
            // EXECUTION VFX
            // -------------------------------------------------
            //
            // Must happen before HP is set to zero because
            // PlayOnCreatureCenter will not spawn on a dead
            // creature.
            // -------------------------------------------------

            VfxCmd.PlayOnCreatureCenter(
                target,
                "vfx/vfx_fire_burst");


            // -------------------------------------------------
            // EXECUTION SFX
            // -------------------------------------------------

            SfxCmd.Play(
                "event:/sfx/characters/ironclad/ironclad_hellraiser");


            // -------------------------------------------------
            // EXECUTE
            // -------------------------------------------------

            target.SetCurrentHpInternal(
                0);


            await CreatureCmd.Kill(
                target);


            return;
        }


        // =====================================================
        // NORMAL / PROTECTED DAMAGE
        // =====================================================
        //
        // Deal 7(10) damage 3 times.
        //
        // This branch is also used against protected Elite /
        // Boss enemies while Condemn is played in Divinity.
        // =====================================================

        await DamageCmd
            .Attack(
                DynamicVars.Damage.BaseValue)
            .WithHitCount(
                3)
            .FromCard(
                this,
                cardPlay)
            .Targeting(
                target)
            .WithHitFx(
                "vfx/vfx_big_slash_impact",
                null,
                "heavy_attack.mp3")
            .Execute(
                choiceContext);
    }


    // =========================================================
    // ELITE / BOSS PROTECTION
    // =========================================================

    private bool IsProtectedFromCondemn(
        Creature target)
    {
        RoomType? roomType =
            CombatState?
                .Encounter?
                .RoomType;


        // =====================================================
        // ELITE
        // =====================================================
        //
        // Every enemy in an Elite encounter is protected.
        // =====================================================

        if (roomType == RoomType.Elite)
        {
            return true;
        }


        // =====================================================
        // BOSS
        // =====================================================
        //
        // Primary enemy:
        //     Protected.
        //
        // Secondary/minion enemies:
        //     Can be executed.
        // =====================================================

        if (roomType == RoomType.Boss)
        {
            return
                target.IsPrimaryEnemy;
        }


        // =====================================================
        // NORMAL ENCOUNTER
        // =====================================================

        return false;
    }
}
