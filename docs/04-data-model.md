# مدل داده (ERD) — کال سنتر هوشمند آرکا

> موجودیت‌های اصلی دیتابیس و روابط آن‌ها (EF Core + MySQL).

```mermaid
erDiagram
    User ||--o| SmartPhone : "دارد"
    User ||--o| KnowledgeBase : "دارد"
    User ||--o{ OtpCode : "درخواست می‌کند"
    User ||--o{ TokenUsage : "مصرف دارد"
    SmartPhone ||--o{ CallSession : "میزبانِ تماس"
    KnowledgeBase ||--o{ KnowledgeChunk : "تکه‌های نمایه"

    User {
        int Id PK
        string PhoneNumber
        string FirstName
        string LastName
        string BrandName
        int Role "User/SuperAdmin"
        bool IsActive
        bool IsDemo
        bool ProfileCompleted
        string VoiceName
        int CallMinuteLimit
        int UsedMinutes
        string AvatarPath
    }

    SmartPhone {
        int Id PK
        int UserId FK
        int Extension
        string SipSecret
        int Status "Provisioning/Active/Failed/Suspended"
        string WelcomeMessageText
        string WelcomeAudioPath
        int AnswerAccuracyPercent "10..100"
    }

    KnowledgeBase {
        int Id PK
        int UserId FK
        int SourceType "Text/File"
        string RawText
        string FileName
        int CharCount
        int ModerationStatus "Pending/Approved/Rejected"
    }

    KnowledgeChunk {
        int Id PK
        int KnowledgeBaseId FK
        string Content
        string EmbeddingJson
    }

    CallSession {
        int Id PK
        int SmartPhoneId FK
        string CallerId
        datetime StartedAt
        datetime EndedAt
        int DurationSeconds
        bool AnsweredFromKb
        string TranscriptJson
        string UnansweredQuestionsJson
        string RecordingPath
    }

    OtpCode {
        int Id PK
        string PhoneNumber
        string Code
        int Attempts
        bool Consumed
        datetime ExpiresAt
    }

    TokenUsage {
        int Id PK
        string Operation
        string Model
        int PromptTokens
        int CompletionTokens
        int TotalTokens
    }

    AppSetting {
        string Key PK
        string Value
    }
```

## نکات کلیدی

- **AnswerAccuracyPercent** روی `SmartPhone`: پیش‌فرض ۷۰؛ کنترلِ پایبندی به پایگاه دانش از طریق پرامپت.
- **UnansweredQuestionsJson** روی `CallSession`: آرایه‌ی JSON از سوالاتی که پاسخشان در پایگاه دانش نبوده.
- **AppSetting**: تنظیماتِ سراسری (کلیدها/مدل‌های OpenAI، پیامِ fallback، گوینده‌ی پیش‌فرض، سقفِ دقیقه، موسیقی انتظار، ...)؛ اسرار با ماسک نمایش داده و هنگام ذخیره بازنویسی نمی‌شوند.
- **جداسازی مستأجرها:** همه‌ی پرس‌وجوها با `UserId` محدود می‌شوند.
