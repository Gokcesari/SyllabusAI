using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyllabusAI.Data.Migrations
{
    public partial class AddWeeklyLessonFeedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"IF COL_LENGTH(N'dbo.Courses', N'WeeklyFeedbackOpensAtUtc') IS NULL
                    ALTER TABLE [dbo].[Courses] ADD [WeeklyFeedbackOpensAtUtc] datetime2 NULL;");

            migrationBuilder.Sql(
                @"IF COL_LENGTH(N'dbo.Courses', N'WeeklyFeedbackClosesAtUtc') IS NULL
                    ALTER TABLE [dbo].[Courses] ADD [WeeklyFeedbackClosesAtUtc] datetime2 NULL;");

            migrationBuilder.Sql(
                @"IF OBJECT_ID(N'[dbo].[WeeklyFeedbackQuestions]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[WeeklyFeedbackQuestions] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [QuestionNo] int NOT NULL,
                        [Text] nvarchar(1000) NOT NULL,
                        [IsActive] bit NOT NULL,
                        CONSTRAINT [PK_WeeklyFeedbackQuestions] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_WeeklyFeedbackQuestions_QuestionNo] ON [dbo].[WeeklyFeedbackQuestions]([QuestionNo]);
                END");

            migrationBuilder.Sql(
                @"IF OBJECT_ID(N'[dbo].[CourseWeeklyFeedbacks]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CourseWeeklyFeedbacks] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [CourseId] int NOT NULL,
                        [StudentUserId] int NOT NULL,
                        [Rating] tinyint NOT NULL,
                        [Comment] nvarchar(max) NULL,
                        [SubmittedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_CourseWeeklyFeedbacks] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CourseWeeklyFeedbacks_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [dbo].[Courses]([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_CourseWeeklyFeedbacks_Users_StudentUserId] FOREIGN KEY ([StudentUserId]) REFERENCES [dbo].[Users]([Id])
                    );
                    CREATE UNIQUE INDEX [IX_CourseWeeklyFeedbacks_CourseId_StudentUserId] ON [dbo].[CourseWeeklyFeedbacks]([CourseId], [StudentUserId]);
                    CREATE INDEX [IX_CourseWeeklyFeedbacks_StudentUserId] ON [dbo].[CourseWeeklyFeedbacks]([StudentUserId]);
                END");

            migrationBuilder.Sql(
                @"IF OBJECT_ID(N'[dbo].[CourseWeeklyFeedbackAnswers]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[CourseWeeklyFeedbackAnswers] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [CourseWeeklyFeedbackId] int NOT NULL,
                        [WeeklyFeedbackQuestionId] int NOT NULL,
                        [Rating] tinyint NOT NULL,
                        CONSTRAINT [PK_CourseWeeklyFeedbackAnswers] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CourseWeeklyFeedbackAnswers_CourseWeeklyFeedbacks_CourseWeeklyFeedbackId] FOREIGN KEY ([CourseWeeklyFeedbackId]) REFERENCES [dbo].[CourseWeeklyFeedbacks]([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_CourseWeeklyFeedbackAnswers_WeeklyFeedbackQuestions_WeeklyFeedbackQuestionId] FOREIGN KEY ([WeeklyFeedbackQuestionId]) REFERENCES [dbo].[WeeklyFeedbackQuestions]([Id])
                    );
                    CREATE UNIQUE INDEX [IX_CourseWeeklyFeedbackAnswers_CourseWeeklyFeedbackId_WeeklyFeedbackQuestionId] ON [dbo].[CourseWeeklyFeedbackAnswers]([CourseWeeklyFeedbackId], [WeeklyFeedbackQuestionId]);
                    CREATE INDEX [IX_CourseWeeklyFeedbackAnswers_WeeklyFeedbackQuestionId] ON [dbo].[CourseWeeklyFeedbackAnswers]([WeeklyFeedbackQuestionId]);
                END");

            migrationBuilder.Sql(
                @"IF NOT EXISTS (SELECT 1 FROM [dbo].[WeeklyFeedbackQuestions])
                BEGIN
                    INSERT INTO [dbo].[WeeklyFeedbackQuestions] ([QuestionNo], [Text], [IsActive]) VALUES
                    (1, N'This week''s lesson was productive overall.', 1),
                    (2, N'The lesson content was clear and understandable.', 1),
                    (3, N'The instructor''s explanation was effective.', 1),
                    (4, N'The examples given in the lesson helped me understand the topic.', 1),
                    (5, N'There was sufficient interaction and participation during the lesson.', 1),
                    (6, N'I believe I can apply what I learned in this lesson.', 1),
                    (7, N'This week''s lesson met my expectations.', 1);
                END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left non-destructive for existing environments.
        }
    }
}
