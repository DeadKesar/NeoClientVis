using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;



namespace NeoClientVis
{
    [JsonObject(MemberSerialization.OptIn)]
    public class NodeType
    {
        [JsonProperty]
        public Dictionary<string, string> Label { get; set; } = new Dictionary<string, string>();

        [JsonIgnore]
        public Dictionary<string, Type> Properties { get; set; } = new Dictionary<string, Type>();

        // Свойство для сериализации типов с полной квалификацией (assembly-qualified name)
        [JsonProperty("Properties")]
        public Dictionary<string, string> PropertiesSerialized
        {
            get => Properties.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.AssemblyQualifiedName 
            );
            set => Properties = value.ToDictionary(
                kvp => kvp.Key,
                kvp => Type.GetType(kvp.Value) ?? typeof(string)  
            );
        }

        // Конструкторы без изменений
        [JsonConstructor]
        public NodeType(
            Dictionary<string, string> Label,
            Dictionary<string, string> PropertiesSerialized)
        {
            this.Label = Label ?? new Dictionary<string, string>();
            this.PropertiesSerialized = PropertiesSerialized ?? new Dictionary<string, string>();
        }

        public NodeType() { }
    }

}