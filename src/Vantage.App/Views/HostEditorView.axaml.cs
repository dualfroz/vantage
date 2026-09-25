using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Vantage.App.Views;

public partial class HostEditorView : UserControl
{
    public HostEditorView() => AvaloniaXamlLoader.Load(this);
}
