using ArkaCallCenter.Infrastructure.Services;
using Xunit;

namespace ArkaCallCenter.Tests;

public class KnowledgeAnswerMatchingTests
{
    [Theory]
    [InlineData("كلاس‌هاي عصر چه ساعتی برگزار می‌شوند؟", "کلاس های عصر چه ساعتی برگزار می شوند")]
    [InlineData("شماره ۰۲۱۹۱۰۰۸۲۸۸", "شماره 02191008288")]
    public void Persian_normalization_removes_orthographic_noise(string first, string second)
        => Assert.Equal(KnowledgeAnswerService.NormalizeQuestion(first),
            KnowledgeAnswerService.NormalizeQuestion(second));

    [Fact]
    public void Equivalent_question_has_an_exact_match_after_normalization()
    {
        var first = KnowledgeAnswerService.NormalizeQuestion("کلاس‌های عصر چه ساعتی برگزار می‌شوند؟");
        var second = KnowledgeAnswerService.NormalizeQuestion("کلاس های عصر چه ساعتی برگزار می شوند");

        Assert.Equal(1, KnowledgeAnswerService.Similarity(first, second));
    }

    [Fact]
    public void Unrelated_question_does_not_pass_the_deterministic_threshold()
    {
        var food = KnowledgeAnswerService.NormalizeQuestion("قرمه سبزی چطور درست می‌شود؟");
        var classes = KnowledgeAnswerService.NormalizeQuestion("کلاس عصر چه ساعتی برگزار می‌شود؟");

        Assert.True(KnowledgeAnswerService.Similarity(food, classes) < 0.68);
    }

    [Fact]
    public void Small_typing_difference_keeps_a_high_score()
    {
        var typed = KnowledgeAnswerService.NormalizeQuestion("ساعت برگزاری کلاسهای عصر چیست");
        var stored = KnowledgeAnswerService.NormalizeQuestion("ساعت برگزاری کلاس های عصر چیست؟");

        Assert.True(KnowledgeAnswerService.Similarity(typed, stored) >= 0.68);
    }
}
