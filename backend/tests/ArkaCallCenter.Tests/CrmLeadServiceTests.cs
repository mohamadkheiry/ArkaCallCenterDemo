using System.Net;
using System.Text;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Entities;
using ArkaCallCenter.Core.Enums;
using ArkaCallCenter.Infrastructure.Data;
using ArkaCallCenter.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task Successful_history_does_not_suppress_a_repeated_lead()
    {
        var services = new ServiceCollection();
        await using var db = new ArkaDbContext(new DbContextOptionsBuilder<ArkaDbContext>()
            .UseInMemoryDatabase($"crm-repeat-{Guid.NewGuid():N}")
            .Options);
        services.AddSingleton(db);
        services.AddScoped<ISettingsService, SettingsService>();
        await using var provider = services.BuildServiceProvider();

        var previousSentAt = DateTime.UtcNow.AddDays(-1);
        db.AppSettings.AddRange(
            new AppSetting { Key = SettingKeys.CrmEnabled, Value = "true" },
            new AppSetting { Key = SettingKeys.CrmBaseUrl, Value = "https://crm.example.test" },
            new AppSetting { Key = SettingKeys.CrmUsername, Value = "user" },
            new AppSetting { Key = SettingKeys.CrmPassword, Value = "password", IsSecret = true });
        db.CrmLeadSubmissions.Add(new CrmLeadSubmission
        {
            PhoneNumber = "09120000000",
            Stage = LeadStage.PhoneEntered,
            Success = true,
            ResponseMessage = "previous success",
            SentAt = previousSentAt,
        });
        await db.SaveChangesAsync();

        var handler = new SuccessfulCrmHandler();
        var service = new CrmLeadService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedHttpClientFactory(new HttpClient(handler)),
            NullLogger<CrmLeadService>.Instance);

        await service.SubmitAsync(LeadStage.PhoneEntered, "09120000000");

        var snapshot = await db.CrmLeadSubmissions.SingleAsync();
        Assert.True(handler.RequestCount == 2,
            $"Expected login and insert requests, but observed {handler.RequestCount}. Last result: {snapshot.ResponseMessage}");
        Assert.Equal(CrmLeadService.LoginPath, handler.RequestPaths[0]);
        Assert.Equal(CrmLeadService.InsertLeadPath, handler.RequestPaths[1]);
        Assert.True(snapshot.Success);
        Assert.Equal("resent", snapshot.ResponseMessage);
        Assert.True(snapshot.SentAt > previousSentAt);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class SuccessfulCrmHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public List<string> RequestPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestPaths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            var json = RequestCount == 1
                ? "{\"result\":{\"token\":\"jwt-token\"}}"
                : "{\"success\":true,\"message\":\"resent\"}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
