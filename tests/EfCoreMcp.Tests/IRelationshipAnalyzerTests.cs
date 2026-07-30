namespace EfCoreMcp.Tests;

public interface IRelationshipAnalyzerTests : IDisposable
{
    void ExplainRelationship_DirectForeignKey_IsOneHop();
    void ExplainRelationship_TransitivePath_GoesThroughJoinEntity();
    void ExplainRelationship_SameEntity_IsZeroHops();
    void ExplainRelationship_ResolvesCaseInsensitiveAndTableNames();
    void ExplainRelationship_UnknownEntity_ThrowsWithAvailableNames();
    void GetDependencyOrder_PrincipalsComeBeforeDependents();
}
