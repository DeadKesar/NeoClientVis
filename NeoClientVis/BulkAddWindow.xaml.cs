using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.ComponentModel;
using Neo4jClient;
using Neo4j.Driver;
using System.Threading.Tasks;
using Microsoft.Win32;  // Для OpenFileDialog

namespace NeoClientVis
{
    public partial class BulkAddWindow : Window
    {
        private readonly GraphClient _client;
        private readonly NodeTypeCollection _nodeTypeCollection;
        private List<BulkFileItem> _files = new List<BulkFileItem>();
        private string _selectedFolder;

        public BulkAddWindow(GraphClient client, NodeTypeCollection nodeTypeCollection)
        {
            InitializeComponent();
            _client = client;
            _nodeTypeCollection = nodeTypeCollection;

            // Заполняем ComboBox типами узлов
            NodeTypeComboBox.ItemsSource = _nodeTypeCollection.NodeTypes.Select(nt => nt.Label.First().Key);
            if (NodeTypeComboBox.Items.Count > 0)
                NodeTypeComboBox.SelectedIndex = 0;

            // Устанавливаем общую дату по умолчанию
            CommonDatePicker.SelectedDate = DateTime.Today;

            FilesDataGrid.ItemsSource = _files;
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите папку с файлами",
                FileName = "Выберите эту папку",  // Текст, который увидит пользователь
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFolder = Path.GetDirectoryName(dialog.FileName);
                SelectedFolderText.Text = _selectedFolder ?? "Папка не выбрана";

                LoadFilesFromFolder();
            }
        }

        private void LoadFilesFromFolder()
        {
            if (string.IsNullOrEmpty(_selectedFolder)) return;

            try
            {
                var filePaths = Directory.GetFiles(_selectedFolder, "*.*", SearchOption.TopDirectoryOnly);
                _files.Clear();

                foreach (var filePath in filePaths)
                {
                    _files.Add(new BulkFileItem
                    {
                        Add = true,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        PathToFile = filePath,
                        Date = CommonDatePicker.SelectedDate ?? DateTime.Today,
                        Actual = true
                    });
                }

                FilesDataGrid.ItemsSource = null;
                FilesDataGrid.ItemsSource = _files;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файлов: {ex.Message}");
            }
        }

        private void ApplyDateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CommonDatePicker.SelectedDate.HasValue) return;

            foreach (var item in _files)
            {
                item.Date = CommonDatePicker.SelectedDate.Value;
            }

            FilesDataGrid.Items.Refresh();
        }

        private async void AddSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите тип узла!");
                return;
            }

            string selectedTypeKey = NodeTypeComboBox.SelectedItem.ToString();
            var selectedNodeType = _nodeTypeCollection.NodeTypes.FirstOrDefault(nt => nt.Label.ContainsKey(selectedTypeKey));
            if (selectedNodeType == null)
            {
                MessageBox.Show("Тип узла не найден!");
                return;
            }

            string label = selectedNodeType.Label.Values.First();
            var propertyTypes = selectedNodeType.Properties;

            int addedCount = 0;
            foreach (var item in _files.Where(f => f.Add))
            {
                try
                {
                    var properties = new Dictionary<string, object>
                    {
                        { "Имя", item.Name },
                        { "Путь_к_файлу", item.PathToFile },
                        { "Дата", new LocalDate(item.Date.Year, item.Date.Month, item.Date.Day) },
                        { "Актуальность", item.Actual }
                    };

                    // Добавляем другие свойства типа с дефолтными значениями, если есть
                    foreach (var prop in propertyTypes.Where(p => !properties.ContainsKey(p.Key)))
                    {
                        object defaultValue = prop.Value == typeof(bool) ? false :
                                              prop.Value == typeof(LocalDate) ? new LocalDate(1, 1, 1) :
                                              "";
                        properties[prop.Key] = defaultValue;
                    }

                    await BDController.AddNodeToDb(_client, label, properties, propertyTypes);
                    addedCount++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении файла {item.PathToFile}: {ex.Message}");
                }
            }

            MessageBox.Show($"Успешно добавлено {addedCount} объектов.");
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}