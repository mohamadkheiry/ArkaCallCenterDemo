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
    [InlineData("خیلی لطف کردید")]
    [InlineData("خیلی محبت کردید")]
    [InlineData("خیلی زحمت کشیدید")]
    [InlineData("دستتون درد نکنه")]
    [InlineData("ممنون بابت راهنماییتون")]
    [InlineData("مرسی بابت توضیحات")]
    [InlineData("سپاس فراوان")]
    [InlineData("دمت گرم")]
    [InlineData("خدا خیرتون بده")]
    [InlineData("خواهش می‌کنم")]
    [InlineData("قابلی نداشت")]
    [InlineData("عالی بود")]
    [InlineData("نه، ممنون")]
    [InlineData("خیر")]
    [InlineData("نخیر")]
    [InlineData("ببخشید")]
    [InlineData("شرمنده وقتتون رو گرفتم")]
    [InlineData("معذرت میخوام")]
    [InlineData("خدا نگهدار")]
    [InlineData("خدانگهدارتون")]
    [InlineData("موفق باشید")]
    [InlineData("به امید دیدار")]
    [InlineData("یا علی")]
    [InlineData("روز خوش")]
    [InlineData("روزتون خوش")]
    [InlineData("سلام علیکم")]
    [InlineData("خدا قوت")]
    [InlineData("باشه متوجه شدم")]
    [InlineData("بله")]
    [InlineData("آره، بفرمایید")]
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
    [InlineData("ممنون بابت راهنمایی، قیمت خدمات چقدر است؟")]
    [InlineData("مرسی، شرایط وام چیست؟")]
    [InlineData("ببخشید آدرس شعبه کجاست؟")]
    [InlineData("شرمنده، ساعت کاری شما چطور است؟")]
    [InlineData("لطفاً راهنمایی کنید چطور ثبت نام کنم؟")]
    [InlineData("خیر، درباره شرایط قرارداد سؤال دارم")]
    [InlineData("درد دارید؟")]
    [InlineData("چه وقت است؟")]
    [InlineData("وقت دارید؟")]
    [InlineData("موفق هستید؟")]
    [InlineData("خدا هست؟")]
    [InlineData("لطف کنید قیمت را بگویید")]
    [InlineData("خواهش می‌کنم شرایط را توضیح دهید")]
    [InlineData("سلام، راهنمایی کنید")]
    [InlineData("ببخشید، کمک کنید")]
    [InlineData("نه، منظورم قیمت محصول بود")]
    [InlineData("سلام، شرایط دریافت وام چطوره؟")]
    [InlineData("حال سفارش من چطور است؟")]
    [InlineData("شرایط ثبت نام چیست؟")]
    public void Knowledge_questions_are_not_mistaken_for_social_turns(string text)
    {
        var handled = ConversationTurnClassifier.TryCreateResponse(text, out _);

        Assert.False(handled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("؟؟؟")]
    public void Empty_or_non_lexical_transcripts_are_not_meaningful(string text)
    {
        Assert.False(ConversationTurnClassifier.HasMeaningfulInput(text));
    }

    [Fact]
    public void Standalone_affirmation_gets_a_polite_prompt_instead_of_fallback()
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse("بله.", out var response));
        Assert.Contains("بفرمایید", response);
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
