using System;
using System.Collections.Generic;
using Neo4j.Driver;

namespace NeoClientVis
{
    public class ExpiredDocumentsFilter : IFilter
    {
        public List<NodeData> Apply(List<NodeData> nodes)
        {
            var today = DateTime.Today;
            var filtered = new List<NodeData>();
            foreach (var node in nodes)
            {
                if (node.Properties.TryGetValue("Дата", out var dateObj) && dateObj is DateTime nodeDate &&
                    nodeDate < today && (bool)node.Properties["Актуальность"])
                {
                    filtered.Add(node);
                }
            }
            return filtered;
        }
    }
}