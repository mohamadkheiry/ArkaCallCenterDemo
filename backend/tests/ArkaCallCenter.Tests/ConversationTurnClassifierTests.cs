using ArkaCallCenter.Realtime.Call;
using Xunit;

namespace ArkaCallCenter.Tests;

public class ConversationTurnClassifierTests
{
    [Theory]
    [InlineData("سلام")]
    [InlineData("سلام، وقت بخیر")]
    [InlineData("درود بر شما")]
    [InlineData("صبح بخیر")]
    [InlineData("روز بخیر")]
    [InlineData("خسته نباشید")]
    [InlineData("حالتون چطوره؟")]
    [InlineData("چه خبر؟")]
    [InlineData("خیلی ممنون از شما")]
    [InlineData("دست شما درد نکنه")]
    [InlineData("ببخشید")]
    [InlineData("خدا نگهدار")]
    [InlineData("روز خوش")]
    [InlineData("باشه متوجه شدم")]
    public void Social_turns_are_handled_without_rag(string text)
    {
        var handled = ConversationTurnClassifier.TryCreateResponse(text, out var response);

        Assert.True(handled);
        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.DoesNotContain("پایگاه دانش", response);
    }

    [Theory]
    [InlineData("سلام، ساعت کاری شما چطور است؟")]
    [InlineData("ممنون، قیمت این خدمات چقدر است؟")]
    [InlineData("سلام، شرایط دریافت وام چطوره؟")]
    [InlineData("حال سفارش من چطور است؟")]
    [InlineData("شرایط ثبت نام چیست؟")]
    public void Knowledge_questions_are_not_mistaken_for_social_turns(string text)
    {
        var handled = ConversationTurnClassifier.TryCreateResponse(text, out _);

        Assert.False(handled);
    }

    [Theory]
    [InlineData("من با کجا تماس گرفته‌ام؟")]
    [InlineData("با چه مجموعه‌ای تماس گرفتم؟")]
    [InlineData("اینجا کجاست؟")]
    [InlineData("شما کی هستید؟")]
    [InlineData("کی هستی؟")]
    [InlineData("کی هستین؟")]
    [InlineData("تو کی هستی؟")]
    [InlineData("چه کسی هستید؟")]
    [InlineData("اسمت چیه؟")]
    [InlineData("خودت را معرفی کن")]
    [InlineData("اسم کسب و کار شما چیست؟")]
    public void Business_identity_questions_return_the_configured_brand(string text)
    {
        var handled = ConversationTurnClassifier.TryCreateBusinessIdentityResponse(
            text,
            "فروشگاه آرکا",
            out var response);

        Assert.True(handled);
        Assert.Contains("فروشگاه آرکا", response);
        Assert.DoesNotContain("پایگاه دانش", response);
    }

    [Theory]
    [InlineData("آدرس شعبه شما کجاست؟")]
    [InlineData("ساعت کاری شرکت چیست؟")]
    [InlineData("مدیر شرکت کی هست؟")]
    [InlineData("این محصول برای کی هست؟")]
    public void Business_knowledge_questions_are_not_mistaken_for_identity(string text)
    {
        Assert.False(ConversationTurnClassifier.TryCreateBusinessIdentityResponse(text, "آرکا", out _));
    }
}
