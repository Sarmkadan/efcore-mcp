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
    public const string CodeEfMcp009 = "EFMCP009";
    public const string CodeEfMcp010 = "EFMCP010";
    public const string CodeEfMcp011 = "EFMCP011";
    public const string CodeEfMcp012 = "EFMCP012";
    public const string CodeEfMcp013 = "EFMCP013";

    // Delete behavior
    public const string DeleteBehaviorCascade = "Cascade";

    // Cascade fan-out: distinct dependent entity types reached through database cascades before EFMCP010 fires
    public const int CascadeFanOutThreshold = 3;

    // Inheritance mapping strategies (as reported by EF Core)
    public const string MappingStrategyTph = "TPH";

    // Type names
    public const string ClrTypeString = "String";
    public const string ClrTypeDecimal = "Decimal";
    public const string ClrTypeDecimalNullable = "Decimal?";
}
