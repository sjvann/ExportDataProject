using System.Globalization;
using ExportData;

namespace ExportDataWeb.Services;

public sealed class ErCanvas
{
    public double Width { get; init; }
    public double Height { get; init; }
    public IReadOnlyList<ErBox> Boxes { get; init; } = [];
    public IReadOnlyList<ErWire> Wires { get; init; } = [];
}

public sealed class ErBox
{
    public ErNode Node { get; init; } = new();
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double HeaderHeight { get; init; }
}

public sealed class ErWire
{
    public ErEdge Edge { get; init; } = new();
    public string Line { get; init; } = string.Empty;
    public string ParentBar { get; init; } = string.Empty;
    public string? ChildBar { get; init; }
    public string? ChildFoot { get; init; }
    public double? ZeroX { get; init; }
    public double? ZeroY { get; init; }
    public double? LabelX { get; init; }
    public double? LabelY { get; init; }
}

public static class ErDiagramLayout
{
    public static string Px(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Int(double value) => Math.Ceiling(value).ToString("0", CultureInfo.InvariantCulture);

    private const double HeaderHeight = 32;
    private const double RowHeight = 20;
    private const double ColumnGap = 88;
    private const double RowGap = 28;
    private const double Pad = 28;

    public static ErCanvas Arrange(ErGraph graph)
    {
        var columns = graph.Nodes
            .GroupBy(node => node.Distance)
            .OrderBy(group => group.Key)
            .ToList();

        var boxes = new List<ErBox>();
        var x = Pad;
        foreach (var column in columns)
        {
            var drafted = column.Select(node => Draft(node)).ToList();
            var width = drafted.Max(box => box.Width);
            var y = Pad;
            foreach (var box in drafted)
            {
                boxes.Add(new ErBox
                {
                    Node = box.Node,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = box.Height,
                    HeaderHeight = HeaderHeight
                });
                y += box.Height + RowGap;
            }

            x += width + ColumnGap;
        }

        var wires = new List<ErWire>();
        var groups = graph.Edges
            .Select((edge, index) => (edge, index))
            .GroupBy(item => item.edge.ParentTable + "\n" + item.edge.ChildTable, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var items = group.ToList();
            for (var i = 0; i < items.Count; i++)
            {
                var parent = boxes.First(box => string.Equals(box.Node.TableName, items[i].edge.ParentTable, StringComparison.OrdinalIgnoreCase));
                var child = boxes.First(box => string.Equals(box.Node.TableName, items[i].edge.ChildTable, StringComparison.OrdinalIgnoreCase));
                wires.Add(Wire(items[i].edge, parent, child, (i + 1d) / (items.Count + 1d)));
            }
        }

        var widthPx = boxes.Count == 0 ? 320 : boxes.Max(box => box.X + box.Width) + Pad;
        var heightPx = boxes.Count == 0 ? 180 : boxes.Max(box => box.Y + box.Height) + Pad;
        widthPx = Math.Max(widthPx, wires.Count == 0 ? 0 : widthPx + 8);
        if (graph.Edges.Any(edge => string.Equals(edge.ParentTable, edge.ChildTable, StringComparison.OrdinalIgnoreCase)))
        {
            widthPx += 48;
        }

        return new ErCanvas
        {
            Width = widthPx,
            Height = heightPx,
            Boxes = boxes,
            Wires = wires
        };
    }

    private static ErBox Draft(ErNode node)
    {
        var lines = BodyLines(node);
        var longest = Math.Max(node.TableName.Length, lines.Count == 0 ? 0 : lines.Max(line => line.Length));
        var width = Math.Clamp(36 + longest * 7.4, 196, 360);
        var height = HeaderHeight + Math.Max(1, lines.Count) * RowHeight + 10;
        return new ErBox { Node = node, Width = width, Height = height };
    }

    public static IReadOnlyList<string> BodyLines(ErNode node)
    {
        if (!node.SchemaLoaded)
        {
            return ["欄位讀不到"];
        }

        if (node.KeyColumns.Count == 0)
        {
            return ["沒有鍵欄位"];
        }

        return node.KeyColumns.Select(column =>
        {
            var mark = column.IsPrimaryKey && column.IsForeignKey
                ? "PK FK"
                : column.IsPrimaryKey
                    ? "PK"
                    : "FK";
            return $"{mark}  {column.Name}";
        }).ToList();
    }

    private static ErWire Wire(ErEdge edge, ErBox parent, ErBox child, double slot)
    {
        double x1;
        double y1;
        double x2;
        double y2;
        double ux;
        double uy;
        var self = string.Equals(parent.Node.TableName, child.Node.TableName, StringComparison.OrdinalIgnoreCase);

        if (self)
        {
            x1 = parent.X + parent.Width;
            y1 = parent.Y + parent.HeaderHeight + 8;
            x2 = parent.X + parent.Width;
            y2 = parent.Y + parent.Height - 10;
            ux = -1;
            uy = 0;
        }
        else if (Math.Abs(parent.X - child.X) < 1)
        {
            var downward = child.Y >= parent.Y;
            x1 = parent.X + parent.Width * slot;
            y1 = downward ? parent.Y + parent.Height : parent.Y;
            x2 = child.X + child.Width * slot;
            y2 = downward ? child.Y : child.Y + child.Height;
            ux = 0;
            uy = downward ? 1 : -1;
        }
        else if (child.X >= parent.X)
        {
            x1 = parent.X + parent.Width;
            y1 = parent.Y + parent.Height * slot;
            x2 = child.X;
            y2 = child.Y + child.Height * slot;
            (ux, uy) = Unit(x2 - x1, y2 - y1);
        }
        else
        {
            x1 = parent.X;
            y1 = parent.Y + parent.Height * slot;
            x2 = child.X + child.Width;
            y2 = child.Y + child.Height * slot;
            (ux, uy) = Unit(x2 - x1, y2 - y1);
        }

        var px = -uy;
        var py = ux;
        var many = edge.ChildEnd is ErChildEnd.OneOrMany or ErChildEnd.ZeroOrMany;
        var optional = edge.ChildEnd is ErChildEnd.ZeroOrOne or ErChildEnd.ZeroOrMany;
        var unknown = edge.ChildEnd == ErChildEnd.Unknown;
        var heelX = x2 - ux * 12;
        var heelY = y2 - uy * 12;
        double endX;
        double endY;
        string? childBar = null;
        string? childFoot = null;
        double? zeroX = null;
        double? zeroY = null;
        double? labelX = null;
        double? labelY = null;

        if (many)
        {
            childFoot = $"M {N(heelX)} {N(heelY)} L {N(x2)} {N(y2)} M {N(heelX)} {N(heelY)} L {N(x2 + px * 7)} {N(y2 + py * 7)} M {N(heelX)} {N(heelY)} L {N(x2 - px * 7)} {N(y2 - py * 7)}";
            if (optional)
            {
                zeroX = heelX - ux * 9;
                zeroY = heelY - uy * 9;
                endX = heelX - ux * 16;
                endY = heelY - uy * 16;
            }
            else
            {
                childBar = Bar(heelX, heelY, px, py, 7);
                endX = heelX;
                endY = heelY;
            }
        }
        else if (unknown)
        {
            endX = x2 - ux * 10;
            endY = y2 - uy * 10;
            labelX = endX + px * 8;
            labelY = endY + py * 8;
        }
        else
        {
            childBar = Bar(x2, y2, px, py, 7);
            if (optional)
            {
                zeroX = x2 - ux * 9;
                zeroY = y2 - uy * 9;
                endX = x2 - ux * 16;
                endY = y2 - uy * 16;
            }
            else
            {
                endX = x2;
                endY = y2;
            }
        }

        var line = self
            ? $"M {N(x1)} {N(y1)} C {N(x1 + 42)} {N(y1)}, {N(x1 + 42)} {N(endY)}, {N(endX)} {N(endY)}"
            : $"M {N(x1)} {N(y1)} L {N(endX)} {N(endY)}";

        return new ErWire
        {
            Edge = edge,
            Line = line,
            ParentBar = Bar(x1, y1, px, py, 7),
            ChildBar = childBar,
            ChildFoot = childFoot,
            ZeroX = zeroX,
            ZeroY = zeroY,
            LabelX = labelX,
            LabelY = labelY
        };
    }

    private static string Bar(double x, double y, double px, double py, double half)
    {
        return $"M {N(x - px * half)} {N(y - py * half)} L {N(x + px * half)} {N(y + py * half)}";
    }

    private static (double X, double Y) Unit(double x, double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        if (length < 0.5)
        {
            return (1, 0);
        }

        return (x / length, y / length);
    }

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
