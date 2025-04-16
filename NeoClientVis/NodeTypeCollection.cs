using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoClientVis
{
    public class NodeTypeCollection
    {
        public int CreatedCount { get; private set; }
        public List<NodeType> NodeTypes { get; set; }

        public NodeTypeCollection()
        {
            CreatedCount = 0;
            NodeTypes = new List<NodeType>();
        }

        public void AddNodeType(string labelKey, List<string>? properties = null)
        {
            CreatedCount++;
            var defaultProperties = new Dictionary<string, Type>
            {
                { "Актуальность", typeof(bool) },
                { "Имя", typeof(string) },
                { "Дата", typeof(Neo4j.Driver.LocalDate) },
                { "Путь_к_файлу", typeof(string) }
            };
            var newNodeType = new NodeType(labelKey, CreatedCount, defaultProperties);
            NodeTypes.Add(newNodeType);
        }
    }
}