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

        public MainWindow()
        {
            InitializeComponent();
            InitializeNeo4jClient();
            LoadDataAsync();
        }
        private void InitializeNeo4jClient()
        {
            _client = new GraphClient(new Uri("http://localhost:7474"), "neo4j", "12345678a");
            _client.ConnectAsync().Wait(); // Синхронное подключение для простоты
        }
        private async void LoadDataAsync()
        {
            try
            {
                await _client.ConnectAsync();
                _nodeTypeCollection = await BDController.LoadNodeTypesFromDb(_client);

                NodeTypeComboBox.ItemsSource = _nodeTypeCollection.NodeTypes.Select(nt => nt.Label.First().Key);
                if (_nodeTypeCollection.NodeTypes.Count > 0)
                {
                    NodeTypeComboBox.SelectedIndex = 0;
                }
                else
                {
                    NodesListBox.ItemsSource = null;
                    PropertiesListBox.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
        }

        private async void NodeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    var nodes = await BDController.LoadNodesByType(_client, selectedNodeType.Label.Values.First());
                    NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}");
                    PropertiesListBox.ItemsSource = selectedNodeType.Properties.Keys;
                }
            }
        }



        private async void AddPropertyButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    string newProperty = Microsoft.VisualBasic.Interaction.InputBox(
                        "Введите название нового свойства:",
                        "Добавление свойства",
                        "",
                        -1, -1);

                    if (!string.IsNullOrWhiteSpace(newProperty) && !selectedNodeType.Properties.ContainsKey(newProperty))
                    {
                        int propCount = selectedNodeType.Properties.Count;
                        selectedNodeType.Properties[newProperty] = $"prop_{propCount}";
                        string label = selectedNodeType.Label.Values.First();
                        await BDController.UpdateNodesWithNewProperty(_client, label, newProperty);
                        await BDController.SaveNodeTypesToDb(_client, _nodeTypeCollection);

                        PropertiesListBox.ItemsSource = null;
                        PropertiesListBox.ItemsSource = selectedNodeType.Properties.Keys;

                        var nodes = await BDController.LoadNodesByType(_client, label);
                        NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}");
                    }
                    else if (selectedNodeType.Properties.ContainsKey(newProperty))
                    {
                        MessageBox.Show("Свойство с таким названием уже существует!");
                    }
                }
            }
        }
        private async void AddNodeTypeButton_Click(object sender, RoutedEventArgs e)
        {
            string newTypeName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите название нового типа:",
                "Добавление типа узла",
                "",
                -1, -1);

            if (!string.IsNullOrWhiteSpace(newTypeName) && !_nodeTypeCollection.NodeTypes.Any(nt => nt.Label.ContainsKey(newTypeName)))
            {
                _nodeTypeCollection.AddNodeType(newTypeName);
                await BDController.SaveNodeTypesToDb(_client, _nodeTypeCollection);

                NodeTypeComboBox.ItemsSource = null;
                NodeTypeComboBox.ItemsSource = _nodeTypeCollection.NodeTypes.Select(nt => nt.Label.First().Key);
                NodeTypeComboBox.SelectedItem = newTypeName;
            }
            else if (_nodeTypeCollection.NodeTypes.Any(nt => nt.Label.ContainsKey(newTypeName)))
            {
                MessageBox.Show("Тип с таким названием уже существует!");
            }
        }

        private async void AddNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    // Открываем окно для ввода свойств
                    var addNodeWindow = new AddNodeWindow(selectedNodeType.Properties);
                    addNodeWindow.Owner = this;
                    if (addNodeWindow.ShowDialog() == true)
                    {
                        string label = selectedNodeType.Label.Values.First();
                        await BDController.AddNodeToDb(_client, label, addNodeWindow.Properties);

                        // Обновляем список узлов
                        var nodes = await BDController.LoadNodesByType(_client, label);
                        NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Сначала выберите тип узла!");
            }
        }
        // Обработчики контекстного меню
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
                        var editNodeWindow = new EditNodeWindow(selectedNode.Properties);
                        editNodeWindow.Owner = this;
                        if (editNodeWindow.ShowDialog() == true)
                        {
                            await BDController.UpdateNodeProperties(_client, label, selectedNode.Properties, editNodeWindow.Properties);

                            // Обновляем список узлов
                            nodes = await BDController.LoadNodesByType(_client, label);
                            NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}");
                        }
                    }
                }
            }
        }

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
                    var selectedNode = nodes.FirstOrDefault(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}" == selectedNodeString);

                    if (selectedNode != null)
                    {
                        // Подтверждение удаления
                        var result = MessageBox.Show($"Вы уверены, что хотите удалить узел '{selectedNodeString}'?",
                            "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (result == MessageBoxResult.Yes)
                        {
                            await BDController.DeleteNode(_client, label, selectedNode.Properties);

                            // Обновляем список узлов
                            nodes = await BDController.LoadNodesByType(_client, label);
                            NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}");
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите узел для удаления!");
            }
        }

        // Заглушки для остальных пунктов меню
        private void GoToMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Перейти' пока не реализована."); }
        private void RelatedMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Связанные' пока не реализована."); }
        private void AddMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Добавить' пока не реализована."); }
        private void ReplaceMenuItem_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Функция 'Заменить' пока не реализована."); }





    }



}
