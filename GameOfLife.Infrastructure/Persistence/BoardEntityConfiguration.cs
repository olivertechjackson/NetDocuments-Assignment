using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameOfLife.Infrastructure.Persistence;

public sealed class BoardEntityConfiguration : IEntityTypeConfiguration<BoardEntity>
{
    public void Configure(EntityTypeBuilder<BoardEntity> builder)
    {
        builder.ToTable("Boards");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Rows).IsRequired();
        builder.Property(b => b.Cols).IsRequired();
        builder.Property(b => b.Cells).IsRequired();
        builder.Property(b => b.CreatedAtUtc).IsRequired();
    }
}
