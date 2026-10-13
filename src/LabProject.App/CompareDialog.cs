using System.Windows;
using System.Windows.Controls;
using LabProject.Core;

namespace LabProject.App;

/// <summary>
/// Модальный диалог "Сравнить алгоритмы": список алгоритмов (чекбоксы), по
/// которым в БД уже есть история замеров.
/// </summary>
public sealed class CompareDialog : Window
{
    private readonly StackPanel _list = new();

    public List<string> SelectedKeys { get; } = new();

    public CompareDialog(IReadOnlyList<string> availableKeys, Window owner)
    {
        Owner = owner;
        Title = "Сравнить алгоритмы";
        Width = 440;
        Height = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = owner.FontFamily;
        FontSize = 13;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xF4, 0xFA));

        var root = new DockPanel { Margin = new Thickness(16) };

        var info = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
            Text = availableKeys.Count == 0
                ? "В базе данных пока нет подходящих результатов.\nСначала запустите хотя бы один эксперимент."
                : "Отметьте алгоритмы для сравнения (2 и более):",
        };
        DockPanel.SetDock(info, Dock.Top);
        root.Children.Add(info);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var ok = new Button { Content = "OK", Width = 90, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Отмена", Width = 90, IsCancel = true, Style = (Style)Application.Current.FindResource("SecondaryButton") };
        ok.Click += (_, _) =>
        {
            SelectedKeys.Clear();
            foreach (CheckBox cb in _list.Children.OfType<CheckBox>())
                if (cb.IsChecked == true)
                    SelectedKeys.Add((string)cb.Tag);
            // Разрешаем закрыть без выбора - просто ничего не произойдёт
            DialogResult = SelectedKeys.Count >= 1;
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        foreach (string key in availableKeys)
        {
            string label = AlgorithmRegistry.TryGet(key, out var spec) ? spec.DisplayName : key;
            _list.Children.Add(new CheckBox { Content = label, Tag = key, Margin = new Thickness(4, 7, 4, 7) });
        }
        root.Children.Add(new Border
        {
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE4, 0xE8, 0xF0)),
            Background = System.Windows.Media.Brushes.White,
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer { Content = _list, Padding = new Thickness(8) },
        });

        Content = root;
    }
}
