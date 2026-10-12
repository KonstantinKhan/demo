using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Ascon.Plm.Loodsman.PluginSDK;

namespace ExecutionsPlugin
{
    /// <summary>Строка грида: свойства Q1..Q5 — количество по исполнениям 01..05.</summary>
    public sealed class GridRow
    {
        public string Designation { get; set; }
        public string Name { get; set; }
        public string Q1 { get; set; }
        public string Q2 { get; set; }
        public string Q3 { get; set; }
        public string Q4 { get; set; }
        public string Q5 { get; set; }
    }

    internal sealed class ExecutionsForm : Window
    {
        private readonly INetPluginCall _call;
        private readonly PluginConfig _config;
        private readonly BaseObject _base;
        private readonly ObservableCollection<GridRow> _assemblies = new ObservableCollection<GridRow>();
        private readonly ObservableCollection<GridRow> _details = new ObservableCollection<GridRow>();
        private readonly TextBox _log = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 90
        };
        private readonly Button _createButton = new Button { Content = "Создать", Width = 110, Margin = new Thickness(0, 0, 8, 0) };

        public ExecutionsForm(INetPluginCall call, PluginConfig config, BaseObject baseObject)
        {
            _call = call;
            _config = config;
            _base = baseObject;

            Title = $"Исполнения: {baseObject.Product} {baseObject.Name}".Trim();
            Width = 900;
            Height = 700;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new Grid { Margin = new Thickness(10) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var assembliesBox = Section("Сборочные единицы", _assemblies);
            var detailsBox = Section("Детали", _details);
            Grid.SetRow(assembliesBox, 0);
            Grid.SetRow(detailsBox, 1);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 8) };
            _createButton.Click += CreateButton_Click;
            var closeButton = new Button { Content = "Закрыть", Width = 110 };
            closeButton.Click += (s, e) => Close();
            buttons.Children.Add(_createButton);
            buttons.Children.Add(closeButton);
            Grid.SetRow(buttons, 2);

            Grid.SetRow(_log, 3);

            root.Children.Add(assembliesBox);
            root.Children.Add(detailsBox);
            root.Children.Add(buttons);
            root.Children.Add(_log);
            Content = root;
        }

        private static GroupBox Section(string header, ObservableCollection<GridRow> items)
        {
            var grid = new DataGrid
            {
                ItemsSource = items,
                AutoGenerateColumns = false,
                CanUserAddRows = true,
                CanUserDeleteRows = true,
                Margin = new Thickness(4)
            };
            grid.Columns.Add(Column("Обозначение", nameof(GridRow.Designation), new DataGridLength(2, DataGridLengthUnitType.Star)));
            grid.Columns.Add(Column("Наименование", nameof(GridRow.Name), new DataGridLength(3, DataGridLengthUnitType.Star)));
            for (var i = 1; i <= TableRow.ExecutionCount; i++)
            {
                grid.Columns.Add(Column($"{i:00}", $"Q{i}", new DataGridLength(1, DataGridLengthUnitType.Star)));
            }

            var box = new GroupBox { Header = header, Content = grid, Margin = new Thickness(0, 0, 0, 6) };
            return box;
        }

        private static DataGridTextColumn Column(string header, string path, DataGridLength width) =>
            new DataGridTextColumn { Header = header, Binding = new Binding(path), Width = width };

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            _log.Clear();
            _createButton.IsEnabled = false;
            try
            {
                // Фиксируем правку в ячейке, которая ещё в режиме редактирования.
                CommitEdits(this);

                var rows = _assemblies.Select(r => ToRow(r, RowKind.Assembly))
                    .Concat(_details.Select(r => ToRow(r, RowKind.Detail)));
                var plan = ExecutionPlanBuilder.Build(_base, rows, _config);

                if (plan.Errors.Count > 0)
                {
                    Log("Ошибки в таблице, ничего не создано:\n" + string.Join("\n", plan.Errors));
                    return;
                }

                if (plan.IsEmpty)
                {
                    Log("Нет данных: заполните хотя бы одно количество.");
                    return;
                }

                var executor = new LoodsmanExecutor(_call, _config, Log);
                executor.Run(_base, plan);
                Log("Готово.");
                MessageBox.Show(this, "Исполнения созданы.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                MessageBox.Show(this, ex.ToString(), "Исполнения: ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _createButton.IsEnabled = true;
            }
        }

        private static TableRow ToRow(GridRow row, RowKind kind) => new TableRow
        {
            Kind = kind,
            Designation = row.Designation ?? string.Empty,
            Name = row.Name ?? string.Empty,
            Quantities = new[] { row.Q1, row.Q2, row.Q3, row.Q4, row.Q5 }
        };

        private static void CommitEdits(DependencyObject root)
        {
            foreach (var grid in Descendants<DataGrid>(root))
            {
                grid.CommitEdit(DataGridEditingUnit.Row, true);
            }
        }

        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
        {
            var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in Descendants<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private void Log(string text)
        {
            _log.AppendText(text + Environment.NewLine);
            _log.ScrollToEnd();
        }
    }
}
