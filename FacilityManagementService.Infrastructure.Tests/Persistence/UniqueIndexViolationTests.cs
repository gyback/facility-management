using FacilityManagementService.Infrastructure.Persistence;

namespace FacilityManagementService.Infrastructure.Tests.Persistence;

public sealed class UniqueIndexViolationTests
{
    [Theory]
    [InlineData(
        "Cannot insert duplicate key row in object 'dbo.Notes' with unique index 'IX_Notes_Text'. The duplicate key value is (Sauna).",
        "IX_Notes_Text")]
    [InlineData(
        "Violation of UNIQUE KEY constraint 'UQ_Notes_Text'. Cannot insert duplicate key in object 'dbo.Notes'. The duplicate key value is (Sauna).",
        "UQ_Notes_Text")]
    [InlineData(
        "Violation of PRIMARY KEY constraint 'PK_Notes'. Cannot insert duplicate key in object 'dbo.Notes'. The duplicate key value is (1).",
        "PK_Notes")]
    public void The_index_name_is_parsed_from_the_message(string message, string indexName) =>
        Assert.Equal(indexName, UniqueIndexViolation.ParseIndexName(message));

    [Fact]
    public void A_message_without_an_index_name_gives_null() =>
        Assert.Null(UniqueIndexViolation.ParseIndexName("Deadlock victim, rerun the transaction."));
}
