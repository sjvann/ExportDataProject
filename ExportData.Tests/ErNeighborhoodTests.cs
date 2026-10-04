using ExportData.Models.Database;
using Xunit;

namespace ExportData.Tests;

public sealed class ErNeighborhoodTests
{
    [Fact(DisplayName = "一層只含中心表與直接相連的父表、子表")]
    public void DepthOne_IncludesOnlyDirectNeighbors()
    {
        var graph = ErNeighborhood.Build("Orders", 1, OrderChain(), Schemas());

        Assert.Equal(["Customers", "OrderLines", "Orders"], Names(graph));
        Assert.Equal(2, graph.Edges.Count);
        Assert.DoesNotContain(graph.Nodes, node => node.TableName == "Products");
    }

    [Fact(DisplayName = "n 層沿宣告外鍵再走，不含更遠的表")]
    public void DepthTwo_StopsBeforeTheNextTable()
    {
        var graph = ErNeighborhood.Build("Orders", 2, OrderChain(), Schemas());

        Assert.Equal(["Customers", "OrderLines", "Orders", "Products"], Names(graph));
        Assert.DoesNotContain(graph.Nodes, node => node.TableName == "Warehouses");
        Assert.Contains(graph.Edges, edge => edge.ParentTable == "Products" && edge.ChildTable == "OrderLines");
    }

    [Fact(DisplayName = "已選入的兩表之間另有外鍵時也畫")]
    public void ExtraEdge_BetweenSelectedTables_IsDrawn()
    {
        var links = OrderChain().Append(Link("Customers", "Id", "OrderLines", "CustomerId", "fk_line_customer"));
        var graph = ErNeighborhood.Build("Orders", 1, links, Schemas());

        Assert.Contains(graph.Edges, edge => edge.ConstraintName == "fk_line_customer");
    }

    [Fact(DisplayName = "環不會重複展開")]
    public void Cycle_DoesNotRepeatTables()
    {
        var links = new[]
        {
            Link("A", "Id", "B", "AId", "fk_b"),
            Link("B", "Id", "A", "BId", "fk_a")
        };

        var graph = ErNeighborhood.Build("A", 5, links, null);

        Assert.Equal(["A", "B"], Names(graph));
        Assert.Equal(2, graph.Edges.Count);
    }

    [Fact(DisplayName = "沒有宣告關聯時只有中心表")]
    public void NoRelations_OnlyCenter()
    {
        var graph = ErNeighborhood.Build("Orders", 3, [], Schemas());

        Assert.Equal(["Orders"], Names(graph));
        Assert.False(graph.HasDeclaredRelations);
    }

    [Theory(DisplayName = "小於 1 的層數改回 1")]
    [InlineData(0)]
    [InlineData(-4)]
    public void IllegalDepth_BehavesAsOne(int depth)
    {
        var graph = ErNeighborhood.Build("Orders", depth, OrderChain(), Schemas());

        Assert.Equal(1, graph.Depth);
        Assert.Equal(["Customers", "OrderLines", "Orders"], Names(graph));
    }

    [Fact(DisplayName = "同一約束的多欄合成一條線")]
    public void MultiColumnConstraint_BecomesOneEdge()
    {
        var links = new[]
        {
            Link("Parent", "A", "Child", "A", "fk_pair"),
            Link("Parent", "B", "Child", "B", "fk_pair")
        };
        var schemas = new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Child"] = Table(
                Column("A", primary: false, nullable: true, foreign: true),
                Column("B", primary: false, nullable: true, foreign: true))
        };

        var graph = ErNeighborhood.Build("Parent", 1, links, schemas);
        var edge = Assert.Single(graph.Edges);

        Assert.Equal(["A", "B"], edge.ParentColumns);
        Assert.Equal(["A", "B"], edge.ChildColumns);
        Assert.Equal(ErChildEnd.ZeroOrMany, edge.ChildEnd);
    }

    [Fact(DisplayName = "子端依主鍵與可否空值決定基數")]
    public void ChildEnd_FollowsPrimaryKeyAndNullability()
    {
        Assert.Equal(ErChildEnd.ExactlyOne, EndOf(primary: true, nullable: false));
        Assert.Equal(ErChildEnd.ZeroOrOne, EndOf(primary: true, nullable: true));
        Assert.Equal(ErChildEnd.OneOrMany, EndOf(primary: false, nullable: false));
        Assert.Equal(ErChildEnd.ZeroOrMany, EndOf(primary: false, nullable: true));
    }

    [Fact(DisplayName = "子端欄位基數不一致時整條標未明")]
    public void DisagreeingColumns_AreUnknown()
    {
        var links = new[]
        {
            Link("Parent", "A", "Child", "A", "fk_pair"),
            Link("Parent", "B", "Child", "B", "fk_pair")
        };
        var schemas = new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Child"] = Table(
                Column("A", primary: true, nullable: false, foreign: true),
                Column("B", primary: false, nullable: true, foreign: true))
        };

        var edge = Assert.Single(ErNeighborhood.Build("Parent", 1, links, schemas).Edges);

        Assert.Equal(ErChildEnd.Unknown, edge.ChildEnd);
    }

    [Fact(DisplayName = "鄰表結構讀不到時子端為未明，節點仍在")]
    public void MissingSchema_KeepsNodeAndMarksUnknown()
    {
        var schemas = new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders"] = Table(Column("Id", primary: true, nullable: false)),
            ["OrderLines"] = null
        };

        var graph = ErNeighborhood.Build("Orders", 1, OrderChain(), schemas);
        var orderLines = Assert.Single(graph.Nodes, node => node.TableName == "OrderLines");
        var edge = Assert.Single(graph.Edges, item => item.ChildTable == "OrderLines");

        Assert.False(orderLines.SchemaLoaded);
        Assert.Empty(orderLines.KeyColumns);
        Assert.Equal(ErChildEnd.Unknown, edge.ChildEnd);
    }

    [Fact(DisplayName = "節點只列主鍵與外鍵")]
    public void Node_ListsOnlyKeyColumns()
    {
        var graph = ErNeighborhood.Build("Orders", 1, OrderChain(), Schemas());
        var orders = Assert.Single(graph.Nodes, node => node.TableName == "Orders");

        Assert.Equal(["Id", "CustomerId"], orders.KeyColumns.Select(column => column.Name).ToArray());
    }

    private static ErChildEnd EndOf(bool primary, bool nullable)
    {
        var schemas = new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Child"] = Table(Column("Fk", primary, nullable, foreign: true))
        };
        var links = new[] { Link("Parent", "Id", "Child", "Fk", "fk") };
        return Assert.Single(ErNeighborhood.Build("Parent", 1, links, schemas).Edges).ChildEnd;
    }

    private static string[] Names(ErGraph graph)
    {
        return graph.Nodes.Select(node => node.TableName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<TableRelation> OrderChain()
    {
        return
        [
            Link("Customers", "Id", "Orders", "CustomerId", "fk_order_customer"),
            Link("Orders", "Id", "OrderLines", "OrderId", "fk_line_order"),
            Link("Products", "Id", "OrderLines", "ProductId", "fk_line_product"),
            Link("Warehouses", "Id", "Products", "WarehouseId", "fk_product_warehouse")
        ];
    }

    private static Dictionary<string, TableSchema?> Schemas()
    {
        return new Dictionary<string, TableSchema?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Customers"] = Table(Column("Id", primary: true, nullable: false)),
            ["Orders"] = Table(
                Column("Id", primary: true, nullable: false),
                Column("CustomerId", primary: false, nullable: false, foreign: true),
                Column("Note", primary: false, nullable: true)),
            ["OrderLines"] = Table(
                Column("OrderId", primary: false, nullable: false, foreign: true),
                Column("ProductId", primary: false, nullable: true, foreign: true)),
            ["Products"] = Table(
                Column("Id", primary: true, nullable: false),
                Column("WarehouseId", primary: false, nullable: true, foreign: true)),
            ["Warehouses"] = Table(Column("Id", primary: true, nullable: false))
        };
    }

    private static TableRelation Link(string parent, string parentColumn, string child, string childColumn, string constraint)
    {
        return new TableRelation
        {
            ParentTable = parent,
            ParentColumn = parentColumn,
            ChildTable = child,
            ChildColumn = childColumn,
            ConstraintName = constraint
        };
    }

    private static TableSchema Table(params ColumnInfo[] columns)
    {
        return new TableSchema { Columns = columns.ToList() };
    }

    private static ColumnInfo Column(string name, bool primary, bool nullable, bool foreign = false)
    {
        return new ColumnInfo
        {
            ColumnName = name,
            IsPrimaryKey = primary,
            IsNullable = nullable,
            IsForeignKey = foreign
        };
    }
}
