using ArkaCallCenter.Infrastructure.Services;
using Xunit;

namespace ArkaCallCenter.Tests;

public class CrmLeadServiceTests
{
    [Fact]
    public void Operational_paths_match_the_supplied_contract()
    {
        Assert.Equal("/api/User/Login", CrmLeadService.LoginPath);
        Assert.Equal("/api/ContactUs/InsertContactUsByAdmin", CrmLeadService.InsertLeadPath);
    }

    [Theory]
    [InlineData("{\"result\":{\"token\":\"jwt-token\"}}")]
    [InlineData("{\"Result\":{\"Token\":\"jwt-token\"}}")]
    public void Login_token_is_read_case_insensitively(string body)
    {
        Assert.True(CrmLeadService.TryParseLoginToken(body, out var token));
        Assert.Equal("jwt-token", token);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"result\":{}}")]
    [InlineData("not-json")]
    public void Login_response_without_a_token_is_rejected(string body)
    {
        Assert.False(CrmLeadService.TryParseLoginToken(body, out var token));
        Assert.Null(token);
    }

    [Fact]
    public async Task Lead_payload_is_multipart_with_all_required_operational_fields()
    {
        using var content = CrmLeadService.CreateLeadContent(
            "شرکت نمونه",
            "09120000000@demo.arkadp.com",
            "09120000000",
            "لید تست قرارداد عملیاتی");

        Assert.Equal("multipart/form-data", content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(content.Headers.ContentType?.Parameters
            .FirstOrDefault(parameter => parameter.Name == "boundary")?.Value));

        var fieldNames = content
            .Select(part => part.Headers.ContentDisposition?.Name?.Trim('"'))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("inputModel.Name", fieldNames);
        Assert.Contains("inputModel.Email", fieldNames);
        Assert.Contains("inputModel.PhoneNumber", fieldNames);
        Assert.Contains("inputModel.FeedbackText", fieldNames);
        Assert.Contains("inputModel.RequestType", fieldNames);
        Assert.Contains("inputModel.RequestSource", fieldNames);
        Assert.Contains("inputModel.RequestedProject", fieldNames);
        Assert.Contains("inputModel.FormStatus", fieldNames);

        var body = await content.ReadAsStringAsync();
        Assert.Contains("09120000000", body);
    }

    [Theory]
    [InlineData("{\"success\":true,\"message\":\"created\"}", true, "created")]
    [InlineData("{\"Success\":true,\"Message\":\"created\"}", true, "created")]
    [InlineData("{\"success\":false,\"message\":\"rejected\"}", false, "rejected")]
    public void Lead_result_is_read_case_insensitively(string body, bool expected, string message)
    {
        var result = CrmLeadService.ParseResult(body);

        Assert.Equal(expected, result.ok);
        Assert.Equal(message, result.message);
    }
}
