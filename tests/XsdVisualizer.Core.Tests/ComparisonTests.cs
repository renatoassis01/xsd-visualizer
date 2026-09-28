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

        Assert.Equal(PairStatus.Unchanged, pair.Status);
    }

    private static string File(GlobalElement? e) => e is null ? "" : Path.GetFileName(e.SourceFile);
}
