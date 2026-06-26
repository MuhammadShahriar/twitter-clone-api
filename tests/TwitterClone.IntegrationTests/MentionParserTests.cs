using TwitterClone.Application.Common;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Unit tests for <see cref="MentionParser.ExtractHandles"/> — the pure regex that pulls <c>@handle</c>
/// tokens out of tweet text. Covers the boundary rule (an email's "@" is not a mention), case-insensitive
/// de-duplication, and the empty/no-mention cases.
/// </summary>
public class MentionParserTests
{
    [Fact]
    public void Extracts_distinct_handles_in_order_of_first_appearance()
    {
        var handles = MentionParser.ExtractHandles("hey @alice @bob @alice, look at this");

        // @alice appears twice but is returned once; order is first-appearance.
        Assert.Equal(new[] { "alice", "bob" }, handles);
    }

    [Fact]
    public void De_duplication_is_case_insensitive()
    {
        var handles = MentionParser.ExtractHandles("@Alice and @alice and @ALICE");

        Assert.Single(handles);
    }

    [Fact]
    public void Does_not_treat_an_email_address_as_a_mention()
    {
        // The '@' in an email is preceded by a word character, so the lookbehind rejects it.
        var handles = MentionParser.ExtractHandles("mail me at alice@example.com please");

        Assert.Empty(handles);
    }

    [Fact]
    public void Matches_at_start_and_after_punctuation_but_not_mid_word()
    {
        var handles = MentionParser.ExtractHandles("@start, mid(@inner) but not foo@nope");

        Assert.Equal(new[] { "start", "inner" }, handles);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("no mentions here")]
    [InlineData("email like a@b is too short to be a real handle but still skipped: x@y")]
    public void Returns_empty_when_there_is_nothing_to_match(string? content)
    {
        Assert.Empty(MentionParser.ExtractHandles(content));
    }
}
