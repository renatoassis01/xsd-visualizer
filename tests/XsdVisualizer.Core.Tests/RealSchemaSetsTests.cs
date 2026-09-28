namespace XsdVisualizer.Core.Tests;

/// <summary>Integração com pacotes reais de schemas da SEFAZ (em tests/Fixtures).</summary>
[Trait("Category", "Integration")]
public class RealSchemaSetsTests
{
    public static string Fixture(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "Fixtures", name);
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException(name);
    }

    [Theory]
    [InlineData("PL_NFGas_NT2026.002_RTC_1.01")]
    [InlineData("PL_010_V1.30")]
    public void Every_coverage_sample_of_every_global_element_is_valid(string package)
    {
        var set = new SchemaSetLoader().Open(Fixture(package));

        var failures = set.GlobalElements
            .SelectMany(e => e.GenerateCoverageSet().Append(e.GenerateMinimal()))
            .Where(s => !s.IsValid)
            .Select(s => $"{Path.GetFileName(s.Element.SourceFile)} {s.FileName}: {s.Issues.First()}")
            .ToList();

        Assert.True(failures.Count == 0, $"{failures.Count} Samples inválidos:\n" + string.Join("\n", failures.Take(40)));
    }

    [Fact]
    public void The_nfgas_package_loads_cleanly()
    {
        var set = new SchemaSetLoader().Open(Fixture("PL_NFGas_NT2026.002_RTC_1.01"));

        Assert.Empty(set.LoadIssues);
        Assert.Contains(set.GlobalElements, e => e.Name == "NFGas");
    }
}
