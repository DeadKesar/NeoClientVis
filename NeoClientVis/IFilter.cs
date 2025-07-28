using System.Collections.Generic;

namespace NeoClientVis
{
    public interface IFilter
    {
        List<NodeData> Apply(List<NodeData> nodes);
    }
}