using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeoClientVis
{
    [JsonObject(MemberSerialization.OptIn)]
    public class NodeType
    {
        [JsonProperty]
        public Dictionary<string, string> Label { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public Dictionary<string, Type> Properties { get; set; } = new Dictionary<string, Type>();

        // Свойство для сериализации типов
        [JsonProperty("Properties")]
        public Dictionary<string, string> PropertiesSerialized
        {
            get => Properties.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.FullName
            );
            set => Properties = value.ToDictionary(
                kvp => kvp.Key,
                kvp => Type.GetType(kvp.Value) ?? typeof(string)
            );
        }

        // Конструктор для десериализации
        [JsonConstructor]
        public NodeType(
            Dictionary<string, string> Label,
            Dictionary<string, string> PropertiesSerialized)
        {
            this.Label = Label ?? new Dictionary<string, string>();
            this.PropertiesSerialized = PropertiesSerialized ?? new Dictionary<string, string>();
        }

        // Конструктор без параметров для создания в коде
        public NodeType() { }
    }
}