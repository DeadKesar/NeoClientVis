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
        private List<NodeType> _nodeTypes;

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
            _nodeTypes = await LoadNodeTypesFromDb(_client);
            if (_nodeTypes.Count == 0) // Если база пуста, создаём пример данных
            {
                _nodeTypes = new List<NodeType>();
                await SaveNodeTypesToDb(_client, _nodeTypes);
            }

            // Заполняем выпадающий список типами
            NodeTypeComboBox.ItemsSource = _nodeTypes.Select(nt => nt.Label.First().Key);
            NodeTypeComboBox.SelectedIndex = 0;
        }

        private async void NodeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                // Находим выбранный тип
                var selectedNodeType = _nodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    // Загружаем узлы этого типа
                    var nodes = await LoadNodesByType(_client, selectedNodeType.Label.Values.First());
                    NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}");

                    // Отображаем свойства типа
                    PropertiesListBox.ItemsSource = selectedNodeType.Properties.Keys;
                }
            }
        }

        // Метод загрузки узлов по типу
        private async Task<List<NodeData>> LoadNodesByType(GraphClient client, string label)
        {
            var result = await client.Cypher
                .Match($"(n:{label})")
                .Return(n => new NodeData
                {
                    Properties = n.As<Dictionary<string, object>>()
                })
                .ResultsAsync;

            return result.ToList();
        }

        private async void AddPropertyButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem is string selectedType)
            {
                var selectedNodeType = _nodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedType));
                if (selectedNodeType != null)
                {
                    // Запрашиваем название нового свойства
                    string newProperty = Microsoft.VisualBasic.Interaction.InputBox(
                        "Введите название нового свойства:",
                        "Добавление свойства",
                        "",
                        -1, -1);

                    if (!string.IsNullOrWhiteSpace(newProperty) && !selectedNodeType.Properties.ContainsKey(newProperty))
                    {
                        // Добавляем новое свойство в текущий тип (значение по умолчанию - "prop_n")
                        int propCount = selectedNodeType.Properties.Count;
                        selectedNodeType.Properties[newProperty] = $"prop_{propCount}";

                        // Обновляем все узлы этого типа в Neo4j
                        string label = selectedNodeType.Label.Values.First();
                        await UpdateNodesWithNewProperty(_client, label, newProperty);

                        // Сохраняем обновлённый список типов в базу
                        await SaveNodeTypesToDb(_client, _nodeTypes);

                        // Обновляем интерфейс
                        PropertiesListBox.ItemsSource = null; // Сбрасываем, чтобы обновить
                        PropertiesListBox.ItemsSource = selectedNodeType.Properties.Keys;

                        // Перезагружаем список узлов
                        var nodes = await LoadNodesByType(_client, label);
                        NodesListBox.ItemsSource = nodes.Select(n => $"Node: {string.Join(", ", n.Properties.Select(p => $"{p.Key}: {p.Value}"))}");
                    }
                    else if (selectedNodeType.Properties.ContainsKey(newProperty))
                    {
                        MessageBox.Show("Свойство с таким названием уже существует!");
                    }
                }
            }
        }
        private async Task UpdateNodesWithNewProperty(GraphClient client, string label, string newProperty)
        {
            await client.Cypher
                .Match($"(n:{label})")
                .Set($"n.{newProperty} = ''") // Добавляем новое свойство с пустым значением
                .ExecuteWithoutResultsAsync();
        }


        // Метод загрузки типов узлов из базы
        private async Task<List<NodeType>> LoadNodeTypesFromDb(GraphClient client)
        {
            var result = await client.Cypher
                .Match("(n:NodeTypes)")
                .Return(n => n.As<Dictionary<string, object>>())
                .ResultsAsync;

            var json = result.FirstOrDefault()?["data"].ToString();
            if (json != null)
            {
                try
                {
                    return JsonConvert.DeserializeObject<List<NodeType>>(json);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка десериализации: {ex.Message}");
                    return new List<NodeType>();
                }
            }
            return new List<NodeType>();
        }

        // Метод сохранения типов узлов в базу
        private async Task SaveNodeTypesToDb(GraphClient client, List<NodeType> nodeTypes)
        {
            var json = JsonConvert.SerializeObject(nodeTypes, Formatting.Indented);
            await client.Cypher
                .Merge("(n:NodeTypes)")
                .OnCreate()
                .Set("n.data = $json")
                .OnMatch()
                .Set("n.data = $json")
                .WithParam("json", json)
                .ExecuteWithoutResultsAsync();
        }
    }

    // Класс для представления данных узла
    public class NodeData
    {
        public Dictionary<string, object> Properties { get; set; }
    }

    // Класс NodeType из вашего кода
    public class NodeType
    {
        public static int createdCount = 0;
        public Dictionary<string, string> Label { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public NodeType()
        {
            Label = new Dictionary<string, string>();
            Properties = new Dictionary<string, string>();
        }

        public NodeType(string labelKey, List<string> properties)
        {
            createdCount++;
            Label = new Dictionary<string, string> { { labelKey, $"Label_{createdCount}" } };
            Properties = new Dictionary<string, string>();
            for (int i = 0; i < properties.Count; i++)
            {
                Properties[properties[i]] = $"prop_{i}";
            }
        }
    }
}