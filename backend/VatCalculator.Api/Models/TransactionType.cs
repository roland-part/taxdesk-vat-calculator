namespace VatCalculator.Api.Models;

/// <summary>
/// Classifies an invoice by transaction category, determining which section of the
/// VAT declaration (Form 65 series) it is reported on.
/// </summary>
public enum TransactionType
{
    /// <summary>Regular domestic supply or acquisition. Default when column is absent.</summary>
    Domestic,

    /// <summary>
    /// Tax-free intra-Community supply of goods to another EU member state (§89 VAT Act).
    /// Direction must be Sale. VAT rate should be 0.
    /// Maps to Form 65 line 02.
    /// </summary>
    IntraCommunitySale,

    /// <summary>
    /// Intra-Community acquisition of goods from another EU member state (§§19–20 VAT Act).
    /// Direction must be Purchase. VAT is self-assessed at the applicable domestic rate.
    /// Maps to Form 65 lines 12–14 (output) and line 69 (deductible input).
    /// </summary>
    IntraCommunityAcquisition,

    /// <summary>
    /// Import of goods from outside the EU.
    /// Direction must be Purchase.
    /// Maps to Form 65 lines 23–26 (output) and line 70/71 (deductible input).
    /// </summary>
    Import,

    /// <summary>
    /// Domestic reverse-charge transaction under §142 VAT Act (e.g. construction,
    /// certain metals, agricultural products, gas supply).
    /// Valid for both Sale (seller perspective, line 04) and Purchase (buyer perspective, line 29).
    /// </summary>
    ReverseCharge,
}
