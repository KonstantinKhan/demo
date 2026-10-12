using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace ExecutionsPlugin
{
    /// <summary>Имена типов/связей/состояния. Лежат в plugin-config.json рядом с DLL, чтобы не пересобирать при смене имён.</summary>
    internal sealed class PluginConfig
    {
        public string AssemblyType { get; set; } = "Сборочная единица";
        public string DetailType { get; set; } = "Деталь";
        public string ExecutionLinkType { get; set; } = "Исполнения";
        public string CompositionLinkType { get; set; } = "Состоит из ...";
        /// <summary>Состояние новых объектов. Пусто = ошибка при создании: задать в plugin-config.json.</summary>
        public string NewObjectState { get; set; } = string.Empty;
        public string NameAttribute { get; set; } = "Наименование";
        /// <summary>Идентификатор единицы измерения для связи (stIdUnit). Пусто = без единицы.</summary>
        public string UnitId { get; set; } = string.Empty;

        public static PluginConfig Load()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            var path = Path.Combine(dir, "plugin-config.json");
            if (!File.Exists(path))
            {
                return new PluginConfig();
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(path), options)
                   ?? throw new InvalidOperationException("plugin-config.json пуст.");
        }
    }
}
