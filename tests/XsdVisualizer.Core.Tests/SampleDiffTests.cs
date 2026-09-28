namespace XsdVisualizer.Core.Tests;

public class SampleDiffTests
{
    private static ElementPair Pedido(ComparisonFixture f) => Comparison.Compare(f.Before, f.After).Pairs.Single(p => p.Name == "pedido");

    [Fact]
    public void Maximal_samples_are_aligned_side_by_side_with_added_and_removed_lines()
    {
        using var f = new ComparisonFixture();

        var diff = Pedido(f).DiffSamples(SampleKind.Maximal);

        Assert.Equal(diff.Before.Count, diff.After.Count);
        var added = Assert.Single(diff.After, l => l.Kind == DiffLineKind.Added && l.Text.Contains("<IBSCBS>"));
        Assert.Equal("pedido/IBSCBS", added.Path);
        var removed = Assert.Single(diff.Before, l => l.Kind == DiffLineKind.Removed && l.Text.Contains("<fax>"));
        Assert.Equal("pedido/fax", removed.Path);
        // O Maximal usa o primeiro valor da enumeração (100 nos dois lados): a mudança em cStat só aparece na árvore de Changes.
        Assert.Contains(diff.After, l => l.Kind == DiffLineKind.Unchanged && l.Text.Contains("<cStat>100</cStat>"));
        // Linhas sem par do outro lado ficam em branco, para alinhar.
        Assert.Equal(DiffLineKind.Imaginary, diff.Before[diff.After.ToList().IndexOf(added)].Kind);
    }

    [Fact]
    public void The_minimal_diff_shows_a_field_that_became_required()
    {
        using var f = new ComparisonFixture();

        var diff = Pedido(f).DiffSamples(SampleKind.Minimal);

        Assert.Contains(diff.After, l => l.Kind == DiffLineKind.Added && l.Path == "pedido/xPed");
        Assert.DoesNotContain(diff.Before, l => l.Text.Contains("<xPed>"));
    }

    [Fact]
    public void A_pair_with_one_side_missing_diffs_against_nothing()
    {
        using var f = new ComparisonFixture();

        var novo = Comparison.Compare(f.Before, f.After).Pairs.Single(p => p.Name == "novo").DiffSamples(SampleKind.Maximal);

        Assert.All(novo.After, l => Assert.Equal(DiffLineKind.Added, l.Kind));
        Assert.All(novo.Before, l => Assert.Equal(DiffLineKind.Imaginary, l.Kind));
    }

    [Fact]
    public void Focusing_a_change_inside_a_later_choice_branch_brings_that_branch_into_both_samples()
    {
        static string Xsd(string monofasia) => SchemaFolder.Xsd($"""
            <xs:element name="IBSCBS">
              <xs:complexType>
                <xs:choice>
                  <xs:element name="gIBSCBS" type="xs:string"/>
                  <xs:element name="gIBSCBSMono"><xs:complexType><xs:sequence>{monofasia}</xs:sequence></xs:complexType></xs:element>
                </xs:choice>
              </xs:complexType>
            </xs:element>
            """);
        using var before = new SchemaFolder(("n.xsd", Xsd("""<xs:element name="qBCMono" type="xs:decimal"/><xs:element name="vIBSMono" type="xs:decimal"/>""")));
        using var after = new SchemaFolder(("n.xsd", Xsd("""<xs:element name="vIBSMono" type="xs:decimal"/>""")));
        var pair = Comparison.Compare(new SchemaSetLoader().Open(before.Path), new SchemaSetLoader().Open(after.Path)).Pairs.Single();
        var qBCMono = pair.Changes().Single(c => c.Label == "qBCMono");

        var unfocused = pair.DiffSamples(SampleKind.Maximal);
        var focused = pair.DiffSamples(SampleKind.Maximal, focus: qBCMono);

        Assert.DoesNotContain(unfocused.Before, l => l.Text.Contains("<qBCMono>"));
        Assert.Contains(focused.Before, l => l.Kind == DiffLineKind.Removed && l.Path == "IBSCBS/gIBSCBSMono/qBCMono");
        Assert.Contains(focused.After, l => l.Text.Contains("<gIBSCBSMono>"));
    }
}
