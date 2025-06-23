namespace NeoClientVis
{
    // Класс для представления данных узла
    public class NodeData
    {
        public Dictionary<string, object> Properties { get; set; }
        public string DisplayString { get; set; }

        // Добавляем для удобства
        public long Id => Properties.ContainsKey("Id") ? (long)Properties["Id"] : -1;
    }
}