using ArkaCallCenter.Core.Common;

namespace ArkaCallCenter.Core.Entities;

public class OtpCode : BaseEntity
{
    public string PhoneNumber { get; set; } = default!;
    public string Code { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public bool Consumed { get; set; }
    public int Attempts { get; set; }
}
