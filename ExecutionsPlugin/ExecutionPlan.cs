using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExecutionsPlugin
{
    internal enum RowKind
    {
        Assembly,
        Detail
    }

    /// <summary>Строка таблицы: обозначение, наименование и количество по исполнениям 01..05 (текст, как ввёл пользователь).</summary>
    internal sealed class TableRow
    {
        public const int ExecutionCount = 5;

        public RowKind Kind { get; set; }
        public string Designation { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string[] Quantities { get; set; } = new string[ExecutionCount];
    }

    internal sealed class BaseObject
    {
        public long IdVersion { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Объект, который нужно найти или создать.</summary>
    internal sealed class ObjectSpec
    {
        public string Type { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public string Key => Type + "\u0001" + Product;
    }

    /// <summary>Связь родитель -> потомок с количеством (min = max).</summary>
    internal sealed class LinkSpec
    {
        public ObjectSpec Parent { get; set; }
        public ObjectSpec Child { get; set; }
        public string LinkType { get; set; } = string.Empty;
        public double Quantity { get; set; }
    }

    internal sealed class ExecutionPlan
    {
        public List<ObjectSpec> Objects { get; } = new List<ObjectSpec>();
        public List<LinkSpec> Links { get; } = new List<LinkSpec>();
        public List<string> Errors { get; } = new List<string>();
        public bool IsEmpty => Links.Count == 0;
    }

    internal static class ExecutionPlanBuilder
    {
        /// <summary>Обозначение исполнения: «база-01».</summary>
        public static string ExecutionProduct(string baseProduct, int number) =>
            $"{baseProduct}-{number:00}";

        public static ExecutionPlan Build(BaseObject baseObject, IEnumerable<TableRow> rows, PluginConfig config)
        {
            var plan = new ExecutionPlan();
            var seen = new Dictionary<string, ObjectSpec>();
            var baseSpec = new ObjectSpec { Type = baseObject.Type, Product = baseObject.Product, Name = baseObject.Name };
            seen[baseSpec.Key] = baseSpec;

            var filled = rows.Where(r => r != null && !IsBlank(r)).ToList();
            var usedPairs = new HashSet<string>();

            for (var column = 0; column < TableRow.ExecutionCount; column++)
            {
                var number = column + 1;
                ObjectSpec execution = null;

                foreach (var row in filled)
                {
                    var text = column < row.Quantities.Length ? row.Quantities[column] : null;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    var label = $"Исполнение {number:00}, «{row.Designation}»";
                    if (string.IsNullOrWhiteSpace(row.Designation))
                    {
                        plan.Errors.Add($"{label}: не задано обозначение.");
                        continue;
                    }

                    if (!TryParseQuantity(text, out var quantity))
                    {
                        plan.Errors.Add($"{label}: количество «{text}» должно быть числом больше 0.");
                        continue;
                    }

                    if (execution == null)
                    {
                        execution = new ObjectSpec
                        {
                            Type = config.AssemblyType,
                            Product = ExecutionProduct(baseObject.Product, number),
                            Name = baseObject.Name
                        };
                        Register(plan, seen, execution);
                        plan.Links.Add(new LinkSpec
                        {
                            Parent = baseSpec,
                            Child = seen[execution.Key],
                            LinkType = config.ExecutionLinkType,
                            Quantity = 1
                        });
                    }

                    var child = new ObjectSpec
                    {
                        Type = row.Kind == RowKind.Assembly ? config.AssemblyType : config.DetailType,
                        Product = row.Designation.Trim(),
                        Name = row.Name?.Trim() ?? string.Empty
                    };
                    var childSpec = Register(plan, seen, child);

                    if (!usedPairs.Add(execution.Key + "\u0002" + childSpec.Key))
                    {
                        plan.Errors.Add($"{label}: обозначение повторяется в составе исполнения.");
                        continue;
                    }

                    plan.Links.Add(new LinkSpec
                    {
                        Parent = seen[execution.Key],
                        Child = childSpec,
                        LinkType = config.CompositionLinkType,
                        Quantity = quantity
                    });
                }
            }

            return plan;
        }

        public static bool TryParseQuantity(string text, out double value)
        {
            var normalized = (text ?? string.Empty).Trim().Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        private static ObjectSpec Register(ExecutionPlan plan, Dictionary<string, ObjectSpec> seen, ObjectSpec spec)
        {
            if (seen.TryGetValue(spec.Key, out var existing))
            {
                if (string.IsNullOrEmpty(existing.Name) && !string.IsNullOrEmpty(spec.Name))
                {
                    existing.Name = spec.Name;
                }

                return existing;
            }

            seen[spec.Key] = spec;
            plan.Objects.Add(spec);
            return spec;
        }

        private static bool IsBlank(TableRow row) =>
            string.IsNullOrWhiteSpace(row.Designation)
            && string.IsNullOrWhiteSpace(row.Name)
            && (row.Quantities == null || row.Quantities.All(string.IsNullOrWhiteSpace));
    }
}
