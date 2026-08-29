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
    [InlineData("حیف")]
    [InlineData("افسوس")]
    [InlineData("متأسفم")]
    [InlineData("خدا نگهدار")]
    [InlineData("خدانگهدارتون")]
    [InlineData("موفق باشید")]
    [InlineData("روز خوبی داشته باشید")]
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
    [InlineData("سلام", "سلام!")]
    [InlineData("خدا قوت", "سلامت باشید!")]
    [InlineData("حالتون چطوره؟", "ممنون، خوبم")]
    [InlineData("خیلی لطف کردید", "خواهش می‌کنم.")]
    [InlineData("ببخشید", "خواهش می‌کنم، بفرمایید.")]
    [InlineData("حیف", "متوجه‌ام.")]
    [InlineData("موفق باشید", "خدانگهدار")]
    [InlineData("بله", "بله، بفرمایید.")]
    [InlineData("خیر", "بسیار خوب.")]
    [InlineData("عالی بود", "از لطف شما ممنونم")]
    [InlineData("نه، ممنون", "خواهش می‌کنم.")]
    public void Social_turns_return_the_expected_response_category(string text, string expectedPrefix)
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse(text, out var response));
        Assert.StartsWith(expectedPrefix, response);
    }

    [Theory]
    [InlineData("الو، سلام عرض می‌کنم")]
    [InlineData("عرض سلام و ادب")]
    [InlineData("سلام و درود خدمت شما")]
    [InlineData("صبحتون بخیر")]
    [InlineData("ظهرتون بخیر")]
    [InlineData("عصرتون بخیر")]
    [InlineData("شبتون بخیر")]
    [InlineData("وقت شما بخیر")]
    [InlineData("خسته نباشین")]
    public void Professional_persian_greetings_return_a_warm_welcome(string text)
    {
        AssertHandledWithFragments(text, "سلام", "خوش آمدید", "کمک");
    }

    [Theory]
    [InlineData("حال شما چطوره؟")]
    [InlineData("حالتان چطور است؟")]
    [InlineData("خوبین؟")]
    [InlineData("شما خوب هستین؟")]
    [InlineData("اوضاع و احوالتون چطوره؟")]
    [InlineData("سلامت هستید؟")]
    [InlineData("امیدوارم حالتون خوب باشه")]
    public void Wellbeing_questions_receive_a_professional_answer(string text)
    {
        AssertHandledWithFragments(text, "ممنون", "خوبم", "کمک");
    }

    [Theory]
    [InlineData("سلام، حالتون خوبه؟", "سلام", "ممنون", "خوبم")]
    [InlineData("درود، حال شما چطور است؟", "سلام", "ممنون", "خوبم")]
    [InlineData("ممنون، شما چطورید؟", "خواهش", "خوبم", "کمک")]
    [InlineData("مرسی، خودتون خوبین؟", "خواهش", "خوبم", "کمک")]
    public void Combined_greeting_thanks_and_wellbeing_receive_one_natural_response(
        string text,
        string firstExpectedFragment,
        string secondExpectedFragment,
        string thirdExpectedFragment)
    {
        AssertHandledWithFragments(
            text,
            firstExpectedFragment,
            secondExpectedFragment,
            thirdExpectedFragment);
    }

    [Theory]
    [InlineData("خوبم ممنون")]
    [InlineData("ممنونم، خوبم")]
    [InlineData("خوبم، سپاس از شما")]
    [InlineData("شکر خدا خوبم")]
    [InlineData("الحمدلله خوبم")]
    [InlineData("خوبم، شما چطور؟")]
    public void Positive_user_wellbeing_answers_are_acknowledged_and_conversation_stays_open(string text)
    {
        AssertHandledWithFragments(text, "خوشحالم", "بفرمایید");
    }

    [Theory]
    [InlineData("بد نیستم ممنون")]
    [InlineData("تعریفی ندارم")]
    [InlineData("امروز زیاد خوب نیستم")]
    public void Less_positive_user_wellbeing_answers_receive_empathy(string text)
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse(text, out var response));
        AssertContainsAny(response, "امیدوارم", "متأسفم", "بهتر");
        AssertContainsAny(response, "بفرمایید", "کمک");
        Assert.DoesNotContain("پایگاه دانش", response);
    }

    [Theory]
    [InlineData("اختیار دارید")]
    [InlineData("لطف دارید")]
    [InlineData("شما محبت دارید")]
    [InlineData("قابل شما را ندارد")]
    [InlineData("قابل شما رو نداره")]
    [InlineData("ارادتمندم")]
    [InlineData("قربان شما")]
    public void Persian_courtesies_receive_a_respectful_non_rag_answer(string text)
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse(text, out var response));
        AssertContainsAny(response, "ممنون", "سپاس", "لطف دارید");
        AssertContainsAny(response, "بفرمایید", "کمک");
        Assert.DoesNotContain("پایگاه دانش", response);
    }

    [Theory]
    [InlineData("ممنونم")]
    [InlineData("متشکرم")]
    [InlineData("سپاسگزارم")]
    [InlineData("زنده باشید")]
    [InlineData("یک دنیا ممنون")]
    [InlineData("ممنون از پیگیری شما")]
    [InlineData("از راهنمایی خوبتون متشکرم")]
    [InlineData("خیلی ممنون، عالی توضیح دادید")]
    [InlineData("خدا خیرت بده")]
    [InlineData("ممنون از وقتی که گذاشتید")]
    public void Natural_thanks_receive_a_polite_thanks_response(string text)
    {
        AssertHandledWithFragments(text, "خواهش می‌کنم", "بفرمایید");
    }

    [Theory]
    [InlineData("عذر می‌خواهم")]
    [InlineData("عذرخواهی می‌کنم")]
    [InlineData("پوزش می‌طلبم")]
    [InlineData("مزاحم شدم")]
    [InlineData("ببخشین")]
    [InlineData("ببخشید که وقت شما را گرفتم")]
    public void Natural_apologies_are_reassured_and_invited_to_continue(string text)
    {
        AssertHandledWithFragments(text, "خواهش می‌کنم", "بفرمایید", "کمک");
    }

    [Theory]
    [InlineData("فعلاً خداحافظ")]
    [InlineData("بدرود")]
    [InlineData("تا دیداری دیگر")]
    [InlineData("شب خوش")]
    [InlineData("اوقات خوش")]
    [InlineData("به امید دیدار مجدد")]
    [InlineData("خداحافظ و ممنون از راهنمایی‌تون")]
    public void Natural_farewells_close_the_conversation_politely(string text)
    {
        AssertHandledWithFragments(text, "خدانگهدار");
    }

    [Theory]
    [InlineData("بفرمایید ادامه بدید")]
    [InlineData("ادامه بدین لطفاً")]
    [InlineData("لطفاً توضیحتون رو ادامه بدید")]
    [InlineData("گوش می‌دم، بفرمایید")]
    [InlineData("خب، ادامه بده")]
    public void Explicit_requests_to_continue_receive_a_continuation_response(string text)
    {
        AssertHandledWithFragments(text, "حتماً", "ادامه");
    }

    [Theory]
    [InlineData("صدای من میاد؟")]
    [InlineData("الو، منو می‌شنوید؟")]
    [InlineData("صدام رو دارید؟")]
    public void Audio_connection_checks_confirm_that_the_caller_is_heard(string text)
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse(text, out var response));
        Assert.Contains("بله", response);
        AssertContainsAny(response, "می‌شنوم", "صدایتان", "صداتون", "صدا");
        Assert.DoesNotContain("پایگاه دانش", response);
    }

    [Theory]
    [InlineData("کسی هست؟")]
    [InlineData("الو، کسی اونجا هست؟")]
    [InlineData("پشت خط هستید؟")]
    public void Presence_checks_confirm_that_the_assistant_is_available(string text)
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse(text, out var response));
        Assert.Contains("بله", response);
        AssertContainsAny(response, "در خدمت", "هستم", "بفرمایید");
        Assert.DoesNotContain("پایگاه دانش", response);
    }

    [Theory]
    [InlineData("متوجه نشدم")]
    [InlineData("دوباره می‌گید؟")]
    [InlineData("لطفاً تکرار کنید")]
    [InlineData("صداتون قطع شد")]
    [InlineData("چی گفتید؟")]
    [InlineData("یک بار دیگه می‌گید؟")]
    public void Repair_and_repeat_requests_receive_an_apology_and_offer_to_repeat(string text)
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse(text, out var response));
        AssertContainsAny(response, "ببخشید", "متأسفم", "حتماً");
        AssertContainsAny(response, "تکرار", "دوباره");
        Assert.DoesNotContain("پایگاه دانش", response);
    }

    [Theory]
    [InlineData("سلام، راهنمایی کنید")]
    [InlineData("ببخشید، کمک کنید")]
    [InlineData("وقت بخیر، درباره خدمات شما سؤال دارم")]
    [InlineData("یه سؤال داشتم")]
    [InlineData("میشه راهنماییم کنید؟")]
    [InlineData("می‌تونید کمکم کنید؟")]
    public void General_requests_for_help_invite_the_caller_to_ask_their_question(string text)
    {
        AssertHandledWithFragments(text, "حتماً", "سؤال", "بفرمایید");
    }

    [Theory]
    [InlineData("چه طوری؟", "ممنون", "خوبم")]
    [InlineData("تو خوبی؟", "ممنون", "خوبم")]
    [InlineData("سلام خدمتتون", "سلام", "خوش آمدید")]
    [InlineData("ممنونم ازتون", "خواهش می‌کنم", "بفرمایید")]
    [InlineData("خداحافظ تا بعد", "خدانگهدار", "روز خوبی")]
    [InlineData("سلام، ببخشید مزاحم شدم", "سلام", "مزاحمتی نیست")]
    [InlineData("عالی‌ام", "خوشحالم", "بفرمایید")]
    [InlineData("عالی هستم", "خوشحالم", "بفرمایید")]
    [InlineData("امروز خوبم", "خوشحالم", "بفرمایید")]
    [InlineData("حالم خوبه", "خوشحالم", "بفرمایید")]
    [InlineData("حالم خیلی خوبه", "خوشحالم", "بفرمایید")]
    [InlineData("حالم عالیه", "خوشحالم", "بفرمایید")]
    [InlineData("مرسی، حالم خوبه", "خوشحالم", "بفرمایید")]
    [InlineData("من خوب هستم", "خوشحالم", "بفرمایید")]
    [InlineData("سلام مجدد", "سلام", "خوش آمدید")]
    [InlineData("دوباره سلام", "سلام", "خوش آمدید")]
    [InlineData("خوبه", "حتماً", "سؤال دیگری")]
    [InlineData("خوبه ممنون", "خواهش می‌کنم", "بفرمایید")]
    [InlineData("آره خوبه", "بله", "بفرمایید")]
    [InlineData("حالم بده", "متأسفم", "کمک")]
    [InlineData("فوق‌العاده بود", "ممنون", "مفید")]
    [InlineData("خواهش میکنم", "ممنون", "بفرمایید")]
    [InlineData("بله، ممنون", "خواهش می‌کنم", "بفرمایید")]
    public void Common_colloquial_variants_receive_the_semantically_correct_response(
        string text,
        string firstFragment,
        string secondFragment)
    {
        AssertHandledWithFragments(text, firstFragment, secondFragment);
    }

    [Fact]
    public void Thanks_combined_with_goodbye_acknowledges_both_intents()
    {
        AssertHandledWithFragments("خداحافظ و ممنون", "خواهش می‌کنم", "خدانگهدار");
    }

    [Fact]
    public void Have_a_good_day_is_classified_as_a_farewell()
    {
        AssertHandledWithFragments("روز خوبی داشته باشید", "خدانگهدار", "روز خوبی");
    }

    [Fact]
    public void No_problem_is_classified_as_reciprocal_courtesy_instead_of_generic_acknowledgment()
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse("قابلی نداره", out var response));
        Assert.StartsWith("از لطف شما ممنونم", response);
        Assert.False(response.StartsWith("حتماً", StringComparison.Ordinal));
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
    [InlineData("نه، منظورم قیمت محصول بود")]
    [InlineData("حیف که این بیمه شرایط متفاوتی دارد")]
    [InlineData("سلام، شرایط دریافت وام چطوره؟")]
    [InlineData("حال سفارش من چطور است؟")]
    [InlineData("شرایط ثبت نام چیست؟")]
    [InlineData("روز خوبی برای ثبت‌نام چه مدارکی لازم است؟")]
    [InlineData("خدا خیرت بده، قیمت خدمات چقدر است؟")]
    [InlineData("ممنون از وقتی که گذاشتید، وضعیت پرونده‌ام چه شد؟")]
    [InlineData("قابلی نداره، فقط قیمت را بگویید")]
    public void Knowledge_questions_are_not_mistaken_for_social_turns(string text)
    {
        var handled = ConversationTurnClassifier.TryCreateResponse(text, out var response);

        Assert.False(handled);
        Assert.Empty(response);
    }

    [Theory]
    [InlineData("سلام و عرض ادب، شرایط ضمانت چیست؟")]
    [InlineData("خوبم ممنون، وضعیت پرونده من چه شد؟")]
    [InlineData("ممنونم، مهلت پرداخت تا چه تاریخی است؟")]
    [InlineData("متشکرم اما هزینه تمدید چقدر می‌شود؟")]
    [InlineData("ببخشین، سود این وام چند درصده؟")]
    [InlineData("عذر می‌خوام، سفارشم چه زمانی می‌رسه؟")]
    [InlineData("بله، شرایط فسخ قرارداد چیست؟")]
    [InlineData("نه، آدرس شعبه تهران را می‌خواستم")]
    [InlineData("باشه، نحوه ثبت‌نام را هم توضیح دهید")]
    [InlineData("عالیه، سقف پوشش بیمه چقدر است؟")]
    [InlineData("نه ممنون، فقط وضعیت سفارش را بگویید")]
    [InlineData("فعلاً نه، شرایط لغو را توضیح دهید")]
    [InlineData("خدانگهدار را به انگلیسی چطور می‌گویند؟")]
    [InlineData("روز خوش چه معنایی دارد؟")]
    [InlineData("تفاوت تشکر و قدردانی چیست؟")]
    [InlineData("چگونه عذرخواهی رسمی بنویسم؟")]
    [InlineData("سلامتی سرور چطور بررسی می‌شود؟")]
    [InlineData("حال سفارش من کجاست؟")]
    [InlineData("صبح کاری شعبه از چه ساعتی شروع می‌شود؟")]
    [InlineData("خدا قوت یعنی چه؟")]
    [InlineData("ادامه قرارداد چه شرایطی دارد؟")]
    [InlineData("چطور ادامه اشتراک را فعال کنم؟")]
    [InlineData("درخواست ادامه همکاری چه شرایطی دارد؟")]
    [InlineData("لطفاً ادامه متن قرارداد را بخوانید")]
    [InlineData("ممنون ولی جواب سؤال قبلی چی شد؟")]
    [InlineData("خیر، سؤال من درباره قیمت بود")]
    [InlineData("سلام، حالتون خوبه؟ ساعات کاری چیه؟")]
    [InlineData("ممنون، شما چطورید؟ آدرس شعبه کجاست؟")]
    [InlineData("الو، منو می‌شنوید؟ هزینه خدمات چقدره؟")]
    [InlineData("کسی هست که شرایط وام را توضیح دهد؟")]
    [InlineData("متوجه نشدم شرایط فسخ قرارداد چیست")]
    [InlineData("دوباره می‌گید قیمت محصول چقدر بود؟")]
    [InlineData("لطفاً تکرار کنید مبلغ قسط چقدر است")]
    [InlineData("صداتون قطع شد، شماره پشتیبانی چیست؟")]
    [InlineData("آیا صدای من در مکالمه ضبط می‌شود؟")]
    [InlineData("صدای منشی را چگونه تغییر بدهم؟")]
    [InlineData("تکرار تماس ناموفق چه شرایطی دارد؟")]
    [InlineData("کسی هست مسئول فروش باشد؟")]
    [InlineData("حال من چطوره؟")]
    [InlineData("من درست فهمیدم؟")]
    [InlineData("خوب بود؟")]
    public void Social_words_inside_real_requests_must_not_bypass_rag(string text)
    {
        var handled = ConversationTurnClassifier.TryCreateResponse(text, out var response);

        Assert.False(handled);
        Assert.Empty(response);
    }

    [Theory]
    [InlineData("سَلام")]
    [InlineData("ممنونم!!!")]
    [InlineData("خدا‌نگهدار")]
    [InlineData("عذر مي‌خواهم")]
    public void Persian_unicode_and_punctuation_variants_are_classified(string text)
    {
        Assert.True(ConversationTurnClassifier.TryCreateResponse(text, out var response));
        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.DoesNotContain("پایگاه دانش", response);
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

    private static void AssertHandledWithFragments(string text, params string[] expectedFragments)
    {
        Assert.True(
            ConversationTurnClassifier.TryCreateResponse(text, out var response),
            $"Expected the social turn to be handled: {text}");
        Assert.False(string.IsNullOrWhiteSpace(response));
        Assert.DoesNotContain("پایگاه دانش", response);
        foreach (var fragment in expectedFragments)
            Assert.Contains(fragment, response);
    }

    private static void AssertContainsAny(string actual, params string[] expectedFragments)
    {
        Assert.True(
            expectedFragments.Any(actual.Contains),
            $"Expected '{actual}' to contain one of: {string.Join("، ", expectedFragments)}");
    }
}
