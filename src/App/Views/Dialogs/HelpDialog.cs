using System.Diagnostics.CodeAnalysis;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace DataMorph.App.Views.Dialogs;

/// <summary>
/// Modal dialog displaying application key bindings and help information.
/// </summary>
internal sealed class HelpDialog : Dialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HelpDialog"/> class.
    /// </summary>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Child views are owned by Dialog and disposed when Dialog is disposed."
    )]
    internal HelpDialog()
    {
        Title = "Help - Key Bindings";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Absolute(54);
        Height = Dim.Absolute(32);

        var helpText = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 1,
            ReadOnly = true,
            Text = GetHelpText(),
        };

        Add(helpText);

        var closeButton = new Button { Text = "Close", IsDefault = true };
        closeButton.Accepting += (s, e) => App?.RequestStop();
        AddButton(closeButton);
    }

    private static string GetHelpText()
    {
        return """
            Global / File Operations
            -------------------------
            o       : Open File
            s       : Save Recipe
            q       : Quit
            t       : Toggle Tree/Table View (JSON Lines)
            x       : Context-Sensitive Action Menu
            ?       : Help (this overlay)

            Navigation
            ----------
            h/j/k/l : Move Left/Down/Up/Right
            gg      : Jump to first row
            G       : Jump to last row
            Enter   : Expand/Collapse (Tree View)

            Context Actions (via 'x' menu)
            ------------------------------
            Rename  : Rename the current column
            Delete  : Remove the current column
            Cast    : Change column data type
            Filter  : Add a filter based on current column
            Fill    : Fill empty cells in column
            Format  : Format timestamp columns
            """;
    }

    /// <inheritdoc/>
    protected override bool OnKeyDown(Key key)
    {
        var baseKey = (char)(key.KeyCode & KeyCode.CharMask);
        var baseKeyLower = char.ToLowerInvariant(baseKey);

        if (key.KeyCode == KeyCode.Esc || baseKeyLower == 'q' || baseKeyLower == '?')
        {
            App?.RequestStop();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
