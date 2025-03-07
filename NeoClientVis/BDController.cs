﻿using Neo4jClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NeoClientVis
{
    internal static class BDController
    {


        // Метод загрузки узлов по типу
        public static async Task<List<NodeData>> LoadNodesByType(GraphClient client, string label)
        {
            var result = await client.Cypher
                .Match($"(n:{label})")
                .Return(n => new NodeData
                {
                    Properties = n.As<Dictionary<string, object>>()
                })
                .ResultsAsync;

            return result.ToList();
        }
        public static async Task UpdateNodesWithNewProperty(GraphClient client, string label, string newProperty)
        {
            await client.Cypher
                .Match($"(n:{label})")
                .Set($"n.{newProperty} = ''") // Добавляем новое свойство с пустым значением
                .ExecuteWithoutResultsAsync();
        }
        // Метод загрузки типов узлов из базы
        public static async Task<NodeTypeCollection> LoadNodeTypesFromDb(GraphClient client)
        {
            var result = await client.Cypher
                .Match("(n:NodeTypes)")
                .Return(n => n.As<Dictionary<string, object>>())
                .ResultsAsync;

            var json = result.FirstOrDefault()?["data"].ToString();
            if (json != null)
            {
                return JsonConvert.DeserializeObject<NodeTypeCollection>(json);
            }
            return new NodeTypeCollection(); // Пустая коллекция при отсутствии данных
        }


        // Метод сохранения типов узлов в базу
        public static async Task SaveNodeTypesToDb(GraphClient client, NodeTypeCollection nodeTypes)
        {
            var json = JsonConvert.SerializeObject(nodeTypes, Formatting.Indented);
            await client.Cypher
                .Merge("(n:NodeTypes)")
                .OnCreate()
                .Set("n.data = $json")
                .OnMatch()
                .Set("n.data = $json")
                .WithParam("json", json)
                .ExecuteWithoutResultsAsync();
        }

        public static async Task AddNodeToDb(GraphClient client, string label, Dictionary<string, string> properties)
        {
            var propertiesString = string.Join(", ", properties.Select(p => $"{p.Key}: ${p.Key}"));
            await client.Cypher
                .Create($"(n:{label} {{ {propertiesString} }})")
                .WithParams(properties.ToDictionary(p => p.Key, p => (object)p.Value))
                .ExecuteWithoutResultsAsync();
        }
        public static async Task UpdateNodeProperties(GraphClient client, string label, Dictionary<string, object> oldProperties, Dictionary<string, string> newProperties)
        {
            // Формируем условие для поиска узла по старым свойствам
            var matchProperties = string.Join(" AND ", oldProperties.Select(p => $"n.{p.Key} = ${p.Key}"));
            var setProperties = string.Join(", ", newProperties.Select(p => $"n.{p.Key} = ${p.Key}_new"));

            await client.Cypher
                .Match($"(n:{label})")
                .Where(matchProperties)
                .Set(setProperties)
                .WithParams(oldProperties.ToDictionary(p => p.Key, p => p.Value)
                    .Concat(newProperties.ToDictionary(p => $"{p.Key}_new", p => (object)p.Value))
                    .ToDictionary(p => p.Key, p => p.Value))
                .ExecuteWithoutResultsAsync();
        }
        public static async Task DeleteNode(GraphClient client, string label, Dictionary<string, object> properties)
        {
            var matchProperties = string.Join(" AND ", properties.Select(p => $"n.{p.Key} = ${p.Key}"));
            await client.Cypher
                .Match($"(n:{label})")
                .Where(matchProperties)
                .Delete("n")
                .WithParams(properties.ToDictionary(p => p.Key, p => p.Value))
                .ExecuteWithoutResultsAsync();
        }


    }
}
