using Terminal.Gui.Views;

namespace DataMorph.App.Views.Dialogs;

/// <summary>
/// Extension methods for <see cref="OptionSelector{TEnum}"/> to enable auto-select on focus.
/// </summary>
internal static class OptionSelectorExtensions
{
    /// <summary>
    /// Configures <see cref="OptionSelector{TEnum}"/> to auto-select focused option when arrow key navigation is used.
    /// </summary>
    /// <param name="selector">The option selector to configure.</param>
    internal static void EnableAutoSelectOnFocus<TEnum>(this OptionSelector<TEnum> selector)
        where TEnum : struct, Enum
    {
        foreach (var cb in selector.SubViews.OfType<CheckBox>())
        {
            cb.HasFocusChanged += (s, e) =>
            {
                if (cb.HasFocus)
                {
                    var valInt = selector.GetCheckBoxValue(cb);
                    selector.Value = (TEnum)(object)valInt;
                }
            };
        }
    }
}
