using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Nanny_BackEnd.Models;

namespace Nanny_BackEnd.Data;

public partial class Sep490NannyDbContext : DbContext
{
    public Sep490NannyDbContext(DbContextOptions<Sep490NannyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Blog> Blogs { get; set; }

    public virtual DbSet<BlogCategory> BlogCategories { get; set; }

    public virtual DbSet<ChildProfile> ChildProfiles { get; set; }

    public virtual DbSet<ContactRequest> ContactRequests { get; set; }

    public virtual DbSet<Contract> Contracts { get; set; }

    public virtual DbSet<ContractTemplate> ContractTemplates { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<ConversationParticipant> ConversationParticipants { get; set; }

    public virtual DbSet<Faq> Faqs { get; set; }

    public virtual DbSet<FavoriteJobPosting> FavoriteJobPostings { get; set; }

    public virtual DbSet<FavoriteNanny> FavoriteNannies { get; set; }

    public virtual DbSet<HiringRecord> HiringRecords { get; set; }

    public virtual DbSet<Interview> Interviews { get; set; }

    public virtual DbSet<JobApplication> JobApplications { get; set; }

    public virtual DbSet<JobPosting> JobPostings { get; set; }

    public virtual DbSet<JobRequirement> JobRequirements { get; set; }

    public virtual DbSet<JobScheduleRequirement> JobScheduleRequirements { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<NannyAvailability> NannyAvailabilities { get; set; }

    public virtual DbSet<NannyCertificate> NannyCertificates { get; set; }

    public virtual DbSet<NannyProfile> NannyProfiles { get; set; }

    public virtual DbSet<NannySkill> NannySkills { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OtpCode> OtpCodes { get; set; }

    public virtual DbSet<ParentProfile> ParentProfiles { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    public virtual DbSet<UserSubscription> UserSubscriptions { get; set; }

    public virtual DbSet<VerificationDocument> VerificationDocuments { get; set; }

    public virtual DbSet<VerificationRequest> VerificationRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => new { e.Action, e.CreatedAt }, "IX_AuditLogs_Action_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_AuditLogs_UserId_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.EntityType).HasMaxLength(50);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_AuditLogs_Users");
        });

        modelBuilder.Entity<Blog>(entity =>
        {
            entity.HasIndex(e => new { e.Status, e.PublishedAt }, "IX_Blogs_Status_PublishedAt")
                .IsDescending(false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.Slug, "UQ_Blogs_Slug")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Slug).HasMaxLength(300);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.ThumbnailUrl).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(300);

            entity.HasOne(d => d.AuthorUser).WithMany(p => p.Blogs)
                .HasForeignKey(d => d.AuthorUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Blogs_Users");

            entity.HasOne(d => d.Category).WithMany(p => p.Blogs)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_Blogs_BlogCategories");
        });

        modelBuilder.Entity<BlogCategory>(entity =>
        {
            entity.HasIndex(e => e.Slug, "UQ_BlogCategories_Slug")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<ChildProfile>(entity =>
        {
            entity.HasIndex(e => e.ParentProfileId, "IX_ChildProfiles_ParentProfileId").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Characteristic).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.SpecialNeeds).HasMaxLength(1000);

            entity.HasOne(d => d.ParentProfile).WithMany(p => p.ChildProfiles)
                .HasForeignKey(d => d.ParentProfileId)
                .HasConstraintName("FK_ChildProfiles_ParentProfiles");
        });

        modelBuilder.Entity<ContactRequest>(entity =>
        {
            entity.HasIndex(e => new { e.ParentProfileId, e.NannyProfileId }, "UQ_ContactRequests_ParentProfileId_NannyProfileId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.ResponseMessage).HasMaxLength(1000);

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.ContactRequests)
                .HasForeignKey(d => d.NannyProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContactRequests_NannyProfiles");

            entity.HasOne(d => d.ParentProfile).WithMany(p => p.ContactRequests)
                .HasForeignKey(d => d.ParentProfileId)
                .HasConstraintName("FK_ContactRequests_ParentProfiles");
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasIndex(e => e.HiringRecordId, "IX_Contracts_HiringRecordId").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.PdfUrl).HasMaxLength(500);

            entity.HasOne(d => d.ContractTemplate).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.ContractTemplateId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contracts_ContractTemplates");

            entity.HasOne(d => d.HiringRecord).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.HiringRecordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contracts_HiringRecords");
        });

        modelBuilder.Entity<ContractTemplate>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Version).HasMaxLength(50);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasIndex(e => e.LastMessageAt, "IX_Conversations_LastMessageAt")
                .IsDescending()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<ConversationParticipant>(entity =>
        {
            entity.HasIndex(e => new { e.ConversationId, e.UserId }, "UQ_ConversationParticipants_ConversationId_UserId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ConversationParticipants)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("FK_ConversationParticipants_Conversations");

            entity.HasOne(d => d.User).WithMany(p => p.ConversationParticipants)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ConversationParticipants_Users");
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.ToTable("FAQs");

            entity.HasIndex(e => new { e.Category, e.IsActive }, "IX_FAQs_Category_IsActive").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Question).HasMaxLength(500);
        });

        modelBuilder.Entity<FavoriteJobPosting>(entity =>
        {
            entity.HasIndex(e => new { e.NannyProfileId, e.JobPostingId }, "UQ_FavoriteJobPostings_NannyProfileId_JobPostingId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.JobPosting).WithMany(p => p.FavoriteJobPostings)
                .HasForeignKey(d => d.JobPostingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FavoriteJobPostings_JobPostings");

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.FavoriteJobPostings)
                .HasForeignKey(d => d.NannyProfileId)
                .HasConstraintName("FK_FavoriteJobPostings_NannyProfiles");
        });

        modelBuilder.Entity<FavoriteNanny>(entity =>
        {
            entity.HasIndex(e => new { e.ParentProfileId, e.NannyProfileId }, "UQ_FavoriteNannies_ParentProfileId_NannyProfileId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Note).HasMaxLength(500);

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.FavoriteNannies)
                .HasForeignKey(d => d.NannyProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FavoriteNannies_NannyProfiles");

            entity.HasOne(d => d.ParentProfile).WithMany(p => p.FavoriteNannies)
                .HasForeignKey(d => d.ParentProfileId)
                .HasConstraintName("FK_FavoriteNannies_ParentProfiles");
        });

        modelBuilder.Entity<HiringRecord>(entity =>
        {
            entity.HasIndex(e => e.JobApplicationId, "IX_HiringRecords_JobApplicationId").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.Status, "IX_HiringRecords_Status").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.ContractTemplate).WithMany(p => p.HiringRecords)
                .HasForeignKey(d => d.ContractTemplateId)
                .HasConstraintName("FK_HiringRecords_ContractTemplates");

            entity.HasOne(d => d.JobApplication).WithMany(p => p.HiringRecords)
                .HasForeignKey(d => d.JobApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HiringRecords_JobApplications");

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.HiringRecords)
                .HasForeignKey(d => d.NannyProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HiringRecords_NannyProfiles");

            entity.HasOne(d => d.ParentProfile).WithMany(p => p.HiringRecords)
                .HasForeignKey(d => d.ParentProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HiringRecords_ParentProfiles");
        });

        modelBuilder.Entity<Interview>(entity =>
        {
            entity.HasIndex(e => e.JobApplicationId, "IX_Interviews_JobApplicationId").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.MeetingLink).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasOne(d => d.JobApplication).WithMany(p => p.Interviews)
                .HasForeignKey(d => d.JobApplicationId)
                .HasConstraintName("FK_Interviews_JobApplications");

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.Interviews)
                .HasForeignKey(d => d.NannyProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Interviews_NannyProfiles");

            entity.HasOne(d => d.ParentProfile).WithMany(p => p.Interviews)
                .HasForeignKey(d => d.ParentProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Interviews_ParentProfiles");
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasIndex(e => e.JobPostingId, "IX_JobApplications_JobPostingId").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.NannyProfileId, "IX_JobApplications_NannyProfileId").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.Status, "IX_JobApplications_Status").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.JobPostingId, e.NannyProfileId }, "UQ_JobApplications_JobPostingId_NannyProfileId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.RejectionReason).HasMaxLength(500);

            entity.HasOne(d => d.JobPosting).WithMany(p => p.JobApplications)
                .HasForeignKey(d => d.JobPostingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JobApplications_JobPostings");

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.JobApplications)
                .HasForeignKey(d => d.NannyProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JobApplications_NannyProfiles");
        });

        modelBuilder.Entity<JobPosting>(entity =>
        {
            entity.HasIndex(e => e.ChildProfileId, "IX_JobPostings_ChildProfileId").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.ParentProfileId, "IX_JobPostings_ParentProfileId").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.City, e.District, e.CreatedAt }, "IX_JobPostings_Search")
                .IsDescending(false, false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.Status, e.ModerationStatus }, "IX_JobPostings_Status").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.Longitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.ModerationNote).HasMaxLength(500);
            entity.Property(e => e.SalaryMax).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SalaryMin).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.ChildProfile).WithMany(p => p.JobPostings)
                .HasForeignKey(d => d.ChildProfileId)
                .HasConstraintName("FK_JobPostings_ChildProfiles");

            entity.HasOne(d => d.ModeratedByNavigation).WithMany(p => p.JobPostings)
                .HasForeignKey(d => d.ModeratedBy)
                .HasConstraintName("FK_JobPostings_ModeratedBy");

            entity.HasOne(d => d.ParentProfile).WithMany(p => p.JobPostings)
                .HasForeignKey(d => d.ParentProfileId)
                .HasConstraintName("FK_JobPostings_ParentProfiles");
        });

        modelBuilder.Entity<JobRequirement>(entity =>
        {
            entity.HasIndex(e => new { e.JobPostingId, e.SkillId }, "UQ_JobRequirements_JobPostingId_SkillId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsRequired).HasDefaultValue(true);

            entity.HasOne(d => d.JobPosting).WithMany(p => p.JobRequirements)
                .HasForeignKey(d => d.JobPostingId)
                .HasConstraintName("FK_JobRequirements_JobPostings");

            entity.HasOne(d => d.Skill).WithMany(p => p.JobRequirements)
                .HasForeignKey(d => d.SkillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_JobRequirements_Skills");
        });

        modelBuilder.Entity<JobScheduleRequirement>(entity =>
        {
            entity.HasIndex(e => new { e.JobPostingId, e.DayOfWeek, e.TimeSlot }, "UQ_JobScheduleRequirements").IsUnique();

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsRequired).HasDefaultValue(true);

            entity.HasOne(d => d.JobPosting).WithMany(p => p.JobScheduleRequirements)
                .HasForeignKey(d => d.JobPostingId)
                .HasConstraintName("FK_JobScheduleRequirements_JobPostings");
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt }, "IX_Messages_ConversationId_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AttachmentUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("FK_Messages_Conversations");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.Messages)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Messages_Users");
        });

        modelBuilder.Entity<NannyAvailability>(entity =>
        {
            entity.HasIndex(e => new { e.NannyProfileId, e.DayOfWeek, e.TimeSlot }, "UQ_NannyAvailabilities_NannyProfileId_DayOfWeek_TimeSlot")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsAvailable).HasDefaultValue(true);

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.NannyAvailabilities)
                .HasForeignKey(d => d.NannyProfileId)
                .HasConstraintName("FK_NannyAvailabilities_NannyProfiles");
        });

        modelBuilder.Entity<NannyCertificate>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CertificateUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IssuingOrganization).HasMaxLength(200);
            entity.Property(e => e.Name).HasMaxLength(200);

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.NannyCertificates)
                .HasForeignKey(d => d.NannyProfileId)
                .HasConstraintName("FK_NannyCertificates_NannyProfiles");
        });

        modelBuilder.Entity<NannyProfile>(entity =>
        {
            entity.HasIndex(e => e.AverageRating, "IX_NannyProfiles_AverageRating")
                .IsDescending()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.ExpectedSalaryMin, e.ExpectedSalaryMax }, "IX_NannyProfiles_ExpectedSalary").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.VerificationStatus, "IX_NannyProfiles_VerificationStatus").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.UserId, "UQ_NannyProfiles_UserId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.AverageRating).HasColumnType("decimal(3, 2)");
            entity.Property(e => e.Bio).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.ExpectedSalaryMax).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExpectedSalaryMin).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.User).WithOne(p => p.NannyProfileUser)
                .HasForeignKey<NannyProfile>(d => d.UserId)
                .HasConstraintName("FK_NannyProfiles_Users");

            entity.HasOne(d => d.VerifiedByNavigation).WithMany(p => p.NannyProfileVerifiedByNavigations)
                .HasForeignKey(d => d.VerifiedBy)
                .HasConstraintName("FK_NannyProfiles_VerifiedBy");
        });

        modelBuilder.Entity<NannySkill>(entity =>
        {
            entity.HasIndex(e => new { e.NannyProfileId, e.SkillId }, "UQ_NannySkills_NannyProfileId_SkillId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.NannySkills)
                .HasForeignKey(d => d.NannyProfileId)
                .HasConstraintName("FK_NannySkills_NannyProfiles");

            entity.HasOne(d => d.Skill).WithMany(p => p.NannySkills)
                .HasForeignKey(d => d.SkillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_NannySkills_Skills");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt }, "IX_Notifications_UserId_IsRead")
                .IsDescending(false, false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Content).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.RelatedEntityType).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Notifications_Users");
        });

        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Code).HasMaxLength(10);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);

            entity.HasOne(d => d.User).WithMany(p => p.OtpCodes)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_OtpCodes_Users");
        });

        modelBuilder.Entity<ParentProfile>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UQ_ParentProfiles_UserId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.FamilyDescription).HasMaxLength(1000);

            entity.HasOne(d => d.User).WithOne(p => p.ParentProfile)
                .HasForeignKey<ParentProfile>(d => d.UserId)
                .HasConstraintName("FK_ParentProfiles_Users");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(e => e.Token, "UQ_RefreshTokens_Token")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.CreatedByIp).HasMaxLength(45);
            entity.Property(e => e.JwtId).HasMaxLength(256);
            entity.Property(e => e.ReplacedByToken).HasMaxLength(500);
            entity.Property(e => e.RevokedByIp).HasMaxLength(45);
            entity.Property(e => e.Token).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_RefreshTokens_Users");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasIndex(e => new { e.ReportedEntityType, e.ReportedEntityId }, "IX_Reports_Entity").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_Reports_Status_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ActionTaken).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.ReportedEntityType).HasMaxLength(50);
            entity.Property(e => e.Resolution).HasMaxLength(1000);

            entity.HasOne(d => d.HandledByNavigation).WithMany(p => p.ReportHandledByNavigations)
                .HasForeignKey(d => d.HandledBy)
                .HasConstraintName("FK_Reports_HandledBy");

            entity.HasOne(d => d.ReporterUser).WithMany(p => p.ReportReporterUsers)
                .HasForeignKey(d => d.ReporterUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Reporter");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => new { e.RevieweeUserId, e.Rating }, "IX_Reviews_RevieweeUserId").HasFilter("([IsDeleted]=(0) AND [IsVisible]=(1))");

            entity.HasIndex(e => new { e.ReviewerUserId, e.HiringRecordId }, "UQ_Reviews_ReviewerUserId_HiringRecordId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.IsVisible).HasDefaultValue(true);

            entity.HasOne(d => d.HiringRecord).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.HiringRecordId)
                .HasConstraintName("FK_Reviews_HiringRecords");

            entity.HasOne(d => d.RevieweeUser).WithMany(p => p.ReviewRevieweeUsers)
                .HasForeignKey(d => d.RevieweeUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reviews_Reviewee");

            entity.HasOne(d => d.ReviewerUser).WithMany(p => p.ReviewReviewerUsers)
                .HasForeignKey(d => d.ReviewerUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reviews_Reviewer");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Roles_Name")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.HasIndex(e => e.Name, "UQ_Skills_Name")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasIndex(e => e.Key, "UQ_SystemSettings_Key")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.DataType).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Key).HasMaxLength(100);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(e => e.Status, "IX_Transactions_Status").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.UserId, e.CreatedAt }, "IX_Transactions_UserId_CreatedAt")
                .IsDescending(false, true)
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PaymentGatewayTransactionId).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.Transactions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_Transactions_Users");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => new { e.City, e.District }, "IX_Users_Location");

            entity.HasIndex(e => e.Status, "IX_Users_Status").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.Email, "UQ_Users_Email")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => e.PhoneNumber, "UQ_Users_PhoneNumber")
                .IsUnique()
                .HasFilter("([PhoneNumber] IS NOT NULL AND [IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.AvatarUrl).HasMaxLength(500);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.District).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.GoogleId).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.Latitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(9, 6)");
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasDefaultValue(3);
            entity.Property(e => e.Ward).HasMaxLength(100);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.RoleId }, "UQ_UserRoles_UserId_RoleId")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Roles");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserRoles_Users");
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasIndex(e => e.EndDate, "IX_UserSubscriptions_EndDate").HasFilter("([IsDeleted]=(0))");

            entity.HasIndex(e => new { e.UserId, e.Status }, "IX_UserSubscriptions_UserId_Status").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");

            entity.HasOne(d => d.PaymentTransaction).WithMany(p => p.UserSubscriptions)
                .HasForeignKey(d => d.PaymentTransactionId)
                .HasConstraintName("FK_UserSubscriptions_Transactions");

            entity.HasOne(d => d.SubscriptionPlan).WithMany(p => p.UserSubscriptions)
                .HasForeignKey(d => d.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserSubscriptions_SubscriptionPlans");

            entity.HasOne(d => d.User).WithMany(p => p.UserSubscriptions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserSubscriptions_Users");
        });

        modelBuilder.Entity<VerificationDocument>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.DocumentUrl).HasMaxLength(500);
            entity.Property(e => e.FileName).HasMaxLength(200);

            entity.HasOne(d => d.VerificationRequest).WithMany(p => p.VerificationDocuments)
                .HasForeignKey(d => d.VerificationRequestId)
                .HasConstraintName("FK_VerificationDocuments_VerificationRequests");
        });

        modelBuilder.Entity<VerificationRequest>(entity =>
        {
            entity.HasIndex(e => new { e.NannyProfileId, e.Status }, "IX_VerificationRequests_NannyProfileId_Status").HasFilter("([IsDeleted]=(0))");

            entity.Property(e => e.Id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getutcdate())");
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);

            entity.HasOne(d => d.NannyProfile).WithMany(p => p.VerificationRequests)
                .HasForeignKey(d => d.NannyProfileId)
                .HasConstraintName("FK_VerificationRequests_NannyProfiles");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.VerificationRequests)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_VerificationRequests_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
