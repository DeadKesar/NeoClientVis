namespace NeoClientVis
{
    public class NodeType
    {
        public Dictionary<string, string> Label { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public NodeType()
        {
            Label = new Dictionary<string, string>();
            Properties = new Dictionary<string, string>();
        }

        public NodeType(string labelKey, int count, List<string>? properties = null)
        {
            Label = new Dictionary<string, string> { { labelKey, $"Label_{count}" } };
            Properties = new Dictionary<string, string>();
            properties ??= new List<string>();
            for (int i = 0; i < properties.Count; i++)
            {
                Properties[properties[i]] = $"prop_{i}";
            }
        }
    }
}