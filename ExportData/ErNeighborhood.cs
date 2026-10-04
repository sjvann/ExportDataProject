using ExportData.Models.Database;

namespace ExportData
{
    public enum ErChildEnd
    {
        ExactlyOne,
        ZeroOrOne,
        OneOrMany,
        ZeroOrMany,
        Unknown
    }

    public sealed class ErColumn
    {
        public string Name { get; init; } = string.Empty;
        public bool IsPrimaryKey { get; init; }
        public bool IsForeignKey { get; init; }
    }

    public sealed class ErNode
    {
        public string TableName { get; init; } = string.Empty;
        public int Distance { get; init; }
        public bool SchemaLoaded { get; init; }
        public IReadOnlyList<ErColumn> KeyColumns { get; init; } = [];
    }

    public sealed class ErEdge
    {
        public string ParentTable { get; init; } = string.Empty;
        public string ChildTable { get; init; } = string.Empty;
        public string ConstraintName { get; init; } = string.Empty;
        public IReadOnlyList<string> ParentColumns { get; init; } = [];
        public IReadOnlyList<string> ChildColumns { get; init; } = [];
        public ErChildEnd ChildEnd { get; init; }

        public string Label
        {
            get
            {
                var text = $"{ParentTable}({string.Join(", ", ParentColumns)}) → {ChildTable}({string.Join(", ", ChildColumns)})";
                return string.IsNullOrEmpty(ConstraintName) ? text : $"{ConstraintName}: {text}";
            }
        }
    }

    public sealed class ErGraph
    {
        public string CenterTable { get; init; } = string.Empty;
        public int Depth { get; init; }
        public IReadOnlyList<ErNode> Nodes { get; init; } = [];
        public IReadOnlyList<ErEdge> Edges { get; init; } = [];
        public bool HasDeclaredRelations => Edges.Count > 0;
    }

    public static class ErNeighborhood
    {
        public static int NormalizeDepth(int depth) => depth < 1 ? 1 : depth;

        public static ErGraph Build(
            string? centerTable,
            int depth,
            IEnumerable<TableRelation>? relations,
            IReadOnlyDictionary<string, TableSchema?>? schemas)
        {
            depth = NormalizeDepth(depth);
            var center = (centerTable ?? string.Empty).Trim();
            var links = (relations ?? [])
                .Where(relation => !string.IsNullOrWhiteSpace(relation.ParentTable)
                    && !string.IsNullOrWhiteSpace(relation.ChildTable))
                .ToList();

            var nodes = new Dictionary<string, ErNode>(StringComparer.OrdinalIgnoreCase)
            {
                [center] = new ErNode { TableName = center, Distance = 0 }
            };
            var queue = new Queue<string>();
            queue.Enqueue(center);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var distance = nodes[current].Distance;
                if (distance >= depth)
                {
                    continue;
                }

                foreach (var link in links)
                {
                    string? other = null;
                    if (Same(link.ParentTable, current))
                    {
                        other = link.ChildTable.Trim();
                    }
                    else if (Same(link.ChildTable, current))
                    {
                        other = link.ParentTable.Trim();
                    }

                    if (other == null || nodes.ContainsKey(other))
                    {
                        continue;
                    }

                    nodes[other] = new ErNode { TableName = other, Distance = distance + 1 };
                    queue.Enqueue(other);
                }
            }

            var lookup = new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase);
            if (schemas != null)
            {
                foreach (var pair in schemas)
                {
                    lookup[pair.Key] = pair.Value;
                }
            }

            var shaped = nodes.Values
                .Select(node => Shape(node, lookup))
                .OrderBy(node => node.Distance)
                .ThenBy(node => node.TableName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var edges = links
                .Select(link => new
                {
                    Link = link,
                    Parent = Find(nodes, link.ParentTable),
                    Child = Find(nodes, link.ChildTable)
                })
                .Where(item => item.Parent != null && item.Child != null)
                .GroupBy(item => GroupKey(item.Parent!, item.Child!, item.Link.ConstraintName))
                .Select(group =>
                {
                    var first = group.First();
                    var childColumns = Distinct(group.Select(item => item.Link.ChildColumn));
                    return new ErEdge
                    {
                        ParentTable = first.Parent!.TableName,
                        ChildTable = first.Child!.TableName,
                        ConstraintName = (first.Link.ConstraintName ?? string.Empty).Trim(),
                        ParentColumns = Distinct(group.Select(item => item.Link.ParentColumn)),
                        ChildColumns = childColumns,
                        ChildEnd = ChildEndOf(lookup, first.Child!.TableName, childColumns)
                    };
                })
                .OrderBy(edge => edge.ParentTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ChildTable, StringComparer.OrdinalIgnoreCase)
                .ThenBy(edge => edge.ConstraintName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new ErGraph
            {
                CenterTable = center,
                Depth = depth,
                Nodes = shaped,
                Edges = edges
            };
        }

        private static ErNode Shape(ErNode node, Dictionary<string, TableSchema?> lookup)
        {
            lookup.TryGetValue(node.TableName, out var schema);
            var loaded = schema != null;
            var keys = loaded
                ? schema!.Columns
                    .Where(column => column.IsPrimaryKey || column.IsForeignKey)
                    .Select(column => new ErColumn
                    {
                        Name = column.ColumnName,
                        IsPrimaryKey = column.IsPrimaryKey,
                        IsForeignKey = column.IsForeignKey
                    })
                    .ToList()
                : [];

            return new ErNode
            {
                TableName = node.TableName,
                Distance = node.Distance,
                SchemaLoaded = loaded,
                KeyColumns = keys
            };
        }

        private static ErChildEnd ChildEndOf(
            Dictionary<string, TableSchema?> lookup,
            string childTable,
            IReadOnlyList<string> childColumns)
        {
            if (!lookup.TryGetValue(childTable, out var schema) || schema == null || childColumns.Count == 0)
            {
                return ErChildEnd.Unknown;
            }

            var ends = childColumns.Select(name =>
            {
                var column = schema.Columns.FirstOrDefault(item =>
                    string.Equals(item.ColumnName, name, StringComparison.OrdinalIgnoreCase));
                return ColumnEnd(column);
            }).ToList();

            if (ends.Any(end => end == ErChildEnd.Unknown))
            {
                return ErChildEnd.Unknown;
            }

            var first = ends[0];
            return ends.All(end => end == first) ? first : ErChildEnd.Unknown;
        }

        private static ErChildEnd ColumnEnd(ColumnInfo? column)
        {
            if (column == null)
            {
                return ErChildEnd.Unknown;
            }

            if (column.IsPrimaryKey)
            {
                return column.IsNullable ? ErChildEnd.ZeroOrOne : ErChildEnd.ExactlyOne;
            }

            return column.IsNullable ? ErChildEnd.ZeroOrMany : ErChildEnd.OneOrMany;
        }

        private static ErNode? Find(Dictionary<string, ErNode> nodes, string name)
        {
            return nodes.TryGetValue(name.Trim(), out var node) ? node : null;
        }

        private static string GroupKey(ErNode parent, ErNode child, string? constraint)
        {
            return string.Join(
                "\n",
                parent.TableName.ToUpperInvariant(),
                child.TableName.ToUpperInvariant(),
                (constraint ?? string.Empty).Trim().ToUpperInvariant());
        }

        private static IReadOnlyList<string> Distinct(IEnumerable<string?> names)
        {
            var result = new List<string>();
            foreach (var name in names)
            {
                var text = (name ?? string.Empty).Trim();
                if (text.Length == 0 || result.Any(item => string.Equals(item, text, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(text);
            }

            return result;
        }

        private static bool Same(string? left, string? right)
        {
            return string.Equals((left ?? string.Empty).Trim(), (right ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
