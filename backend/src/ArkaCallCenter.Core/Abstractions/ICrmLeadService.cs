using ArkaCallCenter.Core.Enums;

namespace ArkaCallCenter.Core.Abstractions;

/// <summary>
/// ارسالِ لیدِ کاربرانِ دمو به CRM فروش (ExternalEndpoint/InsertContactUs) تا تیمِ فروش
/// بتواند بعداً از این داده‌ها استفاده کند.
/// </summary>
public interface ICrmLeadService
{
    /// <summary>
    /// ارسالِ یک مرحله‌ی لید به‌صورت «آتش‌کن‌و‌فراموش‌کن». هرگز جریانِ کاربر را کند یا خراب نمی‌کند:
    /// خطاها فقط لاگ می‌شوند. هر مرحله برای هر شماره حداکثر یک‌بار ارسالِ موفق دارد.
    /// </summary>
    void Enqueue(CrmLeadStage stage, string phoneNumber);
}
