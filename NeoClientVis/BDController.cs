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


        /// <summary>
        /// Метод загрузки узлов по типу
        /// </summary>
        /// <param name="client">клиент базы данных</param>
        /// <param name="label">имя для вытаскивания из бд</param>
        /// <returns></returns>
        public static async Task<List<NodeData>> LoadNodesByType(GraphClient client, string label)
        {
            var query = client.Cypher
                .Match($"(n:{label})")
                .Return(n => new
                {
                    Properties = n.As<Dictionary<string, object>>(),
                    Id = n.Id() // Добавляем ID узла
                });

            var results = await query.ResultsAsync;
            return results.Select(item =>
            {
                item.Properties["Id"] = item.Id; // Сохраняем ID в свойства
                return new NodeData
                {
                    Properties = item.Properties,
                    DisplayString = GetNodeDisplayString(item.Properties)
                };
            }).ToList();
        }
        /// <summary>
        /// добавление пустых свойств к конкретному типу
        /// </summary>
        /// <param name="client">контролер базы данных</param>
        /// <param name="label">имя типа с которым работаем</param>
        /// <param name="newProperty">имя нового свойства</param>
        /// <returns></returns>
        public static async Task UpdateNodesWithNewProperty(GraphClient client, string label, string newProperty)
        {
            await client.Cypher
                .Match($"(n:{label})")
                .Set($"n.{newProperty} = ''") // Добавляем новое свойство с пустым значением
                .ExecuteWithoutResultsAsync();
        }
        /// <summary>
        /// Метод загрузки типов узлов из базы
        /// </summary>
        /// <param name="client">клиент базы данны</param>
        /// <returns></returns>

        public static async Task<NodeTypeCollection> LoadNodeTypesFromDb(GraphClient client)
        {
            try
            {
                var query = client.Cypher
                    .Match("(n:NodeTypeCollection)")
                    .Return(n => n.As<Dictionary<string, object>>());

                var result = (await query.ResultsAsync).FirstOrDefault();

                if (result != null && result.TryGetValue("Data", out object dataObj))
                {
                    string json = dataObj.ToString();
                    Console.WriteLine($"Загруженные данные NodeTypeCollection: {json}");  // Для отладки

                    var settings = new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto,
                        Error = (sender, args) =>
                        {
                            args.ErrorContext.Handled = true;
                            Console.WriteLine($"Ошибка десериализации: {args.ErrorContext.Error.Message}");
                        }
                    };

                    return JsonConvert.DeserializeObject<NodeTypeCollection>(json, settings);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке типов узлов: {ex.Message}");
                Console.WriteLine($"Ошибка в LoadNodeTypesFromDb: {ex}");
            }

            return new NodeTypeCollection();
        }

        public static async Task MigrateDatesToLocalDate(GraphClient client, string label)
        {
            await client.Cypher
                .Match($"(n:{label})")
                .Where("n.Дата IS NOT NULL AND NOT n.Дата IS TYPED DATE")
                .Set("n.Дата = date(n.Дата)")
                .ExecuteWithoutResultsAsync();
        }


        /// <summary>
        /// Метод сохранения типов узлов в базу
        /// </summary>
        /// <param name="client">клиент бд</param>
        /// <param name="nodeTypes">имя нового типа</param>
        /// <returns></returns>
        public static async Task SaveNodeTypesToDb(GraphClient client, NodeTypeCollection nodeTypeCollection)
        {
            try
            {
                // Сериализуем всю коллекцию в JSON
                string json = JsonConvert.SerializeObject(nodeTypeCollection, Formatting.Indented, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

                // Удаляем старый узел коллекции, если существует
                await client.Cypher
                    .Match("(n:NodeTypeCollection)")
                    .Delete("n")
                    .ExecuteWithoutResultsAsync();

                // Создаем новый узел с сериализованной коллекцией
                await client.Cypher
                    .Create("(n:NodeTypeCollection {Data: $json})")
                    .WithParam("json", json)
                    .ExecuteWithoutResultsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении типов узлов: {ex.Message}");
                Console.WriteLine($"Ошибка при сохранении NodeTypeCollection: {ex}");
            }
        }
        /// <summary>
        /// метод для добавление объекта в базу данных
        /// </summary>
        /// <param name="client"></param>
        /// <param name="label">тип который добавляем</param>
        /// <param name="properties">свойство</param>
        /// <param name="propertyTypes">тип свойства</param>
        /// <returns></returns>
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

        /// <summary>
        /// замена свойства
        /// </summary>
        /// <param name="client"></param>
        /// <param name="label"></param>
        /// <param name="oldProperties"></param>
        /// <param name="newProperties"></param>
        /// <param name="propertyTypes"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
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
        /// <summary>
        /// удаление ноды
        /// </summary>
        /// <param name="client"></param>
        /// <param name="label"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
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
        /// <summary>
        /// добавление свойства к существующей ноде
        /// </summary>
        /// <param name="client"></param>
        /// <param name="label"></param>
        /// <param name="newProperty"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
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
                else if (filter.Value is string stringValue) // Обработка строковых фильтров
                {
                    conditions.Add($"n.{filter.Key} CONTAINS $str_{filter.Key}");
                    parameters[$"str_{filter.Key}"] = stringValue;
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

            // Выполнение запроса и получение результатов
            var result = await query
                .Return(n => new
                {
                    Properties = n.As<Dictionary<string, object>>(),
                    Id = n.Id()
                })
                .ResultsAsync;

            // Преобразование результатов в NodeData
            return result.Select(item =>
            {
                item.Properties["Id"] = item.Id;
                return new NodeData
                {
                    Properties = item.Properties,
                    DisplayString = GetNodeDisplayString(item.Properties)
                };
            }).ToList();
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

        public static async Task<List<NodeData>> SearchNodes(GraphClient client, string label, string searchText)
        {
            var query = client.Cypher
                .Match($"(n:{label})")
                .Where($"ANY(prop IN keys(n) WHERE toString(n[prop]) CONTAINS $searchText")
                .WithParam("searchText", searchText)
                .Return(n => n.As<Dictionary<string, object>>());

            var results = (await query.ResultsAsync).ToList();
            var nodes = new List<NodeData>();

            foreach (var props in results)
            {
                nodes.Add(new NodeData
                {
                    Properties = props,
                    DisplayString = GetNodeDisplayString(props)
                });
            }
            return nodes;
        }

        public static async Task CreateRelationship(
            GraphClient client,
            NodeData sourceNode,
            NodeData targetNode,
            string relationshipType)
        {
            long sourceId = (long)sourceNode.Properties["Id"];
            long targetId = (long)targetNode.Properties["Id"];

            // Проверяем, существует ли уже связь
            var exists = await client.Cypher
                .Match($"(a)-[r:{relationshipType}]->(b)")
                .Where("id(a) = $sourceId AND id(b) = $targetId")
                .WithParams(new { sourceId, targetId })
                .Return(r => r.Count())
                .ResultsAsync;

            if (exists.Single() > 0)
            {
                throw new Exception("Связь уже существует!");
            }

            // Создаем связь
            await client.Cypher
                .Match("(a)", "(b)")
                .Where("id(a) = $sourceId")
                .AndWhere("id(b) = $targetId")
                .Create($"(a)-[:{relationshipType}]->(b)")
                .WithParams(new { sourceId, targetId })
                .ExecuteWithoutResultsAsync();
        }
        public static NodeData GetNodeFromString(string nodeString, string label)
        {
            var properties = new Dictionary<string, object>();

            try
            {
                // Удаляем префикс "Node:"
                var content = nodeString.StartsWith("Node: ")
                    ? nodeString.Substring(6)
                    : nodeString;

                // Разбиваем на пары ключ-значение
                var pairs = content.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in pairs)
                {
                    var parts = pair.Split(new[] { ": " }, 2, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();

                        // Пытаемся определить тип
                        if (DateTime.TryParse(value, out DateTime dateValue))
                        {
                            properties[key] = dateValue;
                        }
                        else if (bool.TryParse(value, out bool boolValue))
                        {
                            properties[key] = boolValue;
                        }
                        else
                        {
                            properties[key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при разборе узла: {ex.Message}");
            }

            properties["Label"] = label;
            return new NodeData
            {
                Properties = properties,
                DisplayString = GetNodeDisplayString(properties)
            };
        }
        public static string GetNodeDisplayString(Dictionary<string, object> properties)
        {
            return string.Join(", ", properties
                .Where(p => p.Key != "Label" && p.Key != "Id")
                .Select(p =>
                {
                    if (p.Value is Neo4j.Driver.LocalDate localDate)
                        return $"{p.Key}: {new DateTime(localDate.Year, localDate.Month, localDate.Day):yyyy-MM-dd}";
                    return $"{p.Key}: {p.Value}";
                }));
        }

        public static async Task<List<NodeData>> LoadRelatedNodes(
            GraphClient client,
            NodeData sourceNode)
        {
            long nodeId = (long)sourceNode.Properties["Id"];

            var query = client.Cypher
                .Match($"(a)-[r]-(b)")
                .Where("id(a) = $nodeId")
                .WithParam("nodeId", nodeId)
                .ReturnDistinct(b => new
                {
                    Properties = b.As<Dictionary<string, object>>(),
                    Id = b.Id(),
                    Labels = b.Labels()
                });

            var results = await query.ResultsAsync;
            return results.Select(item =>
            {
                item.Properties["Id"] = item.Id;
                // Используем первую метку как тип узла
                item.Properties["Label"] = item.Labels.FirstOrDefault() ?? "Unknown";
                return new NodeData
                {
                    Properties = item.Properties,
                    DisplayString = GetNodeDisplayString(item.Properties)
                };
            }).ToList();
        }

 
    }

}

