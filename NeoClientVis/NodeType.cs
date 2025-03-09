namespace NeoClientVis
{

    public class NodeType
    {
        public Dictionary<string, string> Label { get; set; }
        public Dictionary<string, Type> Properties { get; set; } // Теперь храним типы данных

        public NodeType()
        {
            Label = new Dictionary<string, string>();
            Properties = new Dictionary<string, Type>();
        }

        public NodeType(string labelKey, int count, Dictionary<string, Type> properties)
        {
            Label = new Dictionary<string, string> { { labelKey, $"Label_{count}" } };
            Properties = properties;
        }
    }
}