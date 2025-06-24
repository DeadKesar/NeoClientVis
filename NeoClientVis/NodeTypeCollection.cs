using Newtonsoft.Json;

namespace NeoClientVis
{
    [JsonObject(MemberSerialization.OptIn)]
    public class NodeTypeCollection
    {
        [JsonProperty]
        public int CreatedCount { get; set; }

        [JsonProperty]
        public List<NodeType> NodeTypes { get; set; } = new List<NodeType>();

        // Добавим конструктор для корректной десериализации
        [JsonConstructor]
        public NodeTypeCollection(
            int CreatedCount,
            List<NodeType> NodeTypes)
        {
            this.CreatedCount = CreatedCount;
            this.NodeTypes = NodeTypes ?? new List<NodeType>();
        }

        public NodeTypeCollection()
        {
            CreatedCount = 0;
        }

        public void AddNodeType(string labelKey)
        {
            // Генерируем уникальную метку
            string labelValue = $"Label_{CreatedCount + 1}";

            var defaultProperties = new Dictionary<string, Type>
                {
                    { "Актуальность", typeof(bool) },
                    { "Имя", typeof(string) },
                    { "Дата", typeof(Neo4j.Driver.LocalDate) },
                    { "Путь_к_файлу", typeof(string) }
                };

            var newNodeType = new NodeType
            {
                Label = new Dictionary<string, string> { { labelKey, labelValue } },
                Properties = defaultProperties
            };

            NodeTypes.Add(newNodeType);
            CreatedCount++;
        }

        public void RecalculateCount()
        {
            int maxCount = 0;
            foreach (var nodeType in NodeTypes)
            {
                if (nodeType.Label.Values.FirstOrDefault() is string labelValue)
                {
                    if (labelValue.StartsWith("Label_") &&
                        int.TryParse(labelValue.Substring(6), out int count))
                    {
                        if (count > maxCount) maxCount = count;
                    }
                }
            }
            CreatedCount = maxCount;
        }
    }
}