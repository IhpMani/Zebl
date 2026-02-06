using System.Text;

namespace Zebl.Api.Services;

/// <summary>
/// Service for parsing HL7 DFT (Detail Financial Transaction) messages
/// Parses MSH, PID, and FT1 segments dynamically
/// </summary>
public class Hl7ParserService
{
    private readonly ILogger<Hl7ParserService> _logger;

    public Hl7ParserService(ILogger<Hl7ParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses an HL7 file stream and extracts DFT^P03 messages by message block.
    /// A message starts at an MSH segment and continues until the next MSH or EOF.
    /// Only messages with MSH-9 == DFT^P03 (or containing DFT^P03) are processed.
    /// </summary>
    public List<Hl7DftMessage> ParseHl7File(Stream fileStream, string fileName)
    {
        var result = new List<Hl7DftMessage>();

        // Read all lines using ASCII encoding (HL7 standard)
        var lines = new List<string>();
        using (var reader = new StreamReader(fileStream, Encoding.ASCII, leaveOpen: true))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Normalize line endings and trim
                line = line.TrimEnd('\r', '\n').Trim();
                if (line.Length == 0)
                    continue;

                lines.Add(line);
            }
        }

        if (lines.Count == 0)
        {
            _logger.LogWarning("HL7 file {FileName} is empty or contains no non-empty lines", fileName);
            return result;
        }

        // Group lines into messages starting at MSH
        var messageLinesList = new List<List<string>>();
        List<string>? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("MSH|", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null && current.Count > 0)
                {
                    messageLinesList.Add(current);
                }
                current = new List<string> { line };
            }
            else
            {
                current?.Add(line);
            }
        }

        if (current != null && current.Count > 0)
        {
            messageLinesList.Add(current);
        }

        if (messageLinesList.Count == 0)
        {
            _logger.LogWarning("No MSH segments found in HL7 file {FileName}", fileName);
            return result;
        }

        // Process each message block independently
        foreach (var messageLines in messageLinesList)
        {
            // Find MSH line
            var mshLine = messageLines.FirstOrDefault(l => l.StartsWith("MSH|", StringComparison.OrdinalIgnoreCase));
            if (mshLine == null)
            {
                continue;
            }

            // Detect message type from MSH-9 (index 8 in zero-based array)
            var fields = mshLine.Split('|');
            var messageTypeField = fields.Length > 8 ? fields[8] : string.Empty;

            // Expect message type like "DFT^P03" or "DFT^P03^DFT_P03"
            if (string.IsNullOrWhiteSpace(messageTypeField) ||
                !messageTypeField.Contains("DFT^P03", StringComparison.OrdinalIgnoreCase))
            {
                // Not a DFT^P03 message; skip this block
                continue;
            }

            var dftMessage = new Hl7DftMessage
            {
                MshSegment = mshLine
            };

            // Per-message state: exactly one PID and PV1; multiple FT1/IN1/GT1 allowed
            foreach (var segment in messageLines)
            {
                if (segment == mshLine)
                    continue;

                if (segment.StartsWith("PID", StringComparison.OrdinalIgnoreCase))
                {
                    // Last PID wins if multiple, but typical DFT has one
                    dftMessage.PidSegment = segment;
                    continue;
                }

                if (segment.StartsWith("PV1", StringComparison.OrdinalIgnoreCase))
                {
                    dftMessage.Pv1Segment = segment;
                    continue;
                }

                if (segment.StartsWith("FT1", StringComparison.OrdinalIgnoreCase))
                {
                    dftMessage.Ft1Segments.Add(segment);
                    continue;
                }

                if (segment.StartsWith("IN1", StringComparison.OrdinalIgnoreCase))
                {
                    dftMessage.In1Segments.Add(segment);
                    continue;
                }

                if (segment.StartsWith("GT1", StringComparison.OrdinalIgnoreCase))
                {
                    dftMessage.Gt1Segments.Add(segment);
                    continue;
                }
            }

            // Only add messages that have at least a PID and one FT1 (minimal for claim + service lines)
            if (!string.IsNullOrWhiteSpace(dftMessage.PidSegment) && dftMessage.Ft1Segments.Count > 0)
            {
                result.Add(dftMessage);
            }
            else
            {
                _logger.LogWarning(
                    "Skipping DFT^P03 message in file {FileName} due to missing PID or FT1 segments (PID present: {HasPid}, FT1 count: {Ft1Count})",
                    fileName,
                    !string.IsNullOrWhiteSpace(dftMessage.PidSegment),
                    dftMessage.Ft1Segments.Count);
            }
        }

        _logger.LogInformation("Parsed {Count} HL7 DFT^P03 messages from file {FileName}", result.Count, fileName);
        return result;
    }

    /// <summary>
    /// Extracts field value from HL7 segment by field index (1-based)
    /// HL7 uses pipe (|) as field separator
    /// </summary>
    public string? GetFieldValue(string segment, int fieldIndex)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return null;

        var fields = segment.Split('|');
        if (fieldIndex < 0 || fieldIndex >= fields.Length)
            return null;

        var value = fields[fieldIndex];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Extracts component value from HL7 field by component index (1-based)
    /// HL7 uses caret (^) as component separator
    /// </summary>
    public string? GetComponentValue(string? fieldValue, int componentIndex)
    {
        if (string.IsNullOrWhiteSpace(fieldValue))
            return null;

        var components = fieldValue.Split('^');
        if (componentIndex < 0 || componentIndex >= components.Length)
            return null;

        var value = components[componentIndex];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// Parses HL7 date format (YYYYMMDD or YYYYMMDDHHMMSS) to DateOnly
    /// </summary>
    public DateOnly? ParseHl7Date(string? dateValue)
    {
        if (string.IsNullOrWhiteSpace(dateValue))
            return null;

        // HL7 dates can be YYYYMMDD or YYYYMMDDHHMMSS
        if (dateValue.Length >= 8)
        {
            var datePart = dateValue.Substring(0, 8);
            if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return DateOnly.FromDateTime(date);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses HL7 date format to DateTime (for timestamps)
    /// </summary>
    public DateTime? ParseHl7DateTime(string? dateValue)
    {
        if (string.IsNullOrWhiteSpace(dateValue))
            return null;

        // Try YYYYMMDDHHMMSS format first
        if (dateValue.Length >= 14)
        {
            var dateTimePart = dateValue.Substring(0, 14);
            if (DateTime.TryParseExact(dateTimePart, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var dateTime))
            {
                return dateTime;
            }
        }

        // Fall back to YYYYMMDD
        if (dateValue.Length >= 8)
        {
            var datePart = dateValue.Substring(0, 8);
            if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses decimal value from HL7 field
    /// </summary>
    public decimal ParseDecimal(string? value, decimal defaultValue = 0m)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return defaultValue;
    }

    /// <summary>
    /// Extracts patient MRN (Medical Record Number) from PID segment
    /// MRN is typically in PID.3 (Patient Identifier List), first component
    /// </summary>
    public string? ExtractPatientMrn(string? pidSegment)
    {
        if (string.IsNullOrWhiteSpace(pidSegment))
            return null;

        // PID.3 is the Patient Identifier List
        var pid3 = GetFieldValue(pidSegment, 3);
        if (string.IsNullOrWhiteSpace(pid3))
            return null;

        // First component is usually the ID number
        var mrn = GetComponentValue(pid3, 0);
        return mrn;
    }

    /// <summary>
    /// Extracts patient name from PID segment
    /// Name is in PID.5 (Patient Name), format: LastName^FirstName^MiddleName^Suffix^Prefix
    /// </summary>
    public (string? FirstName, string? LastName) ExtractPatientName(string? pidSegment)
    {
        if (string.IsNullOrWhiteSpace(pidSegment))
            return (null, null);

        // PID.5 is Patient Name
        var pid5 = GetFieldValue(pidSegment, 5);
        if (string.IsNullOrWhiteSpace(pid5))
            return (null, null);

        // Format: LastName^FirstName^MiddleName^Suffix^Prefix
        var lastName = GetComponentValue(pid5, 0);
        var firstName = GetComponentValue(pid5, 1);

        return (firstName, lastName);
    }

    /// <summary>
    /// Extracts patient birth date from PID segment
    /// Birth date is in PID.7 (Date/Time of Birth)
    /// </summary>
    public DateOnly? ExtractPatientBirthDate(string? pidSegment)
    {
        if (string.IsNullOrWhiteSpace(pidSegment))
            return null;

        // PID.7 is Date/Time of Birth
        var pid7 = GetFieldValue(pidSegment, 7);
        return ParseHl7Date(pid7);
    }

    /// <summary>
    /// Sanitizes HL7 XTN (Extended Telecommunication Number) to digits only
    /// EZClaim behavior: extract digits, truncate to maxLength (default 25)
    /// Format: [NNN] [(999)]999-9999[X99999][B99999][C any text]^PRN^PH^^^
    /// </summary>
    public string? SanitizePhoneNumber(string? xtnValue, int maxLength = 25)
    {
        if (string.IsNullOrWhiteSpace(xtnValue))
            return null;

        // Extract only digits from phone number
        var digitsOnly = new string(xtnValue.Where(char.IsDigit).ToArray());

        if (string.IsNullOrWhiteSpace(digitsOnly))
            return null;

        // Truncate to maxLength (EZClaim phone columns are VARCHAR(25))
        if (digitsOnly.Length > maxLength)
        {
            digitsOnly = digitsOnly.Substring(0, maxLength);
        }

        return digitsOnly;
    }

    /// <summary>
    /// Parses HL7 XAD (Extended Address) and extracts components
    /// Format: Street Address^Other Designation^City^State^Zip^Country^Address Type^...
    /// EZClaim behavior: normalize state to 2-char uppercase, truncate fields
    /// </summary>
    public (string? StreetAddress, string? City, string? State, string? Zip) ParseAddress(string? xadValue)
    {
        if (string.IsNullOrWhiteSpace(xadValue))
            return (null, null, null, null);

        var components = xadValue.Split('^');
        
        // XAD components: 0=Street, 1=Other, 2=City, 3=State, 4=Zip
        var streetAddress = components.Length > 0 ? NormalizeString(components[0], maxLength: 50) : null;
        var city = components.Length > 2 ? NormalizeString(components[2], maxLength: 50) : null;
        var state = components.Length > 3 ? NormalizeStateCode(components[3]) : null;
        var zip = components.Length > 4 ? NormalizeString(components[4], maxLength: 20) : null;

        return (streetAddress, city, state, zip);
    }

    /// <summary>
    /// Normalizes state code to 2-character uppercase (EZClaim standard)
    /// Truncates if longer, pads if shorter (unlikely), returns null if empty
    /// </summary>
    public string? NormalizeStateCode(string? stateValue)
    {
        if (string.IsNullOrWhiteSpace(stateValue))
            return null;

        // Remove whitespace and convert to uppercase
        var normalized = stateValue.Trim().ToUpperInvariant();

        // Extract first 2 characters (state codes are 2 chars in EZClaim)
        if (normalized.Length >= 2)
        {
            return normalized.Substring(0, 2);
        }
        else if (normalized.Length == 1)
        {
            // Single character - pad with space? Or return as-is? EZClaim typically uses 2-char codes
            // Return null to avoid invalid state codes
            return null;
        }

        return null;
    }

    /// <summary>
    /// Normalizes string value: trims whitespace, truncates to maxLength
    /// EZClaim behavior: silently truncate, return null if empty after trim
    /// </summary>
    public string? NormalizeString(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        // Truncate to maxLength (EZClaim behavior: silent truncation)
        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed.Substring(0, maxLength);
        }

        return trimmed;
    }
}

/// <summary>
/// Represents a parsed HL7 DFT message containing patient and financial transaction data
/// </summary>
public class Hl7DftMessage
{
    public string MshSegment { get; set; } = string.Empty;
    public string? PidSegment { get; set; }
    public string? Pv1Segment { get; set; }
    public List<string> Ft1Segments { get; set; } = new List<string>();
    public List<string> In1Segments { get; set; } = new List<string>();
    public List<string> Gt1Segments { get; set; } = new List<string>();
}
