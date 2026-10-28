using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Nocturne.API.Tests.Integration.Infrastructure;

public class ResponseComparerIobToleranceTests
{
    private static readonly ResponseComparer Tolerant =
        new(ComparisonOptions.Default.WithIobTolerance());

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static async Task<bool> MatchesAsync(ResponseComparer comparer, string expected, string actual) =>
        (await comparer.CompareAsync(Json(expected), Json(actual))).IsMatch;

    [Theory]
    [InlineData("""{"state":{"iob":3.89}}""", """{"state":{"iob":3.88}}""")]
    [InlineData("""{"state":{"bwp":-3.7}}""", """{"state":{"bwp":-3.69}}""")]
    [InlineData("""{"iob":{"display":"3.89"}}""", """{"iob":{"display":"3.88"}}""")]
    [InlineData("""{"iob":{"displayLine":"IOB: 3.89U"}}""", """{"iob":{"displayLine":"IOB: 3.88U"}}""")]
    public async Task A_difference_of_one_hundredth_on_an_IOB_path_matches(string expected, string actual)
    {
        (await MatchesAsync(Tolerant, expected, actual)).Should().BeTrue();
        (await MatchesAsync(Tolerant, actual, expected)).Should().BeTrue();
    }

    [Theory]
    [InlineData("""{"state":{"iob":3.89}}""", """{"state":{"iob":3.87}}""")]
    [InlineData("""{"state":{"bwp":-3.7}}""", """{"state":{"bwp":-3.68}}""")]
    [InlineData("""{"iob":{"display":"3.89"}}""", """{"iob":{"display":"3.87"}}""")]
    [InlineData("""{"iob":{"displayLine":"IOB: 3.89U"}}""", """{"iob":{"displayLine":"IOB: 3.87U"}}""")]
    public async Task A_difference_above_one_hundredth_on_an_IOB_path_does_not_match(string expected, string actual)
    {
        (await MatchesAsync(Tolerant, expected, actual)).Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"state":{"cob":3.89}}""", """{"state":{"cob":3.88}}""")]
    [InlineData("""{"iob":{"basaliob":3.89}}""", """{"iob":{"basaliob":3.88}}""")]
    [InlineData("""{"state":{"battery":75}}""", """{"state":{"battery":74.99}}""")]
    public async Task A_difference_of_one_hundredth_off_the_IOB_paths_does_not_match(string expected, string actual)
    {
        (await MatchesAsync(Tolerant, expected, actual)).Should().BeFalse();
    }

    [Theory]
    [InlineData("""{"iob":{"display":"0"}}""", """{"iob":{"display":"0.00"}}""")]
    [InlineData("""{"iob":{"displayLine":"IOB: 0U"}}""", """{"iob":{"displayLine":"IOB: 0.01U"}}""")]
    [InlineData("""{"state":{"iob":null}}""", """{"state":{"iob":0.01}}""")]
    [InlineData("""{"state":{"iob":3.89}}""", """{"state":{"iob":"3.88"}}""")]
    [InlineData("""{"state":{"iob":1e-2}}""", """{"state":{"iob":0.01}}""")]
    public async Task A_change_of_form_on_an_IOB_path_does_not_match(string expected, string actual)
    {
        (await MatchesAsync(Tolerant, expected, actual)).Should().BeFalse();
        (await MatchesAsync(Tolerant, actual, expected)).Should().BeFalse();
    }

    [Fact]
    public async Task A_difference_in_the_text_around_the_number_does_not_match()
    {
        (await MatchesAsync(
            Tolerant,
            """{"iob":{"displayLine":"IOB: 3.89U"}}""",
            """{"iob":{"displayLine":"COB: 3.89g"}}""")).Should().BeFalse();
    }

    [Fact]
    public async Task Options_without_the_tolerance_compare_the_IOB_paths_exactly()
    {
        var exact = new ResponseComparer(ComparisonOptions.Default);

        (await MatchesAsync(exact, """{"state":{"iob":3.89}}""", """{"state":{"iob":3.88}}""")).Should().BeFalse();
    }

    [Fact]
    public async Task The_tolerance_survives_the_options_copies_the_tests_chain()
    {
        var chained = new ResponseComparer(
            ComparisonOptions.Default.WithIobTolerance().WithIgnoredFields("serverTime").WithPresenceOnlyFields("activity"));

        (await MatchesAsync(chained, """{"state":{"iob":3.89}}""", """{"state":{"iob":3.88}}""")).Should().BeTrue();
    }
}
