using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Api.Models;

public record UpdateSettingsRequest(Dictionary<string, string?> Settings);

public record SmsTemplateDto(SmsEventType EventType, string Body, bool Enabled);
public record UpdateSmsTemplatesRequest(List<SmsTemplateDto> Templates);

public record SmsRecipientDto(SmsEventType EventType, bool UseUserOwnNumber, string? PhoneNumber);
public record UpdateSmsEventsRequest(List<SmsRecipientDto> Recipients);

public record VoiceDto(string Name, string DisplayName, bool Enabled, bool IsDefault);
public record UpdateVoicesRequest(List<VoiceDto> Voices);

public record FallbackMessageRequest(string Text, string Voice);

public record UpdateUserLimitRequest(int? CallMinuteLimit);

public record CreateDemoRequest(string Label, string WelcomeText, string KbText, string? Voice, int? MinuteLimit);
public record UpdateDemoRequest(string? Label, string? WelcomeText, string? KbText, string? Voice, int? MinuteLimit, bool? IsActive);

public record MainGreetingRequest(string Text, string Voice);
public record HoldEnabledRequest(bool Enabled);
