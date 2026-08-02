namespace EfCoreMcp.Core.Services;

internal static class ModelAnalyzerConstants
{
    // Severity levels
    public const string SeverityWarning = "warning";
    public const string SeverityInfo = "info";
    public const string SeverityError = "error";

    // Finding codes
    public const string CodeEfMcp001 = "EFMCP001";
    public const string CodeEfMcp002 = "EFMCP002";
    public const string CodeEfMcp003 = "EFMCP003";
    public const string CodeEfMcp004 = "EFMCP004";
    public const string CodeEfMcp005 = "EFMCP005";
    public const string CodeEfMcp006 = "EFMCP006";
    public const string CodeEfMcp007 = "EFMCP007";
    public const string CodeEfMcp008 = "EFMCP008";

    // Delete behavior
    public const string DeleteBehaviorCascade = "Cascade";

    // Type names
    public const string ClrTypeString = "String";
    public const string ClrTypeDecimal = "Decimal";
    public const string ClrTypeDecimalNullable = "Decimal?";
}
