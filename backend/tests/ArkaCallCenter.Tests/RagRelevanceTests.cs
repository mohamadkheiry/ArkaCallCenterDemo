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

    [Fact]
    public void Bm25_uses_Persian_synonyms_to_find_a_paraphrased_answer()
    {
        var scores = RagService.Bm25Scores(
            "هزینه عضویت غیرحضوری چقدره؟",
            new[]
            {
                "تعرفه ثبت نام آنلاین پانصد هزار تومان است.",
                "ساعت کاری شعبه از هشت صبح تا چهار عصر است.",
                "نشانی دفتر مرکزی در خیابان آزادی است."
            });

        Assert.Equal(1, scores[0], precision: 6);
        Assert.Equal(0, scores[1], precision: 6);
        Assert.Equal(0, scores[2], precision: 6);
    }

    [Fact]
    public void Reciprocal_rank_fusion_promotes_lexically_supported_semantic_candidate()
    {
        var unrelatedSemanticLeader = RagService.FuseScores(
            semanticScore: 0.55,
            lexicalScore: 0,
            semanticRank: 1,
            lexicalRank: 0);
        var supportedAnswer = RagService.FuseScores(
            semanticScore: 0.50,
            lexicalScore: 1,
            semanticRank: 2,
            lexicalRank: 1);

        Assert.True(supportedAnswer > unrelatedSemanticLeader);
    }

    [Fact]
    public void Bm25_does_not_overrate_one_generic_match_when_product_terms_are_missing()
    {
        var scores = RagService.Bm25Scores(
            "قیمت لپ تاپ چقدر است؟",
            new[]
            {
                "هزینه صدور مجوز نمایندگی بیمه بر اساس آیین نامه تعیین می‌شود.",
                "توانگری مالی شرکت‌های بیمه به صورت مستمر ارزیابی می‌شود."
            });

        Assert.InRange(scores[0], 0, 0.34);
        Assert.Equal(0, scores[1], precision: 6);
    }

    [Fact]
    public void Lexical_fallback_accepts_a_well_covered_knowledge_question()
    {
        var documents = new[]
        {
            "وظیفه اصلی بیمه مرکزی تنظیم مقررات و نظارت بر شرکت‌های بیمه است.",
            "آدرس شعبه مرکزی در خیابان آزادی است."
        };
        var bm25 = RagService.Bm25Scores("با تشکر، وظیفه اصلی بیمه مرکزی چیست؟", documents);
        var fuzzy = RagService.LexicalSimilarity("با تشکر، وظیفه اصلی بیمه مرکزی چیست؟", documents[0]);
        var lexicalScore = (bm25[0] * 0.70) + (fuzzy * 0.30);

        Assert.True(RagService.IsLexicalFallbackRelevant(lexicalScore));
    }

    [Fact]
    public void Lexical_fallback_rejects_a_partial_generic_overlap()
    {
        Assert.False(RagService.IsLexicalFallbackRelevant(0.34));
    }
}
