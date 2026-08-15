using ArkaCallCenter.Core.Constants;
using ArkaCallCenter.Core.Abstractions;
using ArkaCallCenter.Infrastructure.Services;
using ArkaCallCenter.Realtime.Call;
using Xunit;

namespace ArkaCallCenter.Tests;

/// <summary>
/// Guards the evidence needed by the call router to distinguish social turns,
/// clearly out-of-domain questions and on-domain questions whose answer is absent.
/// </summary>
public class ConversationRoutingContractTests
{
    private static readonly string[] InsuranceKnowledge =
    {
        "بیمه مرکزی بر فعالیت شرکت‌های بیمه نظارت می‌کند و مقررات صنعت بیمه را تنظیم می‌کند.",
        "بیمه‌نامه شخص ثالث خسارت‌های جانی و مالی واردشده به اشخاص ثالث را پوشش می‌دهد."
    };

    [Theory]
    [InlineData("حالت چطوره؟")]
    [InlineData("سلام، خوبی؟")]
    [InlineData("احوال شما چطوره؟")]
    public void Wellbeing_turn_is_answered_conversationally_before_rag(string transcript)
    {
        var handled = ConversationTurnClassifier.TryCreateResponse(transcript, out var response);

        Assert.True(handled);
        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.Contains("ممنون", response);
        Assert.NotEqual(ConversationMessages.UnknownKnowledge, response);
    }

    [Fact]
    public void Completely_out_of_domain_food_question_has_no_domain_evidence()
    {
        const string query = "قرمه سبزی را چطور درست کنم؟";

        Assert.All(InsuranceKnowledge, document =>
        {
            Assert.False(RagService.HasDistinctiveTermOverlap(query, document));
            Assert.Equal(0, RagService.LexicalSimilarity(query, document), precision: 6);
        });

        Assert.All(RagService.Bm25Scores(query, InsuranceKnowledge),
            score => Assert.Equal(0, score, precision: 6));
    }

    [Fact]
    public void On_domain_question_with_missing_answer_keeps_domain_evidence_but_is_not_a_rag_answer()
    {
        const string query = "برای بیمه بدنه تخفیف ویژه فرهنگیان دارید؟";

        Assert.Contains(InsuranceKnowledge,
            document => RagService.HasDistinctiveTermOverlap(query, document));

        var bestLexicalScore = BestLexicalScore(query, InsuranceKnowledge);

        Assert.InRange(bestLexicalScore, 0.000001, 0.549999);
        Assert.False(RagService.IsLexicalFallbackRelevant(bestLexicalScore));
    }

    [Fact]
    public void Only_answer_candidates_are_reported_as_found()
    {
        Assert.True(new RagAnswer(RagOutcome.AnswerCandidate, Array.Empty<RagHit>(), "context").Found);
        Assert.False(new RagAnswer(RagOutcome.InDomainUnknown, Array.Empty<RagHit>(), "").Found);
        Assert.False(new RagAnswer(RagOutcome.OutOfDomain, Array.Empty<RagHit>(), "").Found);
    }

    [Fact]
    public void Out_of_domain_message_introduces_the_business_scope_without_claiming_a_kb_miss()
    {
        var message = ConversationMessages.CreateOutOfDomain(
            "شرکت سناپ",
            "اطلاعات و خدمات بیمه‌ای");

        Assert.Contains("شرکت سناپ", message);
        Assert.Contains("اطلاعات و خدمات بیمه‌ای", message);
        Assert.Contains("همین زمینه", message);
        Assert.DoesNotContain("پایگاه دانش", message);
        Assert.DoesNotContain("نمی‌دانم", message);
    }

    [Theory]
    [InlineData("پاسخ این سؤال را فعلاً ندارم.")]
    [InlineData("اطلاعات کافی ندارم.")]
    public void In_domain_unknown_message_always_offers_an_operator_handoff(string configured)
    {
        var message = ConversationMessages.EnsureOperatorEscalation(configured);

        Assert.StartsWith(configured, message);
        Assert.Contains("کارشناس", message);
        Assert.Contains("اپراتور", message);
    }

    [Fact]
    public void Domain_excerpt_is_bounded_and_samples_the_whole_knowledge_base()
    {
        var raw = $"FIRST-{new string('ا', 8_000)}-MIDDLE-{new string('ب', 8_000)}-LAST";

        var excerpt = RagService.BuildDomainExcerpt("پیام خوش‌آمد", raw);

        Assert.InRange(excerpt.Length, 1, 12_000);
        Assert.Contains("FIRST", excerpt);
        Assert.Contains("MIDDLE", excerpt);
        Assert.Contains("LAST", excerpt);
    }

    [Fact]
    public void Existing_demo_welcome_provides_a_useful_scope_fallback()
    {
        var scope = RagService.InferScopeFromWelcome(
            "سلام من دستیار اطلاعات بیمه ای شرکت سناپ هستم اگر سوالی دارید بپرسید در خدمتم.");

        Assert.NotNull(scope);
        Assert.Contains("اطلاعات", scope);
        Assert.Contains("بیمه", scope);
        Assert.DoesNotContain("شرکت سناپ", scope);
    }

    private static double BestLexicalScore(string query, IReadOnlyList<string> documents)
    {
        var bm25Scores = RagService.Bm25Scores(query, documents);
        return documents
            .Select((document, index) =>
                (bm25Scores[index] * 0.70) +
                (RagService.LexicalSimilarity(query, document) * 0.30))
            .Max();
    }
}
