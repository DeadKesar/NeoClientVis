using Neo4jClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;  // Для CollectionViewSource
using System.Windows.Data;   // Для Binding
using System.Windows.Threading;

namespace NeoClientVis
{
    public partial class MainWindow : Window
    {
        private GraphClient _client;
        private NodeTypeCollection _nodeTypeCollection;
        private Dictionary<string, Control[]> _filterControls;
        private NodeData _selectedNodeForRelationship;
        private Stack<List<NodeData>> _navigationHistory = new Stack<List<NodeData>>();
        private string _currentViewType; // "Type" или "Related"
        private NodeData _currentContextNode; // Текущий контекстный узел
        private List<NodeData> _currentNodes; // Текущий список узлов для отображения
        private Dictionary<string, IFilter> _customFilters;
        private string _selectedCustomFilter = "None";
        private CollectionViewSource _nodesViewSource;
        private DispatcherTimer _refreshTimer;
        public MainWindow()
        {
            InitializeComponent();
            InitializeNeo4jClient();
            LoadDataAsync();
            _customFilters = new Dictionary<string, IFilter>
            {
                { "None", null },
                { "истёкшие актуальные доки", new ExpiredDocumentsFilter() }
                // Добавьте другие фильтры здесь, например:
                // { "Actual Documents", new ActualDocumentsFilter() }
            };

            CustomFilterComboBox.ItemsSource = _customFilters.Keys;
            CustomFilterComboBox.SelectedItem = "None";

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(10);  // Каждые 10 секунд
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();  // Запустить таймер

        }
        /// <summary>
        /// подключение к бд
        /// </summary>
        private void InitializeNeo4jClient()
        {
            var config = ConfigManager.LoadConfig();
            _client = new GraphClient(new Uri(config.Uri), config.Username, config.Password);
            try
            {
                _client.ConnectAsync().Wait(); // Синхронное для простоты, но можно async
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к Neo4j: {ex.Message}\nПроверьте config.json и доступ к серверу.");
                // Опционально: открыть окно для редактирования конфига
                var editConfigWindow = new EditConfigWindow(config);
                if (editConfigWindow.ShowDialog() == true)
                {
                    ConfigManager.SaveConfig(editConfigWindow.UpdatedConfig);
                    InitializeNeo4jClient(); // Рекурсивно попробовать снова
                }
                else
                {
                    Application.Current.Shutdown(); // Или обработать иначе
                }
            }
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
                _nodesViewSource = new CollectionViewSource();
                foreach (var nodeType in _nodeTypeCollection.NodeTypes)
                {
                    string label = nodeType.Label.Values.First();
                    await BDController.MigrateDatesToLocalDate(_client, label); // Новая миграция
                    await BDController.UpdateBoolProperties(_client, label, "Актуальность");
                }
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
                    // Добавляем вызов создания фильтров для выбранного типа
                    var selectedType = NodeTypeComboBox.SelectedItem as string;
                    var selectedNodeType = _nodeTypeCollection.NodeTypes
                        .FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                    if (selectedNodeType != null)
                    {
                        // Создаём фильтры для начального типа
                        CreateFilterControls(selectedNodeType.Properties);
                        // Загружаем узлы для начального типа
                        string label = selectedNodeType.Label.Values.First();
                        var nodes = await BDController.LoadNodesByType(_client, label);
                        _currentNodes = nodes;
                        _nodesViewSource.Source = _currentNodes;
                        NodesDataGrid.ItemsSource = _nodesViewSource.View;
                        GenerateDataGridColumns(); // Генерируем колонки
                        ApplyCustomFilter();
                    }
                }
                else
                {
                    NodesDataGrid.ItemsSource = null;
                    FilterPanel.Children.Clear();
                    ApplyCustomFilter();
                }
                await BDController.SaveNodeTypesToDb(_client, _nodeTypeCollection);
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
            _navigationHistory.Clear();
            BackButton.Visibility = Visibility.Collapsed;
            _currentViewType = "Type";
            this.Title = "Neo4j Node Viewer";
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    // Загружаем все узлы без фильтров
                    string label = selectedNodeType.Label.Values.First();
                    var nodes = await BDController.LoadNodesByType(_client, label);
                    _currentNodes = nodes;
                    _nodesViewSource.Source = _currentNodes;
                    NodesDataGrid.ItemsSource = _nodesViewSource.View;
                    // Создаём фильтры
                    CreateFilterControls(selectedNodeType.Properties);
                    GenerateDataGridColumns(); // Генерируем колонки
                    ApplyCustomFilter();
                    await RefreshCurrentView();
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

            // Проверка на null
            if (properties == null)
            {
                MessageBox.Show("Ошибка: свойства типа не загружены");
                return;
            }

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
                    fromDatePicker.SelectedDateChanged += FilterControl_Changed;
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
                else // string и другие типы
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
                        else
                        {
                            var text = (kvp.Value[0] as TextBox)?.Text;
                            if (!string.IsNullOrWhiteSpace(text))
                                filters[kvp.Key] = text;
                        }
                    }
                    string label = selectedNodeType.Label.Values.First();
                    var nodes = await BDController.LoadFilteredNodes(_client, label, filters);
                    // Используем DisplayString вместо ручного форматирования
                    _currentNodes = nodes;
                    _nodesViewSource.Source = _currentNodes;
                    NodesDataGrid.ItemsSource = _nodesViewSource.View;
                    GenerateDataGridColumns(); // Генерируем колонки (на случай изменений)
                    ApplyCustomFilter();
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
                            _currentNodes = nodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки (новое свойство добавлено)
                            ApplyCustomFilter();
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
                if (!_nodeTypeCollection.NodeTypes.Any(nt => nt.Label.ContainsKey(newTypeName)))
                {
                    _nodeTypeCollection.AddNodeType(newTypeName);
                    await BDController.SaveNodeTypesToDb(_client, _nodeTypeCollection);
                    // Принудительно обновляем источник данных комбобокса
                    var types = _nodeTypeCollection.NodeTypes.Select(nt => nt.Label.First().Key).ToList();
                    NodeTypeComboBox.ItemsSource = types;
                    NodeTypeComboBox.SelectedItem = newTypeName;
                    // Явно обновляем фильтры
                    var selectedNodeType = _nodeTypeCollection.NodeTypes
                        .FirstOrDefault(nt => nt.Label.ContainsKey(newTypeName));
                    if (selectedNodeType != null)
                    {
                        CreateFilterControls(selectedNodeType.Properties);
                        // Загружаем узлы для нового типа
                        string label = selectedNodeType.Label.Values.First();
                        var nodes = await BDController.LoadNodesByType(_client, label);
                        _currentNodes = nodes;
                        _nodesViewSource.Source = _currentNodes;
                        NodesDataGrid.ItemsSource = _nodesViewSource.View;
                        GenerateDataGridColumns(); // Генерируем колонки для нового типа
                        ApplyCustomFilter();
                    }
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
                        _currentNodes = nodes;
                        _nodesViewSource.Source = _currentNodes;
                        NodesDataGrid.ItemsSource = _nodesViewSource.View;
                        GenerateDataGridColumns(); // Генерируем колонки (на случай изменений)
                        ApplyCustomFilter();
                        await RefreshCurrentView();
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
            if (NodesDataGrid.SelectedItem is NodeData selectedNode)
            {
                try
                {
                    string label;
                    Dictionary<string, Type> propertyTypes;
                    if (_currentViewType == "Type")
                    {
                        var selectedType = NodeTypeComboBox.SelectedItem as string;
                        var selectedNodeType = _nodeTypeCollection.NodeTypes
                            .FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                        if (selectedNodeType == null)
                        {
                            MessageBox.Show("Тип узла не найден!");
                            return;
                        }
                        label = selectedNodeType.Label.Values.First();
                        propertyTypes = selectedNodeType.Properties;
                    }
                    else if (_currentViewType == "Related")
                    {
                        label = selectedNode.Properties.ContainsKey("Label")
                            ? selectedNode.Properties["Label"].ToString()
                            : "Unknown";
                        var nodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt =>
                            nt.Label.Values.Contains(label));
                        propertyTypes = nodeType?.Properties ?? new Dictionary<string, Type>();
                    }
                    else
                    {
                        MessageBox.Show("Неизвестный режим просмотра!");
                        return;
                    }
                    // Фильтруем свойства: исключаем системные "Id" и "Label"
                    var propertiesToEdit = selectedNode.Properties
                        .Where(p => p.Key != "Id" && p.Key != "Label")
                        .ToDictionary(p => p.Key, p => p.Value);
                    var editNodeWindow = new EditNodeWindow(propertiesToEdit, propertyTypes);
                    editNodeWindow.Owner = this;
                    if (editNodeWindow.ShowDialog() == true)
                    {
                        // Передаём propertyTypes для правильной обработки типов (дат, bool)
                        await BDController.UpdateNodeProperties(_client, label,
                            selectedNode.Properties, editNodeWindow.Properties, propertyTypes);
                        // Обновляем текущий список в зависимости от режима
                        if (_currentViewType == "Type")
                        {
                            var nodes = await BDController.LoadNodesByType(_client, label);
                            _currentNodes = nodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки
                            ApplyCustomFilter();
                            await RefreshCurrentView();
                        }
                        else if (_currentViewType == "Related")
                        {
                            var relatedNodes = await BDController.LoadRelatedNodes(_client, _currentContextNode);
                            _currentNodes = relatedNodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки для Related
                            ApplyCustomFilter();
                            await RefreshCurrentView(); 
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при редактировании узла: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Выберите узел для редактирования!");
            }
        }
        /// <summary>
        /// кнопка удаления
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NodesDataGrid.SelectedItem is NodeData selectedNode)
            {
                try
                {
                    string label;
                    if (_currentViewType == "Type")
                    {
                        var selectedType = NodeTypeComboBox.SelectedItem as string;
                        var selectedNodeType = _nodeTypeCollection.NodeTypes
                            .FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                        if (selectedNodeType == null)
                        {
                            MessageBox.Show("Тип узла не найден!");
                            return;
                        }
                        label = selectedNodeType.Label.Values.First();
                    }
                    else if (_currentViewType == "Related")
                    {
                        label = selectedNode.Properties.ContainsKey("Label")
                            ? selectedNode.Properties["Label"].ToString()
                            : "Unknown";
                    }
                    else
                    {
                        MessageBox.Show("Неизвестный режим просмотра!");
                        return;
                    }
                    var result = MessageBox.Show($"Вы уверены, что хотите удалить узел '{selectedNode.DisplayString}'?",
                        "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        await BDController.DeleteNode(_client, label, selectedNode.Properties);
                        // Обновляем текущий список в зависимости от режима
                        if (_currentViewType == "Type")
                        {
                            var nodes = await BDController.LoadNodesByType(_client, label);
                            _currentNodes = nodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки
                            ApplyCustomFilter();
                            await RefreshCurrentView();
                        }
                        else if (_currentViewType == "Related")
                        {
                            var relatedNodes = await BDController.LoadRelatedNodes(_client, _currentContextNode);
                            _currentNodes = relatedNodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки для Related
                            ApplyCustomFilter();
                            await RefreshCurrentView();
                        }
                        MessageBox.Show("Узел успешно удалён.");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении узла: {ex.Message}");
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
            // Сброс динамических фильтров (существующий код)
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

            // Сброс кастомного фильтра
            CustomFilterComboBox.SelectedItem = "None";

            // Применяем изменения (FilterControl_Changed вызовет ApplyCustomFilter)
            FilterControl_Changed(sender, e);
        }

        private async void CreateRelationshipButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodesDataGrid.SelectedItem is NodeData selectedNode)
            {
                _selectedNodeForRelationship = selectedNode;

                // Открываем окно поиска
                var findWindow = new FindNodeWindow(_client, _nodeTypeCollection);
                findWindow.Owner = this;
                if (findWindow.ShowDialog() == true)
                {
                    NodeData targetNode = findWindow.SelectedNode;
                    if (targetNode != null)
                    {
                        try
                        {
                            // Создаем связь
                            string relationshipType = "СВЯЗАН_С";
                            await BDController.CreateRelationship(_client, _selectedNodeForRelationship, targetNode, relationshipType);
                            MessageBox.Show("Связь успешно создана!");

                            // Обновляем данные
                            NodeTypeComboBox_SelectionChanged(null, null);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при создании связи: {ex.Message}");
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите исходный документ для связи!");
            }
        }

        private async void RelatedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NodesDataGrid.SelectedItem is NodeData selectedNode)
            {
                try
                {
                    // Сохраняем текущий вид
                    _navigationHistory.Push(_currentNodes.ToList());
                    // Загружаем связанные узлы
                    var relatedNodes = await BDController.LoadRelatedNodes(_client, selectedNode);
                    _currentNodes = relatedNodes;
                    _nodesViewSource.Source = _currentNodes;
                    NodesDataGrid.ItemsSource = _nodesViewSource.View;
                    GenerateDataGridColumns(); // Генерируем колонки для Related (смешанные типы)
                    ApplyCustomFilter();
                    // Обновляем состояние
                    _currentViewType = "Related";
                    _currentContextNode = selectedNode;
                    // Показываем кнопку "Назад"
                    BackButton.Visibility = Visibility.Visible;
                    // Обновляем заголовок
                    this.Title = $"Связанные с: {selectedNode.DisplayString}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке связанных узлов: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Выберите узел для просмотра связанных!");
            }
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_navigationHistory.Count > 0)
            {
                var previousNodes = _navigationHistory.Pop();
                _currentNodes = previousNodes; // Добавьте это для синхронизации состояния
                _nodesViewSource.Source = _currentNodes;
                NodesDataGrid.ItemsSource = _nodesViewSource.View;
                GenerateDataGridColumns(); // Генерируем колонки (может вернуться к Type)
                ApplyCustomFilter();
                if (_navigationHistory.Count == 0)
                {
                    BackButton.Visibility = Visibility.Collapsed;
                    _currentViewType = "Type";
                    this.Title = "Neo4j Node Viewer";
                    // Перезагружаем актуальный список с текущими фильтрами из БД
                    FilterControl_Changed(null, new RoutedEventArgs());
                }
            }
        }
        private async void GoToMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NodesDataGrid.SelectedItem is NodeData selectedNode)
            {
                try
                {
                    if (selectedNode.Properties.ContainsKey("Путь_к_файлу"))
                    {
                        string filePath = selectedNode.Properties["Путь_к_файлу"]?.ToString();
                        if (string.IsNullOrWhiteSpace(filePath))
                        {
                            MessageBox.Show("Путь к файлу не указан!");
                            return;
                        }

                        if (!System.IO.File.Exists(filePath))
                        {
                            MessageBox.Show("Файл не найден по указанному пути!");
                            return;
                        }

                        try
                        {
                            string argument = $"/select, \"{filePath}\"";
                            System.Diagnostics.Process.Start("explorer.exe", argument);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при открытии проводника: {ex.Message}");
                        }
                    }
                    else
                    {
                        MessageBox.Show("У выбранного узла отсутствует свойство 'Путь_к_файлу'!");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обработке узла: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Выберите узел для перехода!");
            }
        }



        private async void ReplaceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NodesDataGrid.SelectedItem is NodeData selectedNode)
            {
                try
                {
                    string label = _currentViewType == "Type"
                        ? NodeTypeComboBox.SelectedItem as string
                        : selectedNode.Properties["Label"].ToString();
                    var nodeType = _nodeTypeCollection.NodeTypes
                        .FirstOrDefault(nt => nt.Label.ContainsKey(label));
                    if (nodeType == null)
                    {
                        MessageBox.Show($"Тип узла '{label}' не найден!");
                        return;
                    }
                    // Создаем копию свойств для редактирования
                    var propertiesCopy = new Dictionary<string, object>(
                        selectedNode.Properties
                            .Where(p => p.Key != "Id" && p.Key != "Label")
                            .ToDictionary(p => p.Key, p => p.Value));
                    // Открываем окно редактирования
                    var editWindow = new EditNodeWindow(propertiesCopy, nodeType.Properties);
                    editWindow.Owner = this;
                    if (editWindow.ShowDialog() == true)
                    {
                        // Выполняем замену
                        string typeLabel = nodeType.Label.Values.First();
                        var newNode = await BDController.ReplaceNode(
                            _client,
                            selectedNode,
                            editWindow.Properties,
                            nodeType.Properties,
                            typeLabel);
                        // Обновляем интерфейс
                        if (_currentViewType == "Type")
                        {
                            var nodes = await BDController.LoadNodesByType(_client, typeLabel);
                            _currentNodes = nodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки
                            ApplyCustomFilter();
                            await RefreshCurrentView();
                        }
                        else if (_currentViewType == "Related")
                        {
                            var relatedNodes = await BDController.LoadRelatedNodes(_client, _currentContextNode);
                            _currentNodes = relatedNodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки для Related
                            ApplyCustomFilter();
                            await RefreshCurrentView();
                        }
                        MessageBox.Show("Узел успешно заменен!");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при замене узла: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Выберите узел для замены!");
            }
        }


        private async void AddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (NodesDataGrid.SelectedItem is NodeData selectedNode)
            {
                try
                {
                    // Шаг 1: Открываем окно для создания нового связанного узла
                    var addLinkedNodeWindow = new AddLinkedNodeWindow(_nodeTypeCollection);
                    addLinkedNodeWindow.Owner = this;
                    if (addLinkedNodeWindow.ShowDialog() == true)
                    {
                        // Получаем данные из окна
                        string selectedTypeKey = addLinkedNodeWindow.SelectedType;
                        var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedTypeKey));
                        if (selectedNodeType == null)
                        {
                            MessageBox.Show("Выбранный тип не найден!");
                            return;
                        }
                        string label = selectedNodeType.Label.Values.First();
                        Dictionary<string, object> newProperties = addLinkedNodeWindow.Properties;
                        string relationshipType = addLinkedNodeWindow.RelationshipType;
                        // Шаг 2: Создаём новый узел
                        var newNode = await BDController.AddNodeToDb(_client, label, newProperties, selectedNodeType.Properties);
                        // Шаг 3: Создаём связь между выбранным узлом и новым
                        await BDController.CreateRelationship(_client, selectedNode, newNode, relationshipType);
                        // Шаг 4: Обновляем текущий список в зависимости от режима просмотра
                        if (_currentViewType == "Type")
                        {
                            var nodes = await BDController.LoadNodesByType(_client, label);
                            _currentNodes = nodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки
                            ApplyCustomFilter();
                            await RefreshCurrentView();
                        }
                        else if (_currentViewType == "Related")
                        {
                            var relatedNodes = await BDController.LoadRelatedNodes(_client, _currentContextNode);
                            _currentNodes = relatedNodes;
                            _nodesViewSource.Source = _currentNodes;
                            NodesDataGrid.ItemsSource = _nodesViewSource.View;
                            GenerateDataGridColumns(); // Генерируем колонки для Related
                            ApplyCustomFilter();
                            await RefreshCurrentView();
                        }
                        MessageBox.Show("Новый объект успешно добавлен и связан!");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении связанного объекта: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Выберите узел, к которому добавить новый объект!");
            }
        }
        private void ApplyCustomFilter()
        {
            if (_currentNodes == null) return;
            var filteredNodes = _currentNodes; // Начинаем с текущего списка (после динамических фильтров)
            if (_customFilters.TryGetValue(_selectedCustomFilter, out var filter) && filter != null)
            {
                filteredNodes = filter.Apply(filteredNodes);
            }
            _nodesViewSource.Source = filteredNodes;
            NodesDataGrid.ItemsSource = _nodesViewSource.View;
            GenerateDataGridColumns(); // Генерируем колонки (на случай изменений в фильтре)
        }
        // Метод для генерации колонок
        // Метод для генерации колонок
        private void GenerateDataGridColumns()
        {
            NodesDataGrid.Columns.Clear(); // Очищаем предыдущие колонки

            if (_currentViewType == "Type")
            {
                // Для режима "Type": используем свойства типа с правильными типами колонок
                var selectedType = NodeTypeComboBox.SelectedItem as string;
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType == null) return;

                var properties = selectedNodeType.Properties;
                foreach (var prop in properties.Where(p => p.Key != "Id" && p.Key != "Label"))
                {
                    // Преобразуем ключ в читаемый заголовок: '_' -> ' '
                    string displayHeader = prop.Key.Replace("_", " ");

                    DataGridColumn column;
                    if (prop.Value == typeof(bool))
                    {
                        column = new DataGridCheckBoxColumn
                        {
                            Header = displayHeader,
                            Binding = new Binding($"Properties[{prop.Key}]")
                        };
                    }
                    else
                    {
                        column = new DataGridTextColumn
                        {
                            Header = displayHeader,
                            Binding = new Binding($"Properties[{prop.Key}]")
                        };
                        if (prop.Value == typeof(Neo4j.Driver.LocalDate) || prop.Value == typeof(DateTime))
                        {
                            ((DataGridTextColumn)column).Binding.StringFormat = "yyyy-MM-dd"; // Формат даты
                        }
                    }
                    NodesDataGrid.Columns.Add(column);
                }
            }
            else if (_currentViewType == "Related")
            {
                // Для режима "Related": вычисляем объединение всех свойств, все колонки как текст (из-за смешанных типов)
                // Добавляем колонку "Тип" (Label) первой
                var labelColumn = new DataGridTextColumn
                {
                    Header = "Тип",
                    Binding = new Binding("Properties[Label]")
                };
                NodesDataGrid.Columns.Add(labelColumn);

                // Объединение ключей свойств
                var allProps = _currentNodes
                    .SelectMany(n => n.Properties.Keys)
                    .Distinct()
                    .Where(k => k != "Id" && k != "Label")
                    .OrderBy(k => k) // Для стабильного порядка
                    .ToList();

                foreach (var prop in allProps)
                {
                    // Преобразуем ключ в читаемый заголовок: '_' -> ' '
                    string displayHeader = prop.Replace("_", " ");

                    var column = new DataGridTextColumn
                    {
                        Header = displayHeader,
                        Binding = new Binding($"Properties[{prop}]") { Mode = BindingMode.OneWay } // OneWay, чтобы не падать на отсутствующие свойства
                    };
                    // Специальный формат для известных полей, как "Дата"
                    if (prop == "Дата")
                    {
                        column.Binding.StringFormat = "yyyy-MM-dd";
                    }
                    NodesDataGrid.Columns.Add(column);
                }
            }
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await RefreshCurrentView();
        }

        private async Task RefreshCurrentView()
        {
            if (_currentViewType == "Type" && NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    // Собираем текущие фильтры (как в FilterControl_Changed)
                    var filters = new Dictionary<string, object>();
                    if (_filterControls != null)
                    {
                        foreach (var kvp in _filterControls)
                        {
                            if (selectedNodeType.Properties.TryGetValue(kvp.Key, out var propType))
                            {
                                if (propType == typeof(bool))
                                {
                                    var trueCheck = (kvp.Value[0] as CheckBox)?.IsChecked ?? false;
                                    var falseCheck = (kvp.Value[1] as CheckBox)?.IsChecked ?? false;
                                    if (trueCheck && !falseCheck)
                                        filters[kvp.Key] = true;
                                    else if (!trueCheck && falseCheck)
                                        filters[kvp.Key] = false;
                                }
                                else if (propType == typeof(DateTime) || propType == typeof(Neo4j.Driver.LocalDate))
                                {
                                    var fromDate = (kvp.Value[0] as DatePicker)?.SelectedDate;
                                    var toDate = (kvp.Value[1] as DatePicker)?.SelectedDate;
                                    if (fromDate.HasValue || toDate.HasValue)
                                        filters[kvp.Key] = new { From = fromDate, To = toDate };
                                }
                                else
                                {
                                    var text = (kvp.Value[0] as TextBox)?.Text;
                                    if (!string.IsNullOrWhiteSpace(text))
                                        filters[kvp.Key] = text;
                                }
                            }
                        }
                    }
                    string label = selectedNodeType.Label.Values.First();
                    var nodes = await BDController.LoadFilteredNodes(_client, label, filters);
                    _currentNodes = nodes;
                    _nodesViewSource.Source = _currentNodes;
                    NodesDataGrid.ItemsSource = _nodesViewSource.View;
                    GenerateDataGridColumns();
                    ApplyCustomFilter();  // Применяем кастомные фильтры поверх
                }
            }
            else if (_currentViewType == "Related" && _currentContextNode != null)
            {
                var relatedNodes = await BDController.LoadRelatedNodes(_client, _currentContextNode);
                _currentNodes = relatedNodes;
                _nodesViewSource.Source = _currentNodes;
                NodesDataGrid.ItemsSource = _nodesViewSource.View;
                GenerateDataGridColumns();
                ApplyCustomFilter();  // Только кастомные фильтры для Related
            }
        }

        // Добавьте этот метод для кнопки "Обновить", если добавили кнопку в XAML
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshCurrentView();
        }
        private void CustomFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomFilterComboBox.SelectedItem is string selected)
            {
                _selectedCustomFilter = selected;
                ApplyCustomFilter();
            }
        }
        private async void BulkAddButton_Click(object sender, RoutedEventArgs e)
        {
            var bulkWindow = new BulkAddWindow(_client, _nodeTypeCollection);
            bulkWindow.Owner = this;
            if (bulkWindow.ShowDialog() == true)
            {
                await RefreshCurrentView();
            }
        }
    }
}
