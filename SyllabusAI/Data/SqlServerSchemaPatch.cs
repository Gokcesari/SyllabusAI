using Microsoft.EntityFrameworkCore;

namespace SyllabusAI.Data;

/// <summary>
/// Eski LocalDB/SQL Server şemasında RAG tabloları yoksa (migration geçmişi kaymış DB'ler) çalışma anında oluşturur.
/// </summary>
public static class SqlServerSchemaPatch
{
    public static async Task EnsureSyllabusSupportTablesAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
            return;

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[SyllabusPdfUploads]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SyllabusPdfUploads] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [CourseId] int NOT NULL,
                    [OriginalFileName] nvarchar(max) NOT NULL,
                    [StoredRelativePath] nvarchar(max) NOT NULL,
                    [ExtractedText] nvarchar(max) NOT NULL,
                    [UploadedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_SyllabusPdfUploads] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SyllabusPdfUploads_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_SyllabusPdfUploads_CourseId] ON [dbo].[SyllabusPdfUploads]([CourseId]);
            END
            """,
            cancellationToken: ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[SyllabusChunks]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SyllabusChunks] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [CourseId] int NOT NULL,
                    [ChunkIndex] int NOT NULL,
                    [Text] nvarchar(max) NOT NULL,
                    [EmbeddingJson] nvarchar(max) NULL,
                    CONSTRAINT [PK_SyllabusChunks] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_SyllabusChunks_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_SyllabusChunks_CourseId_ChunkIndex] ON [dbo].[SyllabusChunks]([CourseId], [ChunkIndex]);
            END
            """,
            cancellationToken: ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF COL_LENGTH(N'dbo.Courses', N'FeedbackOpensAtUtc') IS NULL
                ALTER TABLE [dbo].[Courses] ADD [FeedbackOpensAtUtc] datetime2 NULL, [FeedbackClosesAtUtc] datetime2 NULL;
            """,
            cancellationToken: ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            IF OBJECT_ID(N'[dbo].[CourseFeedbacks]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CourseFeedbacks] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [CourseId] int NOT NULL,
                    [StudentUserId] int NOT NULL,
                    [Rating] tinyint NOT NULL,
                    [Comment] nvarchar(max) NULL,
                    [SubmittedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_CourseFeedbacks] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CourseFeedbacks_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_CourseFeedbacks_Users_StudentUserId] FOREIGN KEY ([StudentUserId]) REFERENCES [dbo].[Users] ([Id])
                );
                CREATE UNIQUE INDEX [IX_CourseFeedbacks_CourseId_StudentUserId] ON [dbo].[CourseFeedbacks]([CourseId], [StudentUserId]);
                CREATE INDEX [IX_CourseFeedbacks_StudentUserId] ON [dbo].[CourseFeedbacks]([StudentUserId]);
            END
            """,
            cancellationToken: ct);
    }
}
