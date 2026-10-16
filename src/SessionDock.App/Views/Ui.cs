using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace SessionDock.App.Views;

internal static class Ui
{
    public static SolidColorBrush Brush(string key) => (SolidColorBrush)System.Windows.Application.Current.FindResource(key);
    public static TextBlock Text(string text, double size = 14, string brush = "TextBrush")
    {
        var block = new TextBlock { Text = text, FontSize = size, Margin = new Thickness(0, 3, 0, 7), TextWrapping = TextWrapping.Wrap };
        if (size >= 18) block.FontWeight = FontWeights.SemiBold;
        block.SetResourceReference(TextBlock.ForegroundProperty, brush); return block;
    }
    public static Button Button(string label, Action action)
    {
        var b = new Button { Content = label }; AutomationProperties.SetName(b, label); b.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception error) { if (Window.GetWindow(b) is { } owner) Error(owner, error); else MessageBox.Show(error.Message, "SessionDock", MessageBoxButton.OK, MessageBoxImage.Error); }
        }; return b;
    }
    public static TextBox Input(string label, string? value, Panel parent)
    {
        parent.Children.Add(Text(label)); var box = new TextBox { Text = value ?? "" }; AutomationProperties.SetName(box, label); parent.Children.Add(box); return box;
    }
    public static CheckBox Check(string text, bool value, Panel parent, bool enabled = true)
    {
        var c = new CheckBox { Content = text, IsChecked = value, IsEnabled = enabled }; AutomationProperties.SetName(c, text); parent.Children.Add(c); return c;
    }
    public static ComboBox Combo<T>(string label, IEnumerable<T> items, T selected, Panel parent)
    {
        parent.Children.Add(Text(label)); var c = new ComboBox { ItemsSource = items.ToList(), SelectedItem = selected }; AutomationProperties.SetName(c, label); if (c.SelectedIndex < 0) c.SelectedIndex = 0; parent.Children.Add(c); return c;
    }
    public static void Note(Panel panel, string text) => panel.Children.Add(Text(text, 12, "MutedBrush"));
    public static StackPanel Stack(double margin = 16) => new() { Margin = new Thickness(margin) };
    public static ScrollViewer Scroll(UIElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    public static void Error(Window owner, Exception error) => MessageBox.Show(owner, error.Message, "SessionDock", MessageBoxButton.OK, MessageBoxImage.Error);
    public static bool Confirm(Window owner, string text) => MessageBox.Show(owner, text, "SessionDock", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
    public static string? Ask(Window owner, string title, string label, string initial = "")
    {
        var dialog = new Window { Owner = owner, Title = title, Width = 450, Height = 230, MinWidth = 330, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.CanResize };
        var root = Stack(); var text = Input(label, initial, root); var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = Button("Save", () => { if (!string.IsNullOrWhiteSpace(text.Text)) dialog.DialogResult = true; }); save.IsDefault = true;
        var cancel = Button("Cancel", () => dialog.DialogResult = false); cancel.IsCancel = true; buttons.Children.Add(cancel); buttons.Children.Add(save); root.Children.Add(buttons); dialog.Content = Scroll(root); dialog.Loaded += (_, _) => text.Focus();
        FitToWorkArea(dialog); return dialog.ShowDialog() == true ? text.Text.Trim() : null;
    }
    public static StackPanel Footer(Panel root, Action save, Action cancel)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        var close = Button("Cancel", cancel); close.IsCancel = true; var accept = Button("Save", save); accept.IsDefault = true; buttons.Children.Add(close); buttons.Children.Add(accept); root.Children.Add(buttons); return buttons;
    }
    public static void FitToWorkArea(Window window)
    {
        // WPF implicit Window styles do not match derived window types. Apply
        // the shared style explicitly; its dynamic brushes still follow theme
        // and high-contrast palettes. Window chrome and control behavior remain native.
        window.SetResourceReference(FrameworkElement.StyleProperty, "SessionDockWindowStyle");
        double width = Math.Max(240, SystemParameters.WorkArea.Width - 24);
        double height = Math.Max(180, SystemParameters.WorkArea.Height - 24);
        window.MinWidth = Math.Min(window.MinWidth, width); window.MinHeight = Math.Min(window.MinHeight, height);
        window.Width = Math.Min(window.Width, width); window.Height = Math.Min(window.Height, height);
    }
    public static void Safe(Window owner, Action action) { try { action(); } catch (Exception ex) { Error(owner, ex); } }
}
