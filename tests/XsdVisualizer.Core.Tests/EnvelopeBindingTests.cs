namespace XsdVisualizer.Core.Tests;

public class EnvelopeBindingTests
{
    private const string Soap12 = "http://www.w3.org/2003/05/soap-envelope";

    [Fact]
    public void An_envelope_is_bound_to_the_operation_and_direction_of_its_body_and_its_payload_root()
    {
        using var folder = WsdlFixture.Folder();
        var set = new SchemaSetLoader().Open(folder.Path);

        var binding = DocumentBinding.Find($"""
            <soap:Envelope xmlns:soap="{Soap12}"><soap:Body>
              <resultMsg xmlns="{WsdlFixture.Tns}"><retPedido xmlns="urn:loja"><cStat>100</cStat></retPedido></resultMsg>
            </soap:Body></soap:Envelope>
            """, [set]);

        Assert.True(binding.IsEnvelope);
        // resultMsg é a Response de enviar e de enviarZip: dois candidatos, o usuário escolhe.
        Assert.Equal(["enviar Response", "enviarZip Response"],
            binding.MessageCandidates.Select(c => $"{c.Operation.Name} {c.Direction}").Order());
        Assert.Null(binding.BoundMessage);
        Assert.Equal("retPedido", Assert.Single(binding.Candidates).Name);
    }

    [Fact]
    public void A_single_matching_operation_is_bound_automatically()
    {
        using var folder = WsdlFixture.Folder();
        var set = new SchemaSetLoader().Open(folder.Path);

        var binding = DocumentBinding.Find($"""
            <soap:Envelope xmlns:soap="{Soap12}"><soap:Body><dadosMsg xmlns="{WsdlFixture.Tns}"/></soap:Body></soap:Envelope>
            """, [set]);

        Assert.Equal(("enviar", MessageDirection.Request), (binding.BoundMessage!.Operation.Name, binding.BoundMessage.Direction));
        Assert.Empty(binding.Candidates);
    }
}
