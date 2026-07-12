using System.ComponentModel.DataAnnotations;

namespace ArkaCallCenter.Api.Models;

public record RequestOtpRequest([Required] string PhoneNumber);

public record VerifyOtpRequest([Required] string PhoneNumber, [Required] string Code);

public record CompleteProfileRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [Required, MaxLength(200)] string BrandName);
