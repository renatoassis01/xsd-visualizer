namespace XsdVisualizer.Core.Tests;

public class StructuralDiffTests
{
    private static ElementPair Pedido(ComparisonFixture f) => Comparison.Compare(f.Before, f.After).Pairs.Single(p => p.Name == "pedido");

    [Fact]
    public void The_change_tree_is_the_union_of_both_versions_in_after_order()
    {
        using var f = new ComparisonFixture();

        var tree = Pedido(f).Tree;

        Assert.Equal(
            ["@versao Unchanged", "numero Unchanged", "xPed Modified", "cStat Modified", "obs DocumentationOnly", "fax Removed", "IBSCBS Added"],
            tree.Children.Select(c => $"{c.Label} {c.Kind}"));
        Assert.Equal((4, 1), (tree.ChangeCount, tree.DocumentationChangeCount));
        Assert.Equal("pedido/xPed", tree.Children[2].Path);
    }

    [Fact]
    public void Modified_nodes_say_what_changed_before_and_after()
    {
        using var f = new ComparisonFixture();

        var changes = Pedido(f).Changes().ToDictionary(c => c.Label);

        Assert.Equal(
            ["cardinality 0..1 → 1..1", "maxLength 15 → 60"],
            changes["xPed"].Differences.Select(d => $"{d.Property} {d.Before} → {d.After}"));
        var enumeration = Assert.Single(changes["cStat"].Differences);
        Assert.Equal(["150", "151"], enumeration.AddedValues);
        Assert.Equal(["999"], enumeration.RemovedValues);
    }

    [Fact]
    public void Documentation_only_changes_are_left_out_unless_asked_for()
    {
        using var f = new ComparisonFixture();

        Assert.Equal(["xPed", "cStat", "fax", "IBSCBS"], Pedido(f).Changes().Select(c => c.Label));
        Assert.Equal(["xPed", "cStat", "obs", "fax", "IBSCBS"], Pedido(f).Changes(includeDocumentation: true).Select(c => c.Label));
    }

    [Fact]
    public void Moving_an_element_into_a_choice_makes_it_effectively_optional()
    {
        using var before = new SchemaFolder(("p.xsd", SchemaFolder.Xsd("""
            <xs:element name="dest"><xs:complexType><xs:sequence><xs:element name="CPF" type="xs:string"/></xs:sequence></xs:complexType></xs:element>
            """)));
        using var after = new SchemaFolder(("p.xsd", SchemaFolder.Xsd("""
            <xs:element name="dest"><xs:complexType><xs:choice><xs:element name="CPF" type="xs:string"/><xs:element name="CNPJ" type="xs:string"/></xs:choice></xs:complexType></xs:element>
            """)));

        var pair = Comparison.Compare(new SchemaSetLoader().Open(before.Path), new SchemaSetLoader().Open(after.Path)).Pairs.Single();

        Assert.Equal(["CPF Modified cardinality 1..1 → 0..1", "CNPJ Added "],
            pair.Changes().Select(c => $"{c.Label} {c.Kind} {string.Join(",", c.Differences.Select(d => $"{d.Property} {d.Before} → {d.After}"))}"));
    }

    [Fact]
    public void A_schema_set_compared_with_itself_has_no_changes()
    {
        using var f = new ComparisonFixture();

        var comparison = Comparison.Compare(f.Before, new SchemaSetLoader().Open(f.BeforeFolder.Path));

        Assert.All(comparison.Pairs, p => Assert.Equal(PairStatus.Unchanged, p.Status));
    }
}
