using ArkaCallCenter.Infrastructure.Services;
using Xunit;

namespace ArkaCallCenter.Tests;

public class RagRelevanceTests
{
    [Fact]
    public void Persian_variants_and_similar_word_forms_receive_lexical_support()
    {
        var similarity = RagService.LexicalSimilarity(
            "شرایط ثبت‌نام غیرحضوری چیست؟",
            "برای ثبت نام به صورت غیر حضوری، شماره همراه و کد ملی لازم است.");

        Assert.True(similarity >= 0.5, $"Expected useful lexical similarity, got {similarity:F3}");
    }

    [Fact]
    public void Borderline_semantic_match_with_good_lexical_overlap_is_relevant()
    {
        var relevant = RagService.IsRelevant(
            semanticScore: 0.29,
            lexicalScore: 0.75,
            threshold: 0.35);

        Assert.True(relevant);
    }

    [Fact]
    public void Unrelated_low_semantic_match_is_rejected()
    {
        var relevant = RagService.IsRelevant(
            semanticScore: 0.18,
            lexicalScore: 0,
            threshold: 0.35);

        Assert.False(relevant);
    }
}
