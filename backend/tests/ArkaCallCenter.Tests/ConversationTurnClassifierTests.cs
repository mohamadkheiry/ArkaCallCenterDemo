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
}
