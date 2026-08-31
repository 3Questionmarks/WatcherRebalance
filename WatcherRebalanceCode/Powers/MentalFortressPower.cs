using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using Watcher.Code.Powers;
using WatcherRebalance.WatcherRebalanceCode.Tooltips;

namespace WatcherRebalance.WatcherRebalanceCode.Powers;

[HarmonyPatch]
public static class MentalFortressPowerPatch
{
    // Disable original:
    // Whenever you change Stances, gain Block.
    [HarmonyPatch(
        typeof(MentalFortressPower),
        nameof(MentalFortressPower.OnStanceChange))]
    [HarmonyPrefix]
    private static bool DisableOriginalStanceEffect(
        ref Task __result)
    {
        __result = Task.CompletedTask;
        return false;
    }
    
    // Replace the original Block tooltip on the power
    // with our Token tooltip.
    [HarmonyPatch(
        typeof(MentalFortressPower),
        "get_ExtraHoverTips")]
    [HarmonyPostfix]
    private static void ExtraHoverTipsPostfix(
        ref IEnumerable<IHoverTip> __result)
    {
        __result =
        [
            WatcherRebalanceTips.Token()
        ];
    }

    // At the start of the player's turn:
    // choose Token cards from Exhaust equal to Amount,
    // or all available Tokens if there are fewer.
    [HarmonyPatch(
        typeof(Hook),
        nameof(Hook.BeforeHandDraw),
        [
            typeof(ICombatState),
            typeof(Player),
            typeof(PlayerChoiceContext)
        ])]
    [HarmonyPostfix]
    private static void BeforeHandDrawPostfix(
        ICombatState __0,
        Player __1,
        PlayerChoiceContext __2,
        ref Task __result)
    {
        __result = HandleBeforeHandDraw(
            __result,
            __1,
            __2);
    }

    private static async Task HandleBeforeHandDraw(
        Task original,
        Player player,
        PlayerChoiceContext choiceContext)
    {
        await original;

        MentalFortressPower? power =
            player.Creature.GetPower<MentalFortressPower>();

        if (power == null ||
            power.Amount <= 0)
        {
            return;
        }

        CardPile exhaustPile =
            PileType.Exhaust.GetPile(player);

        int tokenCount =
            exhaustPile.Cards.Count(IsToken);

        if (tokenCount == 0)
            return;

        int cardsToReturn =
            Math.Min(
                power.Amount,
                tokenCount);

        IEnumerable<CardModel> selected =
            await CardSelectCmd.FromCombatPile(
                choiceContext,
                exhaustPile,
                player,
                new CardSelectorPrefs(
                    new LocString(
                        "cards",
                        "WATCHER-MENTAL_FORTRESS.selectionScreenPrompt"),
                    cardsToReturn),
                IsToken);

        foreach (CardModel card in selected)
        {
            await CardPileCmd.Add(
                card,
                PileType.Hand);
        }
    }

    private static bool IsToken(
        CardModel card)
    {
        return card.Rarity == CardRarity.Token;
    }
}