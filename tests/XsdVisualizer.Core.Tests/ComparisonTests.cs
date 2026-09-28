namespace XsdVisualizer.Core.Tests;

public class ComparisonTests
{
    [Fact]
    public void Global_elements_are_paired_and_each_pair_has_a_status()
    {
        using var f = new ComparisonFixture();

        var comparison = Comparison.Compare(f.Before, f.After);

        Assert.Equal(
            ["igual Unchanged", "legado Removed", "novo Added", "pedido Modified"],
            comparison.Pairs.Select(p => $"{p.Name} {p.Status}").Order());
        var pedido = comparison.Pairs.Single(p => p.Name == "pedido");
        Assert.Same(f.Before.GlobalElements.Single(e => e.Name == "pedido"), pedido.Before);
        Assert.Same(f.After.GlobalElements.Single(e => e.Name == "pedido"), pedido.After);
    }

    [Fact]
    public void Same_named_elements_pair_by_declaring_file_first_and_ambiguous_ones_stay_unpaired()
    {
        using var before = new SchemaFolder(
            ("envCCe_v1.00.xsd", SchemaFolder.Xsd("""<xs:element name="envEvento" type="xs:string"/>""")),
            ("envEPEC_v1.00.xsd", SchemaFolder.Xsd("""<xs:element name="envEvento" type="xs:int"/>""")));
        using var after = new SchemaFolder(
            ("envCCe_v1.00.xsd", SchemaFolder.Xsd("""<xs:element name="envEvento" type="xs:string"/>""")),
            ("envEPEC_v2.00.xsd", SchemaFolder.Xsd("""<xs:element name="envEvento" type="xs:long"/>""")),
            ("envOutro_v1.00.xsd", SchemaFolder.Xsd("""<xs:element name="envEvento" type="xs:long"/>""")));

        var comparison = Comparison.Compare(new SchemaSetLoader().Open(before.Path), new SchemaSetLoader().Open(after.Path));

        string[] expected = ["envCCe_v1.00.xsd→envCCe_v1.00.xsd Unchanged", "envEPEC_v1.00.xsd→ Removed", "→envEPEC_v2.00.xsd Added", "→envOutro_v1.00.xsd Added"];
        Assert.Equal(expected.Order(), comparison.Pairs.Select(p => $"{File(p.Before)}→{File(p.After)} {p.Status}").Order());
    }

    [Fact]
    public void Two_global_elements_can_be_paired_by_hand()
    {
        using var f = new ComparisonFixture();

        var pair = ElementPair.Manual(f.Before.GlobalElements.Single(e => e.Name == "legado"), f.After.GlobalElements.Single(e => e.Name == "novo"));

        Assert.Equal(ChangeKind.Unchanged, pair.Status);
    }

    [Fact]
    public void The_markdown_summary_lists_new_removed_and_changed_elements_with_their_changes()
    {
        using var f = new ComparisonFixture();

        var markdown = Comparison.Compare(f.Before, f.After).ToMarkdown();

        Assert.Contains($"{f.Before.Name} → {f.After.Name}", markdown);
        Assert.Contains("- novo", markdown);
        Assert.Contains("- legado", markdown);
        Assert.Contains("| pedido/xPed | Modified | cardinality: 0..1 → 1..1; maxLength: 15 → 60 |", markdown);
        Assert.Contains("| pedido/cStat | Modified | enumeration: +150, +151, −999 |", markdown);
        Assert.Contains("| pedido/IBSCBS | Added | |", markdown);
        Assert.Contains("| pedido/fax | Removed | |", markdown);
        Assert.DoesNotContain("pedido/obs", markdown);
        Assert.DoesNotContain("igual", markdown);
        Assert.Contains("pedido/obs", Comparison.Compare(f.Before, f.After).ToMarkdown(includeDocumentation: true));
    }

    [Fact]
    public void A_pair_whose_only_change_is_documentation_has_that_status()
    {
        static string Xsd(string doc) => SchemaFolder.Xsd($"""
            <xs:element name="obs" type="xs:string"><xs:annotation><xs:documentation>{doc}</xs:documentation></xs:annotation></xs:element>
            """);
        using var before = new SchemaFolder(("o.xsd", Xsd("Observacao")));
        using var after = new SchemaFolder(("o.xsd", Xsd("Observação")));

        var pair = Comparison.Compare(new SchemaSetLoader().Open(before.Path), new SchemaSetLoader().Open(after.Path)).Pairs.Single();

        Assert.Equal(ChangeKind.DocumentationOnly, pair.Status);
    }

    private static string File(GlobalElement? e) => e is null ? "" : Path.GetFileName(e.SourceFile);
}
