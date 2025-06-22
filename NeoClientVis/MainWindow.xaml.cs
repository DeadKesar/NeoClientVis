using Neo4jClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace NeoClientVis
{
    public partial class MainWindow : Window
    {
        private GraphClient _client;
        private NodeTypeCollection _nodeTypeCollection;
        private Dictionary<string, Control[]> _filterControls;
        private NodeData _selectedNodeForRelationship;

        public MainWindow()
        {
            InitializeComponent();
            InitializeNeo4jClient();
            LoadDataAsync();
        }
        /// <summary>
        /// подключение к бд
        /// </summary>
        private void InitializeNeo4jClient()
        {
            _client = new GraphClient(new Uri("http://localhost:7474"), "neo4j", "12345678a");
            _client.ConnectAsync().Wait(); // Синхронное подключение для простоты
        }
        /// <summary>
        /// выгрузка состояния из БД
        /// </summary>
        private async void LoadDataAsync()
        {
            try
            {
                await _client.ConnectAsync();
                _nodeTypeCollection = await BDController.LoadNodeTypesFromDb(_client);

                // Пересчитываем счетчик на случай ручного изменения данных
                _nodeTypeCollection.RecalculateCount();

                foreach (var nodeType in _nodeTypeCollection.NodeTypes)
                {
                    string label = nodeType.Label.Values.First();
                    await BDController.UpdateBoolProperties(_client, label, "Актуальность");
                }

                NodeTypeComboBox.ItemsSource = _nodeTypeCollection.NodeTypes.Select(nt => nt.Label.First().Key);
                if (_nodeTypeCollection.NodeTypes.Count > 0)
                {
                    NodeTypeComboBox.SelectedIndex = 0;
                }
                else
                {
                    NodesListBox.ItemsSource = null;
                    FilterPanel.Children.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
                Console.WriteLine($"Ошибка в LoadDataAsync: {ex}");
            }
        }
        /// <summary>
        /// вывод основной инфы об избранном типе
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void NodeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    // Загружаем все узлы без фильтров
                    string label = selectedNodeType.Label.Values.First();
                    var nodes = await BDController.LoadNodesByType(_client, label);
                    NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p =>
                        p.Key == "Дата" && p.Value is Neo4j.Driver.LocalDate localDate
                            ? $"{p.Key}: {new DateTime(localDate.Year, localDate.Month, localDate.Day):yyyy-MM-dd}"
                            : $"{p.Key}: {p.Value}"))}");  //основной заполнитель объектов строками

                    // Создаём фильтры
                    CreateFilterControls(selectedNodeType.Properties);
                }
            }
        }
        /// <summary>
        /// вывод всех свойств в окне свойств
        /// </summary>
        /// <param name="properties"> свойства которые есть у текущего типа</param>
        private void CreateFilterControls(Dictionary<string, Type> properties)
        {
            FilterPanel.Children.Clear();
            _filterControls = new Dictionary<string, Control[]>();

            foreach (var property in properties)
            {
                var label = new Label { Content = $"{property.Key}:" };
                Control[] controls;

                if (property.Value == typeof(bool))
                {
                    var trueCheckBox = new CheckBox { Content = "Актуальные", Margin = new Thickness(0, 0, 10, 5), IsChecked = true };
                    var falseCheckBox = new CheckBox { Content = "Неактуальные", Margin = new Thickness(0, 0, 0, 5), IsChecked = true };
                    trueCheckBox.Checked += FilterControl_Changed;
                    trueCheckBox.Unchecked += FilterControl_Changed;
                    falseCheckBox.Checked += FilterControl_Changed;
                    falseCheckBox.Unchecked += FilterControl_Changed;
                    controls = new Control[] { trueCheckBox, falseCheckBox };
                    var boolPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    boolPanel.Children.Add(trueCheckBox);
                    boolPanel.Children.Add(falseCheckBox);
                    FilterPanel.Children.Add(label);
                    FilterPanel.Children.Add(boolPanel);
                }
                else if (property.Value == typeof(DateTime) || property.Value == typeof(Neo4j.Driver.LocalDate))
                {
                    var fromLabel = new Label { Content = "От:", Margin = new Thickness(10, 0, 0, 0) };
                    var fromDatePicker = new DatePicker { Width = 100, Margin = new Thickness(0, 0, 0, 5) };
                    var toLabel = new Label { Content = "До:", Margin = new Thickness(10, 0, 0, 0) };
                    var toDatePicker = new DatePicker { Width = 100, Margin = new Thickness(0, 0, 0, 5) };
                    fromDatePicker.SelectedDateChanged += (s, e) =>
                    {
                        if (fromDatePicker.SelectedDate.HasValue && toDatePicker.SelectedDate.HasValue &&
                            fromDatePicker.SelectedDate > toDatePicker.SelectedDate)
                        {
                            MessageBox.Show("Дата 'От' не может быть позже даты 'До'.");
                            fromDatePicker.SelectedDate = null;
                        }
                    };
                    toDatePicker.SelectedDateChanged += FilterControl_Changed;
                    controls = new Control[] { fromDatePicker, toDatePicker };
                    var datePanel = new StackPanel { Orientation = Orientation.Horizontal };
                    datePanel.Children.Add(fromLabel);
                    datePanel.Children.Add(fromDatePicker);
                    datePanel.Children.Add(toLabel);
                    datePanel.Children.Add(toDatePicker);
                    FilterPanel.Children.Add(label);
                    FilterPanel.Children.Add(datePanel);
                }
                else // string
                {
                    var textBox = new TextBox { Width = 200, Margin = new Thickness(0, 0, 0, 5) };
                    textBox.TextChanged += FilterControl_Changed;
                    controls = new Control[] { textBox };
                    FilterPanel.Children.Add(label);
                    FilterPanel.Children.Add(textBox);
                }

                _filterControls[property.Key] = controls;
            }
        }

        /// <summary>
        /// следит за полями фильтров и применяет их изменения в выводе основных данных
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void FilterControl_Changed(object sender, RoutedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    var filters = new Dictionary<string, object>();
                    foreach (var kvp in _filterControls)
                    {
                        if (selectedNodeType.Properties[kvp.Key] == typeof(bool))
                        {
                            var trueCheck = (kvp.Value[0] as CheckBox)?.IsChecked ?? false;
                            var falseCheck = (kvp.Value[1] as CheckBox)?.IsChecked ?? false;
                            if (trueCheck && !falseCheck)
                                filters[kvp.Key] = true;
                            else if (!trueCheck && falseCheck)
                                filters[kvp.Key] = false;
                            // Если обе или ни одна не выбраны, фильтр не применяется
                        }
                        else if (selectedNodeType.Properties[kvp.Key] == typeof(DateTime) ||
                                 selectedNodeType.Properties[kvp.Key] == typeof(Neo4j.Driver.LocalDate))
                        {
                            var fromDate = (kvp.Value[0] as DatePicker)?.SelectedDate;
                            var toDate = (kvp.Value[1] as DatePicker)?.SelectedDate;
                            if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
                            {
                                MessageBox.Show("Дата 'От' не может быть позже даты 'До'.");
                                return;
                            }
                            if (fromDate.HasValue || toDate.HasValue)
                                filters[kvp.Key] = new { From = fromDate, To = toDate };
                        }
                        else // string
                        {
                            var text = (kvp.Value[0] as TextBox)?.Text;
                            if (!string.IsNullOrWhiteSpace(text))
                                filters[kvp.Key] = text;
                        }
                    }

                    string label = selectedNodeType.Label.Values.First();
                    var nodes = await BDController.LoadFilteredNodes(_client, label, filters);
                    NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p =>
                        p.Key == "Дата" && p.Value is Neo4j.Driver.LocalDate localDate
                            ? $"{p.Key}: {new DateTime(localDate.Year, localDate.Month, localDate.Day):yyyy-MM-dd}"
                            : $"{p.Key}: {p.Value}"))}");
                }
            }
        }




        /// <summary>
        /// кнопка для добавления свойств к текущему типу (объект типа)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddPropertyButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    var addPropertyWindow = new AddPropertyWindow();
                    addPropertyWindow.Owner = this;
                    if (addPropertyWindow.ShowDialog() == true)
                    {
                        string newProperty = addPropertyWindow.PropertyName;
                        Type newPropertyType = addPropertyWindow.PropertyType;

                        if (!selectedNodeType.Properties.ContainsKey(newProperty))
                        {
                            selectedNodeType.Properties[newProperty] = newPropertyType;
                            string label = selectedNodeType.Label.Values.First();
                            object defaultValue = newPropertyType == typeof(DateTime) ? DateTime.MinValue : newPropertyType == typeof(bool) ? false : "";
                            await BDController.UpdateNodesWithNewProperty(_client, label, newProperty, defaultValue);
                            await BDController.SaveNodeTypesToDb(_client, _nodeTypeCollection);

                            // Пересоздаём фильтры
                            CreateFilterControls(selectedNodeType.Properties);

                            var nodes = await BDController.LoadNodesByType(_client, label);
                            NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p =>
                                p.Key == "Дата" && p.Value is Neo4j.Driver.LocalDate localDate
                                    ? $"{p.Key}: {new DateTime(localDate.Year, localDate.Month, localDate.Day):yyyy-MM-dd}"
                                    : $"{p.Key}: {p.Value}"))}");
                        }
                        else
                        {
                            MessageBox.Show("Свойство с таким названием уже существует!");
                        }
                    }
                }
            }
        }
        /// <summary>
        /// создать объект нового типа
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddNodeTypeButton_Click(object sender, RoutedEventArgs e)
        {
            string newTypeName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите название нового типа:",
                "Добавление типа узла",
                "",
                -1, -1);

            if (!string.IsNullOrWhiteSpace(newTypeName))
            {
                // Проверяем уникальность по человекочитаемому имени
                if (!_nodeTypeCollection.NodeTypes.Any(nt => nt.Label.ContainsKey(newTypeName)))
                {
                    _nodeTypeCollection.AddNodeType(newTypeName);
                    await BDController.SaveNodeTypesToDb(_client, _nodeTypeCollection);

                    NodeTypeComboBox.ItemsSource = null;
                    NodeTypeComboBox.ItemsSource = _nodeTypeCollection.NodeTypes.Select(nt => nt.Label.First().Key);
                    NodeTypeComboBox.SelectedItem = newTypeName;
                }
                else
                {
                    MessageBox.Show("Тип с таким названием уже существует!");
                }
            }
        }
        /// <summary>
        /// добавление нового объекта текущего типа
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void AddNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    var addNodeWindow = new AddNodeWindow(selectedNodeType.Properties);
                    addNodeWindow.Owner = this;
                    if (addNodeWindow.ShowDialog() == true)
                    {
                        string label = selectedNodeType.Label.Values.First();
                        await BDController.AddNodeToDb(_client, label, addNodeWindow.Properties, selectedNodeType.Properties);

                        var nodes = await BDController.LoadNodesByType(_client, label);
                        NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p =>
                            p.Key == "Дата" && p.Value is Neo4j.Driver.LocalDate localDate
                                ? $"{p.Key}: {new DateTime(localDate.Year, localDate.Month, localDate.Day):yyyy-MM-dd}"
                                : $"{p.Key}: {p.Value}"))}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Сначала выберите тип узла!");
            }
        }


        
        /// <summary>
        /// Обработчики контекстного меню, выподающее меню под левой кнопкой
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void PropertiesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NodesListBox.SelectedItem is string selectedNodeString)
            {
                var selectedType = NodeTypeComboBox.SelectedItem as string;
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    string label = selectedNodeType.Label.Values.First();
                    var nodes = await BDController.LoadNodesByType(_client, label);
                    var selectedNode = nodes.FirstOrDefault(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}" == selectedNodeString);

                    if (selectedNode != null)
                    {
                        var editNodeWindow = new EditNodeWindow(selectedNode.Properties, selectedNodeType.Properties);
                        editNodeWindow.Owner = this;
                        if (editNodeWindow.ShowDialog() == true)
                        {
                            await BDController.UpdateNodeProperties(_client, label, selectedNode.Properties, editNodeWindow.Properties);

                            nodes = await BDController.LoadNodesByType(_client, label);
                            NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p =>
                                p.Key == "Дата" && p.Value is Neo4j.Driver.LocalDate localDate
                                    ? $"{p.Key}: {new DateTime(localDate.Year, localDate.Month, localDate.Day):yyyy-MM-dd}"
                                    : $"{p.Key}: {p.Value}"))}");
                        }
                    }
                }
            }
        }
        /// <summary>
        /// кнопка удаления
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NodesListBox.SelectedItem is string selectedNodeString)
            {
                var selectedType = NodeTypeComboBox.SelectedItem as string;
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    string label = selectedNodeType.Label.Values.First();
                    var nodes = await BDController.LoadNodesByType(_client, label);
                    var selectedNode = nodes.FirstOrDefault(n => $"Node: {string.Join(", ", n.Properties.Select(p =>
                        p.Key == "Дата" && p.Value is DateTime date
                            ? $"{p.Key}: {date:yyyy-MM-dd}"
                            : $"{p.Key}: {p.Value}"))}" == selectedNodeString);

                    if (selectedNode != null)
                    {
                        var result = MessageBox.Show($"Вы уверены, что хотите удалить узел '{selectedNodeString}'?",
                            "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            await BDController.DeleteNode(_client, label, selectedNode.Properties);
                            nodes = await BDController.LoadNodesByType(_client, label);
                            NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p =>
                                p.Key == "Дата" && p.Value is Neo4j.Driver.LocalDate localDate
                                    ? $"{p.Key}: {new DateTime(localDate.Year, localDate.Month, localDate.Day):yyyy-MM-dd}"
                                    : $"{p.Key}: {p.Value}"))}");
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите узел для удаления!");
            }
        }
        /// <summary>
        /// кнопка сброса фильтров
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var kvp in _filterControls)
            {
                if (kvp.Value[0] is CheckBox trueCheckBox)
                {
                    trueCheckBox.IsChecked = true;
                    (kvp.Value[1] as CheckBox).IsChecked = true;
                }
                else if (kvp.Value[0] is DatePicker fromDatePicker)
                {
                    fromDatePicker.SelectedDate = null;
                    (kvp.Value[1] as DatePicker).SelectedDate = null;
                }
                else
                {
                    (kvp.Value[0] as TextBox).Text = "";
                }
            }
            FilterControl_Changed(sender, e);
        }

        private void CreateRelationshipButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodesListBox.SelectedItem is string selectedNodeString)
            {
                // Сохраняем выбранный узел для будущей связи
                var selectedType = NodeTypeComboBox.SelectedItem as string;
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));

                if (selectedNodeType != null)
                {
                    string label = selectedNodeType.Label.Values.First();
                    var node = BDController.GetNodeFromString(selectedNodeString, label);
                    _selectedNodeForRelationship = node;

                    // Открываем окно поиска
                    var findWindow = new FindNodeWindow(_client, _nodeTypeCollection);
                    findWindow.Owner = this;
                    if (findWindow.ShowDialog() == true)
                    {
                        NodeData targetNode = findWindow.SelectedNode;
                        if (targetNode != null)
                        {
                            // Создаем связь
                            string relationshipType = "СВЯЗАН_С"; // Можно сделать выбор типа связи
                            BDController.CreateRelationship(_client, _selectedNodeForRelationship, targetNode, relationshipType);
                            MessageBox.Show("Связь успешно создана!");
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите исходный документ для связи!");
            }
        }


        // Заглушки для остальных пунктов меню
        private void GoToMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Перейти' пока не реализована."); }
        private void RelatedMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Связанные' пока не реализована."); }
        private void AddMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Добавить' пока не реализована."); }
        private void ReplaceMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Заменить' пока не реализована."); }
    }
}
