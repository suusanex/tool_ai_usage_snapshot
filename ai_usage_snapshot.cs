#:property TargetFramework=net10.0
#:package CsvHelper@33.0.1
#:property Nullable=enable
#:property ImplicitUsings=enable

using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

const int ExitCodeSuccess = 0;
const int ExitCodeInvalidArgs = 2;
const int ExitCodeInputFileError = 3;
const int ExitCodeSchemaError = 4;
const int ExitCodeCsvValueError = 5;
const int ExitCodeUnexpected = 10;

var optionsResult = ParseArguments(args);
if (!optionsResult.IsSuccess)
{
    return HandleError(optionsResult.Error!, optionsResult.OutputDirectory);
}

var options = optionsResult.Value;
try
{
    ProcessSnapshot(options);
    return ExitCodeSuccess;
}
catch (SnapshotCliException ex)
{
    return HandleError(ex, options.OutputDirectory);
}
catch (Exception ex)
{
    return HandleError(new SnapshotCliException(ExitCodeUnexpected, "Unexpected error.", ex), options.OutputDirectory);
}

static SnapshotParseResult ParseArguments(string[] args)
{
    if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase) || args.Contains("-h", StringComparer.OrdinalIgnoreCase))
    {
        return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, UsageText()));
    }

    string? inputPath = null;
    string? outputDir = null;
    DateTimeOffset asOf = DateTimeOffset.UtcNow;

    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (string.Equals(arg, "--input", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, "Missing value for --input"));
            }
            inputPath = args[++i];
            continue;
        }

        if (string.Equals(arg, "--output", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, "Missing value for --output"));
            }
            outputDir = args[++i];
            continue;
        }

        if (string.Equals(arg, "--as-of", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, "Missing value for --as-of"));
            }

            if (!DateTimeOffset.TryParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var asOfValue))
            {
                return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, "Invalid --as-of format. Use YYYY-MM-DD."));
            }

            asOf = asOfValue;
            continue;
        }

        return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, $"Unknown argument: {arg}\n\n{UsageText()}"));
    }

    if (string.IsNullOrWhiteSpace(inputPath))
    {
        return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, "Missing --input."));
    }

    if (string.IsNullOrWhiteSpace(outputDir))
    {
        return new SnapshotParseResult(new SnapshotCliException(ExitCodeInvalidArgs, "Missing --output."));
    }

    return new SnapshotParseResult(new SnapshotExecutionOptions(inputPath, outputDir, asOf));
}

static void ProcessSnapshot(SnapshotExecutionOptions options)
{
    if (!File.Exists(options.InputCsvPath))
    {
        throw new SnapshotCliException(ExitCodeInputFileError, $"Input file not found: {options.InputCsvPath}");
    }

    Directory.CreateDirectory(options.OutputDirectory);

    var records = ReadRecords(options.InputCsvPath);

    var userAggrs = new Dictionary<string, UserAggregation>(StringComparer.OrdinalIgnoreCase);
    var departmentAggrs = new Dictionary<string, DepartmentAggregation>(StringComparer.OrdinalIgnoreCase);
    var serviceAggrs = new Dictionary<string, ServiceAggregation>(StringComparer.OrdinalIgnoreCase);
    var dormantCandidates = new List<CandidateRow>();
    var unusedCandidates = new List<CandidateRow>();
    var qualityRows = new List<QualityRow>();
    var qualityTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    foreach (var record in records)
    {
        var resolvedUserKey = ResolveUserKey(record.UserKey, record.UserEmail, record.RowNumber);
        var serviceKey = NormalizeValue(record.Service);

        var qualityFlags = BuildQualityFlags(record, options.AsOf).ToList();
        if (qualityFlags.Count > 0)
        {
            qualityRows.Add(new QualityRow(record.RowNumber, resolvedUserKey, record.Department, serviceKey, qualityFlags));
            foreach (var flag in qualityFlags)
            {
                qualityTotals.TryAdd(flag, 0);
                qualityTotals[flag]++;
            }
        }

        var user = GetOrAdd(userAggrs, resolvedUserKey, _ => new UserAggregation
        {
            UserKey = resolvedUserKey,
            UserEmail = record.UserEmail,
            DisplayName = record.DisplayName,
            Department = record.Department
        });
        user.Services.Add(serviceKey);
        if (record.LicenseStatus == LicenseStatus.Licensed) user.LicensedServices.Add(serviceKey);
        if (record.LicenseStatus == LicenseStatus.Unlicensed) user.UnlicensedServices.Add(serviceKey);
        if (record.LicenseStatus == LicenseStatus.Unknown) user.UnknownLicenseServices.Add(serviceKey);

        if (record.Active == Tristate.True) user.ActiveServices.Add(serviceKey);
        if (record.Active == Tristate.False) user.InactiveServices.Add(serviceKey);

        if (record.EventCount.IsKnown) user.EventCountTotal += record.EventCount.Value;
        else user.EventCountUnknownRows++;

        if (record.LastActivityAt.IsKnown && (!user.LastActivityAt.IsKnown || record.LastActivityAt.Value > user.LastActivityAt.Value))
        {
            user.LastActivityAt = record.LastActivityAt;
        }

        var department = NormalizeValue(record.Department);
        var dept = GetOrAdd(departmentAggrs, department, _ => new DepartmentAggregation());
        dept.Users.Add(resolvedUserKey);
        dept.RecordCount++;
        dept.EventCountTotal += record.EventCount.IsKnown ? record.EventCount.Value : 0;
        dept.EventCountUnknownRows += record.EventCount.IsKnown ? 0 : 1;
        if (record.LicenseStatus == LicenseStatus.Licensed)
        {
            dept.LicensedUsers.Add(resolvedUserKey);
        }

        if (record.Active == Tristate.True)
        {
            dept.ActiveUsers.Add(resolvedUserKey);
        }

        if (record.Active == Tristate.False)
        {
            dept.InactiveUsers.Add(resolvedUserKey);
        }

        var service = GetOrAdd(serviceAggrs, serviceKey, _ => new ServiceAggregation { Service = serviceKey });
        service.UserCountKeys.Add(resolvedUserKey);
        service.EventCountTotal += record.EventCount.IsKnown ? record.EventCount.Value : 0;
        service.RecordCount++;
        if (record.LicenseStatus == LicenseStatus.Licensed)
        {
            service.LicensedUsers.Add(resolvedUserKey);
        }
        if (record.Active == Tristate.True)
        {
            service.ActiveUsers.Add(resolvedUserKey);
        }
        if (record.Active == Tristate.False)
        {
            service.InactiveUsers.Add(resolvedUserKey);
        }

        var (dormantReason, dormantConfidence, isDormant) = EvaluateDormantCandidate(record, options.AsOf);
        if (isDormant)
        {
            if (record.UserEmail is not null || !string.IsNullOrWhiteSpace(record.UserKey))
            {
                var entry = new CandidateRow(
                    resolvedUserKey,
                    record.UserEmail,
                    record.DisplayName,
                    record.Department,
                    serviceKey,
                    dormantReason,
                    dormantConfidence);
                dormantCandidates.Add(entry);
                user.DormantCandidateCount++;
            }
        }

        var (unusedReason, unusedConfidence, isUnused) = EvaluateUnusedCandidate(record);
        if (isUnused)
        {
            if (record.UserEmail is not null || !string.IsNullOrWhiteSpace(record.UserKey))
            {
                var entry = new CandidateRow(
                    resolvedUserKey,
                    record.UserEmail,
                    record.DisplayName,
                    record.Department,
                    serviceKey,
                    unusedReason,
                    unusedConfidence);
                unusedCandidates.Add(entry);
                user.LicenseUnusedCandidateCount++;
            }
        }
    }

    WriteSummaries(
        records.Count,
        userAggrs,
        departmentAggrs,
        serviceAggrs,
        dormantCandidates,
        unusedCandidates,
        qualityRows,
        qualityTotals,
        options);
}

static (string Reason, string Confidence, bool IsMatch) EvaluateDormantCandidate(UsageRecord record, DateTimeOffset asOf)
{
    if (record.LicenseStatus != LicenseStatus.Licensed)
    {
        return ("", "", false);
    }

    if (record.Active == Tristate.False)
    {
        return ("active_false", "high", true);
    }

    if (record.LastActivityAt.IsKnown && record.LastActivityAt.Value <= asOf.AddDays(-90))
    {
        return ("inactive_90_days_or_more", "medium", true);
    }

    return ("", "", false);
}

static (string Reason, string Confidence, bool IsMatch) EvaluateUnusedCandidate(UsageRecord record)
{
    if (record.LicenseStatus != LicenseStatus.Licensed)
    {
        return ("", "", false);
    }

    if (record.Active == Tristate.False &&
        record.EventCount.IsKnown &&
        record.EventCount.Value == 0)
    {
        return ("inactive_and_no_events", "high", true);
    }

    if (record.Active == Tristate.Unknown &&
        record.EventCount.IsKnown &&
        record.EventCount.Value == 0)
    {
        return ("event_count_zero_with_unknown_activity", "low", true);
    }

    return ("", "", false);
}

static void WriteSummaries(
    int totalRows,
    Dictionary<string, UserAggregation> userAggrs,
    Dictionary<string, DepartmentAggregation> departmentAggrs,
    Dictionary<string, ServiceAggregation> serviceAggrs,
    List<CandidateRow> dormantCandidates,
    List<CandidateRow> unusedCandidates,
    List<QualityRow> qualityRows,
    Dictionary<string, int> qualityTotals,
    SnapshotExecutionOptions options)
{
    var userRows = userAggrs.Values
        .Select(x => new UserSummaryRow
        {
            UserKey = x.UserKey,
            UserEmail = x.UserEmail,
            DisplayName = x.DisplayName,
            Department = x.Department,
            ServiceCount = x.Services.Count,
            LicensedServiceCount = x.LicensedServices.Count,
            ActiveServiceCount = x.ActiveServices.Count,
            InactiveServiceCount = x.InactiveServices.Count,
            EventCountTotal = x.EventCountTotal,
            EventCountUnknownRows = x.EventCountUnknownRows,
            LastActivityAtLatest = x.LastActivityAt.IsKnown ? x.LastActivityAt.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : "unknown",
            DormantCandidateCount = x.DormantCandidateCount,
            LicenseUnusedCandidateCount = x.LicenseUnusedCandidateCount
        })
        .OrderBy(x => x.UserKey, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var departmentRows = departmentAggrs
        .Select(x => new DepartmentSummaryRow
        {
            Department = x.Key,
            UserCount = x.Value.Users.Count,
            LicensedUserCount = x.Value.LicensedUsers.Count,
            ActiveUserCount = x.Value.ActiveUsers.Count,
            InactiveUserCount = x.Value.InactiveUsers.Count,
            EventCountTotal = x.Value.EventCountTotal,
            EventCountUnknownRows = x.Value.EventCountUnknownRows,
            RecordCount = x.Value.RecordCount
        })
        .OrderBy(x => x.Department, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var serviceRows = serviceAggrs
        .Select(x => new ServiceSummaryRow
        {
            Service = x.Key,
            UserCount = x.Value.UserCountKeys.Count,
            LicensedUserCount = x.Value.LicensedUsers.Count,
            ActiveUserCount = x.Value.ActiveUsers.Count,
            InactiveUserCount = x.Value.InactiveUsers.Count,
            EventCountTotal = x.Value.EventCountTotal,
            RecordCount = x.Value.RecordCount
        })
        .OrderBy(x => x.Service, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var candidateRows = dormantCandidates
        .DistinctBy(x => new { x.UserKey, x.Service, x.Reason })
        .OrderBy(x => x.UserKey, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.Service, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var unusedRows = unusedCandidates
        .DistinctBy(x => new { x.UserKey, x.Service, x.Reason })
        .OrderBy(x => x.UserKey, StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.Service, StringComparer.OrdinalIgnoreCase)
        .ToList();

    WriteCsv(Path.Combine(options.OutputDirectory, "user-summary.csv"), userRows, x => x);
    WriteCsv(Path.Combine(options.OutputDirectory, "department-summary.csv"), departmentRows, x => x);
    WriteCsv(Path.Combine(options.OutputDirectory, "service-summary.csv"), serviceRows, x => x);
    WriteCsv(Path.Combine(options.OutputDirectory, "dormant-candidates.csv"), candidateRows, x => x);
    WriteCsv(Path.Combine(options.OutputDirectory, "license-unused-candidates.csv"), unusedRows, x => x);
    WriteQualityReport(totalRows, qualityRows, qualityTotals, options);
}

static void WriteCsv<T>(string path, IEnumerable<T> records, Func<T, T> selector)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
    csv.WriteRecords(records.Select(selector));
}

static void WriteQualityReport(
    int totalRows,
    List<QualityRow> qualityRows,
    Dictionary<string, int> qualityTotals,
    SnapshotExecutionOptions options)
{
    var sb = new StringBuilder();
    sb.AppendLine("# Data Quality Report");
    sb.AppendLine();
    sb.AppendLine($"GeneratedAt: {DateTimeOffset.UtcNow:O}");
    sb.AppendLine($"AsOf: {options.AsOf:O}");
    sb.AppendLine($"InputFile: {options.InputCsvPath}");
    sb.AppendLine($"OutputDirectory: {options.OutputDirectory}");
    sb.AppendLine();
    sb.AppendLine($"TotalRows: {totalRows}");
    sb.AppendLine($"RowsWithQualityFlags: {qualityRows.Count}");
    sb.AppendLine();
    sb.AppendLine("## Quality Flag Count");
    sb.AppendLine();
    foreach (var kv in qualityTotals.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
    {
        sb.AppendLine($"- {kv.Key}: {kv.Value}");
    }
    if (qualityRows.Count == 0)
    {
        sb.AppendLine();
        sb.AppendLine("No quality issues found.");
    }
    else
    {
        sb.AppendLine();
        sb.AppendLine("## Row sample");
        sb.AppendLine();
        sb.AppendLine("| Row | User | Department | Service | Flags |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var row in qualityRows.OrderBy(x => x.RowNumber).Take(30))
        {
            sb.AppendLine($"| {row.RowNumber} | {EscapeTableCell(row.UserKey)} | {EscapeTableCell(row.Department)} | {EscapeTableCell(row.Service)} | {EscapeTableCell(string.Join(", ", row.Flags))} |");
        }
    }

    File.WriteAllText(
        Path.Combine(options.OutputDirectory, "data-quality-report.md"),
        sb.ToString(),
        new UTF8Encoding(false));
}

static string EscapeTableCell(string value)
{
    return value.Replace("|", "\\|");
}

static IEnumerable<string> BuildQualityFlags(UsageRecord record, DateTimeOffset asOf)
{
    if (string.IsNullOrWhiteSpace(record.UserKey))
    {
        yield return "missing_user_key";
    }

    if (string.IsNullOrWhiteSpace(record.Service) || string.Equals(record.Service, "unknown", StringComparison.OrdinalIgnoreCase))
    {
        yield return "missing_service";
    }

    if (record.Active == Tristate.Unknown || !record.LastActivityAt.IsKnown)
    {
        yield return "unknown_activity";
    }

    if (record.CollectionMethod == CollectionMethod.ManualAggregate)
    {
        yield return "manual_aggregate";
    }

    if (record.SourceConfidence == SourceConfidence.Low)
    {
        yield return "low_confidence";
    }

    if (record.ImportedAt.IsKnown && record.ImportedAt.Value <= asOf.AddDays(-90))
    {
        yield return "old_data";
    }

    if (record.Notes.Length >= 200 || record.Notes.IndexOf('\n') >= 0 || record.Notes.IndexOf('\r') >= 0)
    {
        yield return "body_data_risk";
    }
}

static string ResolveUserKey(string? userKey, string? userEmail, int rowNumber)
{
    if (!string.IsNullOrWhiteSpace(userKey))
    {
        return userKey.Trim();
    }

    if (!string.IsNullOrWhiteSpace(userEmail))
    {
        return $"email:{userEmail.Trim()}";
    }

    return $"unknown-user:{rowNumber}";
}

static string NormalizeValue(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return "unknown";
    }

    return value.Trim();
}

static List<UsageRecord> ReadRecords(string inputCsvPath)
{
    var requiredHeaders = new[]
    {
        "period_start",
        "period_end",
        "user_key",
        "user_email",
        "display_name",
        "department",
        "service",
        "license_status",
        "active",
        "event_count",
        "event_unit",
        "last_activity_at",
        "collection_method",
        "source_confidence",
        "imported_at",
        "notes"
    };

    using var reader = new StreamReader(inputCsvPath);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        TrimOptions = TrimOptions.Trim,
        MissingFieldFound = null
    });

    if (!csv.Read() || !csv.ReadHeader())
    {
        throw new SnapshotCliException(ExitCodeSchemaError, "CSV header is missing.");
    }

    var headers = csv.HeaderRecord ?? Array.Empty<string>();
    var missingHeaders = requiredHeaders.Where(h => !headers.Contains(h, StringComparer.OrdinalIgnoreCase)).ToList();
    if (missingHeaders.Count > 0)
    {
        throw new SnapshotCliException(
            ExitCodeSchemaError,
            $"Missing required headers: {string.Join(", ", missingHeaders)}");
    }

    var records = new List<UsageRecord>();
    while (csv.Read())
    {
        var raw = ReadRawRow(csv, headers);
        var rowNumber = csv.Context.Parser?.Row is int row ? Math.Max(1, row - 2) : records.Count + 1;
        var record = ParseRecord(raw, rowNumber);
        records.Add(record);
    }

    return records;
}

static Dictionary<string, string> ReadRawRow(CsvReader csv, string[] headers)
{
    var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var header in headers)
    {
        row[header] = csv.GetField(header) ?? string.Empty;
    }
    return row;
}

static UsageRecord ParseRecord(Dictionary<string, string> raw, int rowNumber)
{
    var service = GetField(raw, "service");
    var periodStart = ParseDateOrUnknown(GetField(raw, "period_start"), rowNumber, "period_start");
    var periodEnd = ParseDateOrUnknown(GetField(raw, "period_end"), rowNumber, "period_end");
    var licenseStatus = ParseLicenseStatus(GetField(raw, "license_status"), rowNumber);
    var active = ParseActive(GetField(raw, "active"), rowNumber);
    var eventCount = ParseKnownInt(GetField(raw, "event_count"), rowNumber);
    var lastActivityAt = ParseDateOrUnknown(GetField(raw, "last_activity_at"), rowNumber, "last_activity_at");
    var importedAt = ParseDateOrUnknown(GetField(raw, "imported_at"), rowNumber, "imported_at");

    return new UsageRecord(
        rowNumber,
        periodStart,
        periodEnd,
        NormalizeValue(GetField(raw, "user_key")) == "unknown" ? null : NormalizeValue(GetField(raw, "user_key")),
        NormalizeValue(GetField(raw, "user_email")),
        NormalizeValue(GetField(raw, "display_name")),
        NormalizeValue(GetField(raw, "department")),
        NormalizeValue(service),
        licenseStatus,
        active,
        eventCount,
        NormalizeValue(GetField(raw, "event_unit")),
        lastActivityAt,
        ParseCollectionMethod(GetField(raw, "collection_method"), rowNumber),
        ParseSourceConfidence(GetField(raw, "source_confidence"), rowNumber),
        importedAt,
        NormalizeValue(GetField(raw, "notes")));
}

static string GetField(Dictionary<string, string> raw, string header)
{
    return raw.TryGetValue(header, out var value) ? value : string.Empty;
}

static DateTimeOffset? GetNullableDate(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    if (string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)) return null;
    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt)) return dt;
    return null;
}

static KnownValue<DateTimeOffset> ParseDateOrUnknown(string? value, int rowNumber, string fieldName)
{
    var parsed = GetNullableDate(value);
    if (parsed.HasValue)
    {
        return KnownValue<DateTimeOffset>.Known(parsed.Value);
    }

    if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
    {
        return KnownValue<DateTimeOffset>.Unknown();
    }

    throw new SnapshotCliException(
        ExitCodeCsvValueError,
        $"Row {rowNumber}: invalid date value in {fieldName}: '{value}'.");
}

static LicenseStatus ParseLicenseStatus(string? value, int rowNumber)
{
    if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
    {
        return LicenseStatus.Unknown;
    }

    return value.Trim().ToLowerInvariant() switch
    {
        "licensed" => LicenseStatus.Licensed,
        "unlicensed" => LicenseStatus.Unlicensed,
        _ => throw new SnapshotCliException(
            ExitCodeCsvValueError,
            $"Row {rowNumber}: invalid license_status: '{value}'."),
    };
}

static Tristate ParseActive(string? value, int rowNumber)
{
    if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
    {
        return Tristate.Unknown;
    }

    if (bool.TryParse(value, out var parsed))
    {
        return parsed ? Tristate.True : Tristate.False;
    }

    return value.Trim().ToLowerInvariant() switch
    {
        "1" => Tristate.True,
        "0" => Tristate.False,
        "yes" => Tristate.True,
        "no" => Tristate.False,
        _ => throw new SnapshotCliException(
            ExitCodeCsvValueError,
            $"Row {rowNumber}: invalid active: '{value}'."),
    };
}

static KnownValue<int> ParseKnownInt(string? value, int rowNumber)
{
    if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
    {
        return KnownValue<int>.Unknown();
    }

    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
    {
        return KnownValue<int>.Known(parsed);
    }

    throw new SnapshotCliException(
        ExitCodeCsvValueError,
        $"Row {rowNumber}: invalid event_count: '{value}'.");
}

static CollectionMethod ParseCollectionMethod(string? value, int rowNumber)
{
    if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
    {
        return CollectionMethod.Unknown;
    }

    return value.Trim().ToLowerInvariant() switch
    {
        "manual_csv" or "manual-csv" => CollectionMethod.ManualCsv,
        "manual_aggregate" or "manual-aggregate" => CollectionMethod.ManualAggregate,
        _ => throw new SnapshotCliException(
            ExitCodeCsvValueError,
            $"Row {rowNumber}: invalid collection_method: '{value}'."),
    };
}

static SourceConfidence ParseSourceConfidence(string? value, int rowNumber)
{
    if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
    {
        return SourceConfidence.Unknown;
    }

    return value.Trim().ToLowerInvariant() switch
    {
        "high" => SourceConfidence.High,
        "medium" => SourceConfidence.Medium,
        "low" => SourceConfidence.Low,
        _ => throw new SnapshotCliException(
            ExitCodeCsvValueError,
            $"Row {rowNumber}: invalid source_confidence: '{value}'."),
    };
}

static int HandleError(Exception exception, string outputDirectory)
{
    var snapshotException = exception is SnapshotCliException cliException
        ? cliException
        : new SnapshotCliException(ExitCodeUnexpected, "Unexpected error.", exception);

    try
    {
        var logPath = Path.Combine(outputDirectory, "trace.log");
        Directory.CreateDirectory(outputDirectory);
        File.AppendAllText(logPath, snapshotException.ToString(), new UTF8Encoding(false));
        File.AppendAllText(logPath, Environment.NewLine, new UTF8Encoding(false));
    }
    catch (Exception loggingException)
    {
        Console.Error.WriteLine(loggingException.ToString());
    }

    Console.Error.WriteLine(snapshotException.Message);
    return snapshotException.ExitCode;
}

static string UsageText()
{
    return @"Usage:
dotnet run --file ai_usage_snapshot.cs -- --input <input.csv> --output <out-dir> [--as-of <yyyy-MM-dd>]

Required:
  --input   Path to manual CSV using common schema
  --output  Output directory for generated summaries

Optional:
  --as-of   Base date for dormant evaluation (YYYY-MM-DD)
";
}

static T GetOrAdd<T>(Dictionary<string, T> dic, string key, Func<string, T> create)
{
    if (dic.TryGetValue(key, out var value))
    {
        return value;
    }

    var created = create(key);
    dic[key] = created;
    return created;
}

public readonly record struct KnownValue<T>(bool IsKnown, T Value) where T : struct
{
    public static KnownValue<T> Known(T value) => new(true, value);
    public static KnownValue<T> Unknown() => new(false, default);
}

public enum Tristate
{
    Unknown,
    True,
    False
}

public enum LicenseStatus
{
    Unknown,
    Licensed,
    Unlicensed
}

public enum CollectionMethod
{
    Unknown,
    ManualCsv,
    ManualAggregate
}

public enum SourceConfidence
{
    Unknown,
    High,
    Medium,
    Low
}

public sealed record SnapshotExecutionOptions(
    string InputCsvPath,
    string OutputDirectory,
    DateTimeOffset AsOf);

public sealed class SnapshotParseResult
{
    public SnapshotParseResult(SnapshotExecutionOptions options)
    {
        Value = options;
        IsSuccess = true;
    }

    public SnapshotParseResult(SnapshotCliException error)
    {
        Error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }
    public SnapshotExecutionOptions Value { get; } = null!;
    public SnapshotCliException? Error { get; }
    public string OutputDirectory => Value?.OutputDirectory ?? string.Empty;
}

public sealed class SnapshotCliException : Exception
{
    public SnapshotCliException(int exitCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}

public sealed record UsageRecord(
    int RowNumber,
    KnownValue<DateTimeOffset> PeriodStart,
    KnownValue<DateTimeOffset> PeriodEnd,
    string? UserKey,
    string? UserEmail,
    string? DisplayName,
    string Department,
    string Service,
    LicenseStatus LicenseStatus,
    Tristate Active,
    KnownValue<int> EventCount,
    string EventUnit,
    KnownValue<DateTimeOffset> LastActivityAt,
    CollectionMethod CollectionMethod,
    SourceConfidence SourceConfidence,
    KnownValue<DateTimeOffset> ImportedAt,
    string Notes);

public sealed record UserAggregation
{
    public string UserKey { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? DisplayName { get; set; }
    public string Department { get; set; } = string.Empty;
    public HashSet<string> Services { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LicensedServices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UnlicensedServices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UnknownLicenseServices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ActiveServices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> InactiveServices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int EventCountTotal { get; set; }
    public int EventCountUnknownRows { get; set; }
    public KnownValue<DateTimeOffset> LastActivityAt { get; set; } = KnownValue<DateTimeOffset>.Unknown();
    public int DormantCandidateCount { get; set; }
    public int LicenseUnusedCandidateCount { get; set; }
}

public sealed record DepartmentAggregation
{
    public HashSet<string> Users { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LicensedUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ActiveUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> InactiveUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int EventCountTotal { get; set; }
    public int EventCountUnknownRows { get; set; }
    public int RecordCount { get; set; }
}

public sealed record ServiceAggregation
{
    public string Service { get; init; } = string.Empty;
    public HashSet<string> UserCountKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> LicensedUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ActiveUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> InactiveUsers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int EventCountTotal { get; set; }
    public int RecordCount { get; set; }
}

public sealed record CandidateRow(
    string UserKey,
    string? UserEmail,
    string? DisplayName,
    string Department,
    string Service,
    string Reason,
    string Confidence);

public sealed record UserSummaryRow
{
    public string UserKey { get; init; } = string.Empty;
    public string? UserEmail { get; init; }
    public string? DisplayName { get; init; }
    public string Department { get; init; } = string.Empty;
    public int ServiceCount { get; init; }
    public int LicensedServiceCount { get; init; }
    public int ActiveServiceCount { get; init; }
    public int InactiveServiceCount { get; init; }
    public int EventCountTotal { get; init; }
    public int EventCountUnknownRows { get; init; }
    public string LastActivityAtLatest { get; init; } = "unknown";
    public int DormantCandidateCount { get; init; }
    public int LicenseUnusedCandidateCount { get; init; }
}

public sealed record DepartmentSummaryRow
{
    public string Department { get; init; } = string.Empty;
    public int UserCount { get; init; }
    public int LicensedUserCount { get; init; }
    public int ActiveUserCount { get; init; }
    public int InactiveUserCount { get; init; }
    public int EventCountTotal { get; init; }
    public int EventCountUnknownRows { get; init; }
    public int RecordCount { get; init; }
}

public sealed record ServiceSummaryRow
{
    public string Service { get; init; } = string.Empty;
    public int UserCount { get; init; }
    public int LicensedUserCount { get; init; }
    public int ActiveUserCount { get; init; }
    public int InactiveUserCount { get; init; }
    public int EventCountTotal { get; init; }
    public int RecordCount { get; init; }
}

public sealed record QualityRow(
    int RowNumber,
    string UserKey,
    string Department,
    string Service,
    List<string> Flags);
