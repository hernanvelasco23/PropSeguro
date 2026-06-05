using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Domain.Tests;

public sealed class DocumentTextQualityEvaluatorTests
{
    [Fact]
    public void Reservation_text_with_only_signnow_audit_metadata_is_poor()
    {
        var text = "signNow historial del documento firmantes auditoria document id ip address correo electronico";

        var quality = DocumentTextQualityEvaluator.Evaluate(DocumentType.ReservationAgreement, text);

        Assert.Equal(TextExtractionQuality.Poor, quality);
        Assert.True(DocumentTextQualityEvaluator.ShouldAttemptOcr(DocumentType.ReservationAgreement, text));
    }

    [Fact]
    public void Reservation_text_with_compacted_signnow_audit_and_document_name_is_poor()
    {
        var text = """
            facundo nougues05/07/2026
            HistorialdeldocumentoRegistrodeauditoriadefirmaelectronicadesignNowTodaslasfechasexpresadasenMM/DD/YYYY(US)
            Nombredeldocumento:RESERVASANCHEZBUSTAMANTEDocumentocreado:05/07/202616:18:31Paginasdeldocumento:2
            IDdeldocumento:d2071403494541a9a19068e65315e5bde367440eDocumentoenviado:05/07/202616:23:49UTC
            Estadodeldocumento:Signed05/07/202616:28:34UTCRemitente:felipeabate29@gmail.comFirmantes:facundonougues@gmail.com
            SignNowWebApplicationSehavistoeldocumentofacundonougues@gmail.com05/07/202616:27:10pmUTC186.143.133.196
            SignNowWebApplicationSehafirmadoeldocumentofacundonougues@gmail.com05/07/202616:28:34pmUTC186.143.133.196
            """;

        var quality = DocumentTextQualityEvaluator.Evaluate(DocumentType.ReservationAgreement, text);

        Assert.Equal(TextExtractionQuality.Poor, quality);
        Assert.True(DocumentTextQualityEvaluator.ShouldAttemptOcr(DocumentType.ReservationAgreement, text));
        Assert.True(DocumentTextQualityEvaluator.IsLikelyAuditPage(DocumentType.ReservationAgreement, text));
    }

    [Fact]
    public void Reservation_text_with_expected_keywords_is_partial_or_good()
    {
        var text = """
            Reserva de compraventa de inmueble. Comprador Juan Perez y vendedor Ana Gomez.
            Precio en dolares, escritura, inmobiliaria, dominio y aprobacion crediticia.
            """;

        var quality = DocumentTextQualityEvaluator.Evaluate(DocumentType.ReservationAgreement, text);

        Assert.Contains(quality, new[] { TextExtractionQuality.Partial, TextExtractionQuality.Good });
    }

    [Fact]
    public void Expenses_text_without_expense_keywords_is_poor()
    {
        var text = "archivo generado automaticamente con metadatos de firma y hash";

        var quality = DocumentTextQualityEvaluator.Evaluate(DocumentType.Expenses, text);

        Assert.Equal(TextExtractionQuality.Poor, quality);
    }
}
