namespace XsdVisualizer.Core.Tests;

/// <summary>PL_009_V4 × PL_010_V1.30 da NF-e: a mudança real da reforma tributária.</summary>
[Trait("Category", "Integration")]
public class RealComparisonTests
{
    [Fact]
    public void The_tax_reform_group_appears_as_added_in_the_nfe()
    {
        var comparison = Comparison.Compare(
            new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("PL_009_V4")),
            new SchemaSetLoader().Open(RealSchemaSetsTests.Fixture("PL_010_V1.30")));

        var nfe = comparison.Pairs.Single(p => p.Name == "NFe" && p.After?.SourceFile.EndsWith("nfe_v4.00.xsd") == true);

        Assert.Equal(PairStatus.Modified, nfe.Status);
        Assert.Contains(nfe.Changes(), c => c.Kind == ChangeKind.Added && c.Label == "IBSCBS");
        var diff = nfe.DiffSamples(SampleKind.Maximal);
        Assert.Contains(diff.After, l => l.Kind == DiffLineKind.Added && l.Text.Contains("<IBSCBS>"));
        Assert.False(string.IsNullOrWhiteSpace(comparison.ToMarkdown()));
    }
}
