using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoClientVis
{
    public class NodeTypeCollection
    {
        public int CreatedCount 
        {
            get
            {
                return NodeTypes.Count;
            }
            set { }
        }
        public List<NodeType> NodeTypes { get; set; }

        public NodeTypeCollection()
        {
            CreatedCount = 0;
            NodeTypes = new List<NodeType>();
        }

        // Метод для добавления нового типа
        public void AddNodeType(string labelKey, List<string>? properties = null)
        {
            var newNodeType = new NodeType(labelKey, CreatedCount + 1, properties);
            NodeTypes.Add(newNodeType);
        }
    }
}