namespace XsdVisualizer.Core.Tests;

public class BindingTests
{
    private static string Nota(string versao) => SchemaFolder.Xsd($"""
        <xs:element name="nota">
          <xs:complexType><xs:attribute name="versao" type="xs:string" fixed="{versao}"/></xs:complexType>
        </xs:element>
        """, "urn:nota");

    [Fact]
    public void A_document_with_a_single_candidate_is_bound_automatically()
    {
        using var folder = new SchemaFolder(("nota.xsd", Nota("4.00")), ("outro.xsd", SchemaFolder.Xsd("""<xs:element name="outro" type="xs:string"/>""")));
        var set = new SchemaSetLoader().Open(folder.Path);

        var binding = DocumentBinding.Find("""<nota xmlns="urn:nota" versao="4.00"/>""", [set]);

        Assert.Same(set.GlobalElements.Single(e => e.Name == "nota"), binding.Bound);
    }

    [Fact]
    public void Several_candidates_across_open_schema_sets_are_left_for_the_user_to_choose()
    {
        using var antigo = new SchemaFolder(("nota.xsd", Nota("3.10")));
        using var novo = new SchemaFolder(("nota.xsd", Nota("4.00")));
        var sets = new[] { new SchemaSetLoader().Open(antigo.Path), new SchemaSetLoader().Open(novo.Path) };

        var binding = DocumentBinding.Find("""<nota xmlns="urn:nota" versao="4.00"/>""", sets);

        Assert.Null(binding.Bound);
        Assert.Equal(2, binding.Candidates.Count);
    }

    [Fact]
    public void A_document_without_candidates_is_unbound_and_reports_its_root()
    {
        using var folder = new SchemaFolder(("nota.xsd", Nota("4.00")));
        var set = new SchemaSetLoader().Open(folder.Path);

        var binding = DocumentBinding.Find("""<recibo xmlns="urn:recibo"/>""", [set]);

        Assert.Null(binding.Bound);
        Assert.Empty(binding.Candidates);
        Assert.Equal(("recibo", "urn:recibo"), (binding.RootName, binding.RootNamespace));
    }

    [Fact]
    public void A_malformed_document_has_no_candidates()
    {
        using var folder = new SchemaFolder(("nota.xsd", Nota("4.00")));

        var binding = DocumentBinding.Find("<nota", [new SchemaSetLoader().Open(folder.Path)]);

        Assert.Empty(binding.Candidates);
        Assert.Null(binding.RootName);
    }
}
