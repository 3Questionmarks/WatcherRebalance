using System.Reflection;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace WatcherRebalance.WatcherRebalanceCode.Tooltips;

public static class WatcherRebalanceTips
{
    public static IHoverTip Token()
    {
        var title =
            new LocString(
                "static_hover_tips",
                "WATCHER_REBALANCE_TOKEN.title");

        var description =
            new LocString(
                "static_hover_tips",
                "WATCHER_REBALANCE_TOKEN.description");

        // This HoverTip constructor requires an icon argument.
        // Token is a text-only tooltip, so pass null.
        return new HoverTip(
            title,
            description,
            null);
    }

    public static void AddTokenTip(
        ConstructedCardModel card)
    {
        MethodInfo? withTips =
            typeof(ConstructedCardModel)
                .GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                    m.Name == "WithTips" &&
                    m.GetParameters().Length == 1);

        if (withTips == null)
        {
            throw new MissingMethodException(
                "Could not find ConstructedCardModel.WithTips.");
        }

        Func<CardModel, IEnumerable<IHoverTip>> tooltipFactory =
            _ =>
            [
                Token()
            ];

        withTips.Invoke(
            card,
            [tooltipFactory]);
    }
    
    public static IHoverTip Enchant()
    {
        var title =
            new LocString(
                "static_hover_tips",
                "WATCHER_REBALANCE_ENCHANT.title");

        var description =
            new LocString(
                "static_hover_tips",
                "WATCHER_REBALANCE_ENCHANT.description");

        return new HoverTip(
            title,
            description,
            null);
    }
}