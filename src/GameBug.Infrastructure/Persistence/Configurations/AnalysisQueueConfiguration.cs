using GameBug.Domain.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class AnalysisOutboxMessageConfiguration : IEntityTypeConfiguration<AnalysisOutboxMessage>
{
    public void Configure(EntityTypeBuilder<AnalysisOutboxMessage> builder)
    {
        builder.ToTable("analysis_outbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AggregateId)
            .HasColumnName("aggregate_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.DispatchStatus).HasColumnName("dispatch_status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(x => x.LockedBy).HasColumnName("locked_by").HasMaxLength(128);
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");
        builder.Property(x => x.DispatchedAt).HasColumnName("dispatched_at");
        builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(80);

        builder.HasIndex(x => new { x.DispatchStatus, x.NextAttemptAt });
        builder.HasIndex(x => x.AggregateId);
        builder.ToTable(table => table.HasCheckConstraint("CK_analysis_outbox_attempt", "attempt_count >= 0"));
    }
}

public sealed class AnalysisJobConfiguration : IEntityTypeConfiguration<AnalysisJob>
{
    public void Configure(EntityTypeBuilder<AnalysisJob> builder)
    {
        builder.ToTable("analysis_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.QueueName).HasColumnName("queue_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();
        builder.Property(x => x.ExpectedVersion).HasColumnName("expected_version").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(x => x.AvailableAt).HasColumnName("available_at").IsRequired();
        builder.Property(x => x.LockedBy).HasColumnName("locked_by").HasMaxLength(128);
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(80);

        builder.HasIndex(x => new { x.QueueName, x.Status, x.AvailableAt });
        builder.HasIndex(x => new { x.QueueName, x.AnalysisRunId, x.ExpectedVersion }).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("CK_analysis_jobs_attempt", "attempt_count >= 0"));
    }
}

public sealed class AnalysisCheckpointConfiguration : IEntityTypeConfiguration<AnalysisCheckpoint>
{
    public void Configure(EntityTypeBuilder<AnalysisCheckpoint> builder)
    {
        builder.ToTable("analysis_checkpoints");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();
        builder.Property(x => x.Stage).HasColumnName("stage").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.StageVersion).HasColumnName("stage_version").HasMaxLength(64).IsRequired();
        builder.Property(x => x.InputHash).HasColumnName("input_hash").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        builder.Property(x => x.OutputReference).HasColumnName("output_reference").HasColumnType("jsonb");
        builder.Property(x => x.Attempt).HasColumnName("attempt").IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.WarningCodesJson).HasColumnName("warning_codes").HasColumnType("jsonb");
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(80);

        builder.HasIndex(x => new { x.AnalysisRunId, x.Stage, x.StageVersion, x.InputHash })
            .IsUnique()
            .HasFilter("status = 'Completed'");
    }
}

public sealed class AnalysisAttemptConfiguration : IEntityTypeConfiguration<AnalysisAttempt>
{
    public void Configure(EntityTypeBuilder<AnalysisAttempt> builder)
    {
        builder.ToTable("analysis_attempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .IsRequired();
        builder.Property(x => x.JobId).HasColumnName("job_id").IsRequired();
        builder.Property(x => x.WorkerId).HasColumnName("worker_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.AttemptNumber).HasColumnName("attempt_number").IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(40).IsRequired();
        builder.Property(x => x.ErrorCode).HasColumnName("error_code").HasMaxLength(80);
        builder.Property(x => x.DurationMs).HasColumnName("duration_ms");

        builder.HasIndex(x => new { x.AnalysisRunId, x.AttemptNumber });
    }
}

public sealed class AnalysisExecutionLeaseConfiguration : IEntityTypeConfiguration<AnalysisExecutionLease>
{
    public void Configure(EntityTypeBuilder<AnalysisExecutionLease> builder)
    {
        builder.ToTable("analysis_execution_locks");
        builder.HasKey(x => x.AnalysisRunId);

        builder.Property(x => x.AnalysisRunId)
            .HasColumnName("analysis_run_id")
            .HasConversion(id => id.Value, value => new AnalysisRunId(value))
            .ValueGeneratedNever();
        builder.Property(x => x.LockedBy).HasColumnName("locked_by").HasMaxLength(128).IsRequired();
        builder.Property(x => x.LockedUntil).HasColumnName("locked_until").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
