using System.IO;
using Newtonsoft.Json;

namespace NeoClientVis
{
    public static class ConfigManager
    {
        private const string ConfigPath = "config.json";

        public static Neo4jConfig LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonConvert.DeserializeObject<Neo4jConfig>(json);
            }
            else
            {
                // Создаем дефолтный конфиг
                var defaultConfig = new Neo4jConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }
        }

        public static void SaveConfig(Neo4jConfig config)
        {
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}