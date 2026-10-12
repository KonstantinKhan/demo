using System.Linq;
using ExecutionsPlugin;
using Xunit;

public class ExecutionPlanTests
{
    private static readonly PluginConfig Config = new PluginConfig();
    private static readonly BaseObject Base = new BaseObject { IdVersion = 1, Type = "Сборочная единица", Product = "АБВ.001", Version = "1", Name = "Узел" };

    private static TableRow Row(RowKind kind, string designation, params string[] q)
    {
        var quantities = new string[TableRow.ExecutionCount];
        for (var i = 0; i < q.Length; i++) quantities[i] = q[i];
        return new TableRow { Kind = kind, Designation = designation, Name = designation + " имя", Quantities = quantities };
    }

    [Fact]
    public void CreatesExecutionOnlyForFilledColumns()
    {
        var plan = ExecutionPlanBuilder.Build(Base, new[]
        {
            Row(RowKind.Detail, "Д1", "2", null, "3"),
            Row(RowKind.Assembly, "С1", null, null, "1"),
        }, Config);

        Assert.Empty(plan.Errors);
        var executions = plan.Links.Where(l => l.LinkType == "Исполнения").Select(l => l.Child.Product).ToList();
        Assert.Equal(new[] { "АБВ.001-01", "АБВ.001-03" }, executions);
    }

    [Fact]
    public void LinksChildrenWithQuantityAndSharedObjects()
    {
        var plan = ExecutionPlanBuilder.Build(Base, new[] { Row(RowKind.Detail, "Д1", "2", "4,5") }, Config);

        var children = plan.Links.Where(l => l.LinkType == "Состоит из ...").ToList();
        Assert.Equal(new[] { 2.0, 4.5 }, children.Select(l => l.Quantity));
        Assert.Single(plan.Objects, o => o.Product == "Д1");
        Assert.All(children, l => Assert.Equal("Деталь", l.Child.Type));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void RejectsBadQuantity(string text)
    {
        var plan = ExecutionPlanBuilder.Build(Base, new[] { Row(RowKind.Detail, "Д1", text) }, Config);
        Assert.Single(plan.Errors);
    }

    [Fact]
    public void RejectsQuantityWithoutDesignation()
    {
        var plan = ExecutionPlanBuilder.Build(Base, new[] { Row(RowKind.Detail, "", "1") }, Config);
        Assert.Single(plan.Errors);
    }
}
