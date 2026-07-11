using GameBug.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameBug.Infrastructure.Persistence.Configurations;

public sealed class WorkerHeartbeatConfiguration : IEntityTypeConfiguration<WorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<WorkerHeartbeat> builder)
    {
        builder.ToTable("worker_heartbeats");
        builder.HasKey(h => h.WorkerName);
        builder.Property(h => h.WorkerName).HasColumnName("worker_name").HasMaxLength(120).IsRequired();
        builder.Property(h => h.LastHeartbeatAt).HasColumnName("last_heartbeat_at").IsRequired();
    }
}
