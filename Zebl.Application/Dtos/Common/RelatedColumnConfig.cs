using System.Collections.Generic;

namespace Zebl.Application.Dtos.Common
{
    /// <summary>
    /// Configuration for available related table columns that can be added to list views
    /// </summary>
    public static class RelatedColumnConfig
    {
        public static Dictionary<string, List<RelatedColumnDefinition>> GetAvailableColumns()
        {
            return new Dictionary<string, List<RelatedColumnDefinition>>
            {
                ["Claim"] = new List<RelatedColumnDefinition>
                {
                    // Patient columns
                    new RelatedColumnDefinition { Table = "Patient", Key = "patFirstName", Label = "Patient First Name", Path = "ClaPatF.PatFirstName" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patLastName", Label = "Patient Last Name", Path = "ClaPatF.PatLastName" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patFullNameCC", Label = "Patient Full Name", Path = "ClaPatF.PatFullNameCC" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patAccountNo", Label = "Patient Account No", Path = "ClaPatF.PatAccountNo" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patPhoneNo", Label = "Patient Phone", Path = "ClaPatF.PatPhoneNo" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patCity", Label = "Patient City", Path = "ClaPatF.PatCity" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patState", Label = "Patient State", Path = "ClaPatF.PatState" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patBirthDate", Label = "Patient Birth Date", Path = "ClaPatF.PatBirthDate" },
                    // Rendering Physician columns
                    new RelatedColumnDefinition { Table = "Physician", Key = "renderingPhyName", Label = "Rendering Physician", Path = "ClaRenderingPhyF.PhyName" },
                    new RelatedColumnDefinition { Table = "Physician", Key = "renderingPhyNPI", Label = "Rendering Physician NPI", Path = "ClaRenderingPhyF.PhyNPI" },
                    // Billing Physician columns
                    new RelatedColumnDefinition { Table = "Physician", Key = "billingPhyName", Label = "Billing Physician", Path = "ClaBillingPhyF.PhyName" },
                    new RelatedColumnDefinition { Table = "Physician", Key = "billingPhyNPI", Label = "Billing Physician NPI", Path = "ClaBillingPhyF.PhyNPI" },
                },
                ["Patient"] = new List<RelatedColumnDefinition>
                {
                    // No direct foreign keys to other main tables in Patient
                },
                ["Service"] = new List<RelatedColumnDefinition>
                {
                    // Claim columns
                    new RelatedColumnDefinition { Table = "Claim", Key = "claStatus", Label = "Claim Status", Path = "SrvClaF.ClaStatus" },
                    new RelatedColumnDefinition { Table = "Claim", Key = "claDateTimeCreated", Label = "Claim Date Created", Path = "SrvClaF.ClaDateTimeCreated" },
                    // Patient columns (through Claim)
                    new RelatedColumnDefinition { Table = "Patient", Key = "patFirstName", Label = "Patient First Name", Path = "SrvClaF.ClaPatF.PatFirstName" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patLastName", Label = "Patient Last Name", Path = "SrvClaF.ClaPatF.PatLastName" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patFullNameCC", Label = "Patient Full Name", Path = "SrvClaF.ClaPatF.PatFullNameCC" },
                },
                ["Payment"] = new List<RelatedColumnDefinition>
                {
                    // Patient columns
                    new RelatedColumnDefinition { Table = "Patient", Key = "patFirstName", Label = "Patient First Name", Path = "PmtPatF.PatFirstName" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patLastName", Label = "Patient Last Name", Path = "PmtPatF.PatLastName" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patFullNameCC", Label = "Patient Full Name", Path = "PmtPatF.PatFullNameCC" },
                    new RelatedColumnDefinition { Table = "Patient", Key = "patAccountNo", Label = "Patient Account No", Path = "PmtPatF.PatAccountNo" },
                },
                ["Adjustment"] = new List<RelatedColumnDefinition>
                {
                    // Service columns
                    new RelatedColumnDefinition { Table = "Service", Key = "srvProcedureCode", Label = "Service Procedure Code", Path = "AdjSrvF.SrvProcedureCode" },
                    new RelatedColumnDefinition { Table = "Service", Key = "srvDesc", Label = "Service Description", Path = "AdjSrvF.SrvDesc" },
                    // Payment columns
                    new RelatedColumnDefinition { Table = "Payment", Key = "pmtAmount", Label = "Payment Amount", Path = "AdjPmtF.PmtAmount" },
                    new RelatedColumnDefinition { Table = "Payment", Key = "pmtDateTimeCreated", Label = "Payment Date", Path = "AdjPmtF.PmtDateTimeCreated" },
                    // Payer columns
                    new RelatedColumnDefinition { Table = "Payer", Key = "payName", Label = "Payer Name", Path = "AdjPayF.PayName" },
                },
                ["Payer"] = new List<RelatedColumnDefinition>
                {
                    // No direct foreign keys to other main tables
                },
                ["Physician"] = new List<RelatedColumnDefinition>
                {
                    // No direct foreign keys to other main tables
                },
                ["Disbursement"] = new List<RelatedColumnDefinition>
                {
                    // Payment columns
                    new RelatedColumnDefinition { Table = "Payment", Key = "pmtAmount", Label = "Payment Amount", Path = "DisbPmtF.PmtAmount" },
                    new RelatedColumnDefinition { Table = "Payment", Key = "pmtDateTimeCreated", Label = "Payment Date", Path = "DisbPmtF.PmtDateTimeCreated" },
                    // Service columns
                    new RelatedColumnDefinition { Table = "Service", Key = "srvProcedureCode", Label = "Service Procedure Code", Path = "DisbSrvF.SrvProcedureCode" },
                    new RelatedColumnDefinition { Table = "Service", Key = "srvDesc", Label = "Service Description", Path = "DisbSrvF.SrvDesc" },
                }
            };
        }
    }

    public class RelatedColumnDefinition
    {
        public string Table { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty; // EF navigation path like "ClaPatF.PatFirstName"
    }
}
