using System;
using System.Collections.Generic;
using Neo4j.Driver;

namespace NeoClientVis
{
    public class ExpiredDocumentsFilter : IFilter
    {
        public List<NodeData> Apply(List<NodeData> nodes)
        {
            var today = DateTime.Today; // DateTime.Parse("2025-07-28")
            var filtered = new List<NodeData>();

            foreach (var node in nodes)
            {
                if (node.Properties.TryGetValue("Дата", out var dateObj))
                {
                    DateTime? nodeDate = null;

                    if (dateObj is LocalDate localDate)
                    {
                        nodeDate = new DateTime(localDate.Year, localDate.Month, localDate.Day);
                    }
                    else if (dateObj is DateTime dt)
                    {
                        nodeDate = dt;
                    }
                    else if (dateObj is string dateStr && DateTime.TryParse(dateStr, out var parsedDate))
                    {
                        nodeDate = parsedDate;
                    }

                    if (nodeDate.HasValue && nodeDate.Value < today && node.Properties["Актуальность"].ToString().ToLower() == "true")
                    {
                        filtered.Add(node);
                    }
                }
            }

            return filtered;
        }
    }
}