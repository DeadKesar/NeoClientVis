using Neo4jClient;
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

        public static async Task AddNodeToDb(GraphClient client, string label, Dictionary<string, object> properties, Dictionary<string, Type> propertyTypes)
        {
            try
            {
                var validatedProperties = new Dictionary<string, object>();
                foreach (var prop in properties)
                {
                    if (propertyTypes[prop.Key] == typeof(Neo4j.Driver.LocalDate))
                    {
                        if (DateTime.TryParse(prop.Value?.ToString(), out var date))
                            validatedProperties[prop.Key] = new Neo4j.Driver.LocalDate(date.Year, date.Month, date.Day);
                        else
                            throw new ArgumentException($"Неверный формат даты для свойства '{prop.Key}': {prop.Value}");
                    }
                    else if (propertyTypes[prop.Key] == typeof(bool))
                    {
                        if (prop.Value is bool boolValue)
                            validatedProperties[prop.Key] = boolValue;
                        else if (bool.TryParse(prop.Value?.ToString(), out boolValue))
                            validatedProperties[prop.Key] = boolValue;
                        else
                            throw new ArgumentException($"Неверный формат булевого значения для свойства '{prop.Key}': {prop.Value}");
                    }
                    else
                    {
                        validatedProperties[prop.Key] = prop.Value?.ToString() ?? "";
                    }
                }

                var propertiesString = string.Join(", ", validatedProperties.Select(p => $"{p.Key}: ${p.Key}"));
                await client.Cypher
                    .Create($"(n:{label} {{ {propertiesString} }})")
                    .WithParams(validatedProperties)
                    .ExecuteWithoutResultsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении узла: {ex.Message}");
                throw;
            }
        }


        public static async Task UpdateNodeProperties(GraphClient client, string label, Dictionary<string, object> oldProperties, Dictionary<string, object> newProperties, Dictionary<string, Type> propertyTypes = null)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrEmpty(label)) throw new ArgumentException("Метка узла не может быть пустой.", nameof(label));
            if (oldProperties == null || !oldProperties.Any()) throw new ArgumentException("Старые свойства не могут быть пустыми.", nameof(oldProperties));
            if (newProperties == null || !newProperties.Any()) throw new ArgumentException("Новые свойства не могут быть пустыми.", nameof(newProperties));

            try
            {
                var matchProperties = string.Join(" AND ", oldProperties.Select(p => $"n.{p.Key} = ${p.Key}"));
                var setProperties = string.Join(", ", newProperties.Select(p => $"n.{p.Key} = ${p.Key}_new"));

                var parameters = oldProperties.ToDictionary(p => p.Key, p => p.Value);

                foreach (var prop in newProperties)
                {
                    if (propertyTypes != null && propertyTypes.TryGetValue(prop.Key, out var type) && type == typeof(Neo4j.Driver.LocalDate) && prop.Value is string dateStr)
                    {
                        if (DateTime.TryParse(dateStr, out var date))
                        {
                            parameters[$"{prop.Key}_new"] = new Neo4j.Driver.LocalDate(date.Year, date.Month, date.Day);
                        }
                        else
                        {
                            throw new ArgumentException($"Неверный формат даты для свойства '{prop.Key}': {dateStr}");
                        }
                    }
                    else
                    {
                        parameters[$"{prop.Key}_new"] = prop.Value;
                    }
                }

                await client.Cypher
                    .Match($"(n:{label})")
                    .Where(matchProperties)
                    .Set(setProperties)
                    .WithParams(parameters)
                    .ExecuteWithoutResultsAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при обновлении свойств узла с меткой '{label}': {ex.Message}", ex);
            }
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
        public static async Task UpdateNodesWithNewProperty(GraphClient client, string label, string newProperty, object defaultValue)
        {
            await client.Cypher
                .Match($"(n:{label})")
                .Set($"n.{newProperty} = $value")
                .WithParam("value", defaultValue)
                .ExecuteWithoutResultsAsync();
        }
        public static async Task AddRelevanceToExistingNodes(GraphClient client, string label)
        {
            await client.Cypher
                .Match($"(n:{label})")
                .Where("NOT EXISTS(n.Актуальность)")
                .Set("n.Актуальность = true")
                .ExecuteWithoutResultsAsync();
        }
        public static async Task<List<NodeData>> LoadFilteredNodes(GraphClient client, string label, Dictionary<string, object> filters)
        {
            // Начало запроса
            var query = client.Cypher.Match($"(n:{label})");

            // Список условий и параметров
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();

            // Обработка фильтров
            foreach (var filter in filters)
            {
                if (filter.Key == "Актуальность" && filter.Value is bool boolValue)
                {
                    conditions.Add($"n.{filter.Key} = $boolValue");
                    parameters["boolValue"] = boolValue;
                }
                else if (filter.Key == "Дата" && filter.Value is object dateFilter)
                {
                    var from = dateFilter.GetType().GetProperty("From")?.GetValue(dateFilter) as DateTime?;
                    var to = dateFilter.GetType().GetProperty("To")?.GetValue(dateFilter) as DateTime?;

                    var dateConditions = new List<string>();
                    if (from.HasValue)
                    {
                        dateConditions.Add($"n.{filter.Key} >= $fromDate");
                        parameters["fromDate"] = new Neo4j.Driver.LocalDate(from.Value.Year, from.Value.Month, from.Value.Day);
                    }
                    if (to.HasValue)
                    {
                        dateConditions.Add($"n.{filter.Key} <= $toDate");
                        parameters["toDate"] = new Neo4j.Driver.LocalDate(to.Value.Year, to.Value.Month, to.Value.Day);
                    }

                    if (dateConditions.Any())
                    {
                        conditions.Add($"n.{filter.Key} IS NOT NULL AND " + string.Join(" AND ", dateConditions));
                    }
                }
            }

            // Объединение условий в одно WHERE
            if (conditions.Any())
            {
                var combinedCondition = string.Join(" AND ", conditions);
                query = query.Where(combinedCondition);
            }

            // Добавление параметров
            foreach (var param in parameters)
            {
                query = query.WithParam(param.Key, param.Value);
            }

            // Выполнение запроса
            var result = await query
                .Return(n => new NodeData { Properties = n.As<Dictionary<string, object>>() })
                .ResultsAsync;

            return result?.ToList() ?? new List<NodeData>();
        }

        public static async Task UpdateBoolProperties(GraphClient client, string label, string propertyName)
        {
            await client.Cypher
                .Match($"(n:{label})")
                .Where($"n.{propertyName} = 'True'")
                .Set($"n.{propertyName} = true")
                .ExecuteWithoutResultsAsync();

            await client.Cypher
                .Match($"(n:{label})")
                .Where($"n.{propertyName} = 'False'")
                .Set($"n.{propertyName} = false")
                .ExecuteWithoutResultsAsync();
        }
    }

}

