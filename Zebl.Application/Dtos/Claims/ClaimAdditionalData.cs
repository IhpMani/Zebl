using System;
using System.Xml.Serialization;

namespace Zebl.Application.Dtos.Claims
{
    /// <summary>
    /// Strongly-typed model for ClaAdditionalData XML field on Claim.
    /// Serialized/deserialized to/from XML in ClaAdditionalData column.
    /// </summary>
    [XmlRoot("ClaimAdditionalData")]
    public class ClaimAdditionalData
    {
        [XmlElement("CustomTextValue")]
        public string? CustomTextValue { get; set; }

        [XmlElement("CustomCurrencyValue")]
        public decimal? CustomCurrencyValue { get; set; }

        [XmlElement("CustomDateValue")]
        public DateTime? CustomDateValue { get; set; }

        [XmlElement("CustomNumberValue")]
        public decimal? CustomNumberValue { get; set; }

        [XmlElement("CustomTrueFalseValue")]
        public bool CustomTrueFalseValue { get; set; }

        [XmlElement("ExternalId")]
        public string? ExternalId { get; set; }

        [XmlElement("PaymentMatchingKey")]
        public string? PaymentMatchingKey { get; set; }
    }
}
