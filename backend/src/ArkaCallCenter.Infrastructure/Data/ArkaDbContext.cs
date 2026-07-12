using ArkaCallCenter.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArkaCallCenter.Infrastructure.Data;

public class ArkaDbContext : DbContext
{
    public ArkaDbContext(DbContextOptions<ArkaDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<SmartPhone> SmartPhones => Set<SmartPhone>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<CallSession> CallSessions => Set<CallSession>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<SmsTemplate> SmsTemplates => Set<SmsTemplate>();
    public DbSet<SmsEventRecipient> SmsEventRecipients => Set<SmsEventRecipient>();
    public DbSet<VoiceOption> VoiceOptions => Set<VoiceOption>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.PhoneNumber).IsUnique();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.BrandName).HasMaxLength(200);
            e.Property(x => x.VoiceName).HasMaxLength(100);
            e.HasOne(x => x.SmartPhone).WithOne(x => x.User).HasForeignKey<SmartPhone>(x => x.UserId);
            e.HasOne(x => x.KnowledgeBase).WithOne(x => x.User).HasForeignKey<KnowledgeBase>(x => x.UserId);
        });

        b.Entity<SmartPhone>(e =>
        {
            e.HasIndex(x => x.Extension).IsUnique();
            e.Property(x => x.WelcomeMessageText).HasColumnType("text");
            e.Property(x => x.SipSecret).HasMaxLength(200);
        });

        b.Entity<KnowledgeBase>(e =>
        {
            e.Property(x => x.RawText).HasColumnType("longtext");
            e.Property(x => x.FileName).HasMaxLength(300);
            e.Property(x => x.FilePath).HasMaxLength(500);
            e.Property(x => x.ModerationReason).HasColumnType("text");
        });

        b.Entity<KnowledgeChunk>(e =>
        {
            e.Property(x => x.Content).HasColumnType("text");
            e.Property(x => x.EmbeddingJson).HasColumnType("longtext");
            e.HasOne(x => x.KnowledgeBase).WithMany(x => x.Chunks).HasForeignKey(x => x.KnowledgeBaseId);
        });

        b.Entity<OtpCode>(e =>
        {
            e.HasIndex(x => x.PhoneNumber);
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.Code).HasMaxLength(10).IsRequired();
        });

        b.Entity<CallSession>(e =>
        {
            e.Property(x => x.CallerId).HasMaxLength(50);
            e.Property(x => x.TranscriptJson).HasColumnType("longtext");
            e.HasOne(x => x.SmartPhone).WithMany(x => x.CallSessions).HasForeignKey(x => x.SmartPhoneId);
        });

        b.Entity<AppSetting>(e =>
        {
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(150).IsRequired();
            e.Property(x => x.Value).HasColumnType("text");
        });

        b.Entity<SmsTemplate>(e =>
        {
            e.HasIndex(x => x.EventType).IsUnique();
            e.Property(x => x.Body).HasColumnType("text").IsRequired();
        });

        b.Entity<SmsEventRecipient>(e =>
        {
            e.Property(x => x.PhoneNumber).HasMaxLength(20);
        });

        b.Entity<VoiceOption>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(50);
        });
    }
}
