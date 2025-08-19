using Neo4j.Driver;
using Neo4jClient;
using Neo4jClient.Cypher;
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
                item.Properties["Id"] = item.Id;
                NormalizeDateProperty(item.Properties);
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
                    Console.WriteLine($"Загруженные данные NodeTypeCollection: {json}"); // Для отладки
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
            try
            {
                // Запускаем миграцию для всех узлов с существующей датой.
                // Если дата уже date, date(n.Дата) не изменит её.
                // Если строка в формате "yyyy-MM-dd", преобразуется успешно.
                // Если неверный формат, запрос упадёт — обработайте в логе.
                await client.Cypher
.Match($"(n:{label})")
.Where("n.Дата IS NOT NULL")
.Set("n.Дата = date(n.Дата)")
.ExecuteWithoutResultsAsync();
            }
            catch (Exception ex)
            {
                // Логируйте или покажите ошибку (например, если строка не парсится как дата)
                Console.WriteLine($"Ошибка миграции дат для {label}: {ex.Message}");
                MessageBox.Show($"Ошибка миграции дат для типа '{label}': Проверьте формат дат в БД. {ex.Message}");
            }
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
        public static async Task<NodeData> AddNodeToDb(GraphClient client, string label, Dictionary<string, object> properties, Dictionary<string, Type> propertyTypes)
        {
            try
            {
                var validatedProperties = new Dictionary<string, object>();
                foreach (var prop in properties)
                {
                    if (propertyTypes[prop.Key] == typeof(Neo4j.Driver.LocalDate) || propertyTypes[prop.Key] == typeof(DateTime))
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
                var query = client.Cypher
                .Create($"(n:{label} {{ {propertiesString} }})")
                .WithParams(validatedProperties)
                .Return(n => new
                {
                    Properties = n.As<Dictionary<string, object>>(),
                    Id = n.Id()
                });
                var result = (await query.ResultsAsync).Single();
                result.Properties["Id"] = result.Id;
                result.Properties["Label"] = label; // Добавляем для consistency
                NormalizeDateProperty(result.Properties);
                return new NodeData
                {
                    Properties = result.Properties,
                    DisplayString = GetNodeDisplayString(result.Properties)
                };
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
                ICypherFluentQuery query = client.Cypher.Match($"(n:{label})");
                if (oldProperties.TryGetValue("Id", out object idObj) && idObj is long nodeId)
                {
                    // Match по ID (предпочтительно)
                    query = query.Where("id(n) = $nodeId").WithParam("nodeId", nodeId);
                }
                else
                {
                    // Fallback: Match по свойствам
                    var matchProperties = string.Join(" AND ", oldProperties.Select(p => $"n.{p.Key} = ${p.Key}"));
                    query = query.Where(matchProperties).WithParams(oldProperties);
                }
                var setProperties = string.Join(", ", newProperties.Select(p => $"n.{p.Key} = ${p.Key}new"));
                var parameters = new Dictionary<string, object>();
                foreach (var prop in newProperties)
                {
                    if (propertyTypes != null && propertyTypes.TryGetValue(prop.Key, out var type) &&
                    (type == typeof(Neo4j.Driver.LocalDate) || type == typeof(DateTime)) && prop.Value is string dateStr)
                    {
                        if (DateTime.TryParse(dateStr, out var date))
                        {
                            parameters[$"{prop.Key}new"] = new Neo4j.Driver.LocalDate(date.Year, date.Month, date.Day);
                        }
                        else
                        {
                            throw new ArgumentException($"Неверный формат даты для свойства '{prop.Key}': {dateStr}");
                        }
                    }
                    else
                    {
                        parameters[$"{prop.Key}new"] = prop.Value;
                    }
                }
                await query.Set(setProperties).WithParams(parameters).ExecuteWithoutResultsAsync();
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
            try
            {
                // Копируем свойства, исключая "Id" и "Label" (они не в БД)
                var matchPropertiesDict = properties
.Where(p => p.Key != "Id" && p.Key != "Label")
.ToDictionary(p => p.Key, p => p.Value);
                // Если есть "Id", используем его для точного match (предпочтительно)
                if (properties.TryGetValue("Id", out object idObj) && idObj is long nodeId)
                {
                    await client.Cypher
                    .Match($"(n:{label})")
                    .Where("id(n) = $nodeId")
                    .DetachDelete("n")
                    .WithParam("nodeId", nodeId)
                    .ExecuteWithoutResultsAsync();
                }
                else
                {
                    // Fallback: match по свойствам (если ID нет)
                    if (!matchPropertiesDict.Any())
                    {
                        throw new ArgumentException("Нет свойств или ID для match узла.");
                    }
                    var matchConditions = string.Join(" AND ", matchPropertiesDict.Select(p => $"n.{p.Key} = ${p.Key}"));
                    // Подготавливаем параметры с учётом типов
                    var parameters = new Dictionary<string, object>();
                    foreach (var prop in matchPropertiesDict)
                    {
                        if (prop.Value is DateTime dateValue)
                        {
                            parameters[prop.Key] = new Neo4j.Driver.LocalDate(dateValue.Year, dateValue.Month, dateValue.Day);
                        }
                        else
                        {
                            parameters[prop.Key] = prop.Value;
                        }
                    }
                    await client.Cypher
                    .Match($"(n:{label})")
                    .Where(matchConditions)
                    .DetachDelete("n")
                    .WithParams(parameters)
                    .ExecuteWithoutResultsAsync();
                }
            }
            catch (Exception ex)
            {
                // Логируем или бросаем дальше
                Console.WriteLine($"Ошибка удаления: {ex.Message}");
                throw new Exception($"Не удалось удалить узел: {ex.Message}");
            }
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
                        dateConditions.Add($"n.Дата >= date({{year: $fromYear, month: $fromMonth, day: $fromDay}})");
                        parameters["fromYear"] = from.Value.Year;
                        parameters["fromMonth"] = from.Value.Month;
                        parameters["fromDay"] = from.Value.Day;
                    }
                    if (to.HasValue)
                    {
                        dateConditions.Add($"n.Дата <= date({{year: $toYear, month: $toMonth, day: $toDay}})");
                        parameters["toYear"] = to.Value.Year;
                        parameters["toMonth"] = to.Value.Month;
                        parameters["toDay"] = to.Value.Day;
                    }
                    if (dateConditions.Any())
                    {
                        conditions.Add($"n.{filter.Key} IS NOT NULL AND " + string.Join(" AND ", dateConditions));
                    }
                }
                else if (filter.Value is string stringValue) // Обработка строковых фильтров
                {
                    conditions.Add($"toLower(n.{filter.Key}) CONTAINS toLower($str_{filter.Key})");
                    parameters[$"str{filter.Key}"] = stringValue;
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
                NormalizeDateProperty(item.Properties);
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
            .Where($"ANY(prop IN keys(n) WHERE toLower(toString(n[prop])) CONTAINS toLower($searchText))")
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
                if (p.Value is DateTime dtValue)
                    return $"{p.Key}: {dtValue:yyyy-MM-dd}";
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
                NormalizeDateProperty(item.Properties);
                return new NodeData
                {
                    Properties = item.Properties,
                    DisplayString = GetNodeDisplayString(item.Properties)
                };
            }).ToList();
        }
        public static async Task<NodeData> ReplaceNode(
        GraphClient client,
        NodeData sourceNode,
        Dictionary<string, object> newProperties,
        Dictionary<string, Type> propertyTypes,
        string label)
        {
            using (var tx = client.BeginTransaction()) // Атомарность
            {
                try
                {
                    // 1. Создаём новый узел с редактированными свойствами
                    var newNode = await AddNodeToDb(client, label, newProperties, propertyTypes);
                    long sourceId = (long)sourceNode.Properties["Id"];
                    long targetId = (long)newNode.Properties["Id"];
                    // 2. Переносим связи (outgoing и incoming)
                    await TransferOutgoingRelationships(client, sourceId, targetId);
                    await TransferIncomingRelationships(client, sourceId, targetId);
                    // 3. Удаляем все связи от старого узла
                    await client.Cypher
.Match("(a)-[r]-()")
.Where("id(a) = $sourceId")
.Delete("r")
.WithParam("sourceId", sourceId)
.ExecuteWithoutResultsAsync();
                    // 4. Обновляем "Актуальность" старого узла на false (используем match по ID)
                    await UpdateNodeProperties(client, label, sourceNode.Properties, new Dictionary<string, object> { { "Актуальность", false } }, propertyTypes);
                    // 5. Создаём связь "ЗАМЕНЕН_НА"
                    await CreateRelationship(client, sourceNode, newNode, "ЗАМЕНЕН_НА");
                    await tx.CommitAsync();
                    return newNode;
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync();
                    throw new Exception($"Ошибка при замене узла: {ex.Message}", ex);
                }
            }
        }
        private static async Task<NodeData> GetNodeByProperties(
        GraphClient client,
        string label,
        Dictionary<string, object> properties)
        {
            var matchConditions = string.Join(" AND ", properties
            .Where(p => p.Key != "Id" && p.Key != "Label")
            .Select(p => $"n.{p.Key} = ${p.Key}"));
            var query = client.Cypher
            .Match($"(n:{label})")
            .Where(matchConditions)
            .WithParams(properties)
            .Return(n => new
            {
                Properties = n.As<Dictionary<string, object>>(),
                Id = n.Id()
            });
            var result = (await query.ResultsAsync).FirstOrDefault();
            if (result == null)
                throw new Exception("Не удалось найти созданный узел");
            result.Properties["Id"] = result.Id;
            return new NodeData
            {
                Properties = result.Properties,
                DisplayString = GetNodeDisplayString(result.Properties)
            };
        }
        private static async Task TransferRelationships(
        GraphClient client,
        NodeData sourceNode,
        NodeData targetNode)
        {
            long sourceId = (long)sourceNode.Properties["Id"];
            long targetId = (long)targetNode.Properties["Id"];
            // Перенос всех отношений
            var relationships = await client.Cypher
.Match($"(a)-[r]->(b)")
.Where("id(a) = $sourceId")
.WithParam("sourceId", sourceId)
.Return(r => new
{
    Type = r.Type(),
    TargetId = Return.As<long>("id(b)")
})
.ResultsAsync;
            foreach (var rel in relationships)
            {
                // Создаем такую же связь для нового узла
                await client.Cypher
.Match("(a)", "(b)")
.Where("id(a) = $targetId AND id(b) = $relId")
.Create($"(a)-[:{rel.Type}]->(b)")
.WithParams(new
{
    targetId,
    relId = rel.TargetId
})
.ExecuteWithoutResultsAsync();
            }
        }
        private static async Task TransferOutgoingRelationships(GraphClient client, long sourceId, long targetId)
        {
            var relationships = await client.Cypher
            .Match("(a)-[r]->(b)")
            .Where("id(a) = $sourceId")
            .WithParam("sourceId", sourceId)
            .Return(r => new
            {
                Type = r.Type(),
                TargetId = Return.As<long>("id(b)")
            })
            .ResultsAsync;
            foreach (var rel in relationships)
            {
                await client.Cypher
                .Match("(a)", "(b)")
                .Where("id(a) = $targetId AND id(b) = $relId")
                .Create($"(a)-[:{rel.Type}]->(b)")
                .WithParams(new { targetId, relId = rel.TargetId })
                .ExecuteWithoutResultsAsync();
            }
        }
        private static async Task TransferIncomingRelationships(GraphClient client, long sourceId, long targetId)
        {
            var relationships = await client.Cypher
            .Match("(b)-[r]->(a)")
            .Where("id(a) = $sourceId")
            .WithParam("sourceId", sourceId)
            .Return(r => new
            {
                Type = r.Type(),
                SourceId = Return.As<long>("id(b)")
            })
            .ResultsAsync;
            foreach (var rel in relationships)
            {
                await client.Cypher
                .Match("(a)", "(b)")
                .Where("id(a) = $relSourceId AND id(b) = $targetId")
                .Create($"(a)-[:{rel.Type}]->(b)")
                .WithParams(new { relSourceId = rel.SourceId, targetId })
                .ExecuteWithoutResultsAsync();
            }
        }
        private static void NormalizeDateProperty(Dictionary<string, object> properties)
        {
            if (properties.TryGetValue("Дата", out var dateObj))
            {
                DateTime? parsedDate = null;
                if (dateObj is LocalDate ld)
                {
                    parsedDate = new DateTime(ld.Year, ld.Month, ld.Day);
                }
                else if (dateObj is DateTime dt)
                {
                    parsedDate = dt;
                }
                else if (dateObj is string dateStr && DateTime.TryParse(dateStr, out dt))
                {
                    parsedDate = dt;
                }
                if (parsedDate.HasValue)
                {
                    properties["Дата"] = parsedDate.Value;
                }
                else
                {
                    // Если не парсится, удалить или установить default
                    properties["Дата"] = DateTime.MinValue; // Или null, но Neo4j не любит null
                }
            }
        }
    }
}