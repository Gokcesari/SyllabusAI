using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SyllabusAI.Data.Migrations
{
    /// <inheritdoc />
    public partial class addFeedbackQuestionsAndAnswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionNo = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackQuestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseFeedbackAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseFeedbackId = table.Column<int>(type: "int", nullable: false),
                    FeedbackQuestionId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseFeedbackAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseFeedbackAnswers_CourseFeedbacks_CourseFeedbackId",
                        column: x => x.CourseFeedbackId,
                        principalTable: "CourseFeedbacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseFeedbackAnswers_FeedbackQuestions_FeedbackQuestionId",
                        column: x => x.FeedbackQuestionId,
                        principalTable: "FeedbackQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "FeedbackQuestions",
                columns: new[] { "Id", "IsActive", "QuestionNo", "Text" },
                values: new object[,]
                {
                    { 1, true, 1, "Dersin hedefleri ve icerigi donem basinda acik sekilde paylasildi." },
                    { 2, true, 2, "Ders plani (syllabus) ile islenen konular su ana kadar uyumluydu." },
                    { 3, true, 3, "Kullanilan dijital platformlar ders takibi ve etkilesim icin yeterliydi." },
                    { 4, true, 4, "Sunulan ders materyalleri (notlar, sunumlar vb.) yeterli ve faydaliydi." },
                    { 5, true, 5, "Ders kapsaminda onerilen ek kaynaklara ve okumalara erisim kolaydi." },
                    { 6, true, 6, "Islenen ders konularini su ana kadar teorik duzeyde iyi anlayabildim." },
                    { 7, true, 7, "Konularin pekismesi icin sinifta daha fazla tekrar veya uygulama yapilmasi gerektigini dusunuyorum." },
                    { 8, true, 8, "Dersin islenisi sirasinda farkli ogrenme hizlarina ve seviyelerine sahip ogrenciler gozetildi." },
                    { 9, true, 9, "Ders kapsaminda yapilan ornekler ve uygulamalar, gercek hayattaki/mesleki senaryolarla iyi iliskilendirildi." },
                    { 10, true, 10, "Dersin islenis temposu (ilerleyis hizi), konulari sindirmem ve not almam icin uygundur." },
                    { 11, true, 11, "Anlatilan konularin zorluk seviyesi, sahip oldugum on bilgilerle ve yetkinligimle ortusmektedir." },
                    { 12, true, 12, "Ders sirasinda soru sorma, tartismaya katilma ve fikir beyan etme konusunda kendimi tesvik edilmis hissediyorum." },
                    { 13, true, 13, "Verilen odevler, projeler veya kisa sinavlar ogrenme surecime gercek anlamda katki saglamaktadir." },
                    { 14, true, 14, "Dersin icerigi ve hocanin anlatim tarzi, konuya olan merakimi ve derse olan motivasyonumu canli tutmaktadir." },
                    { 15, true, 15, "Egitmen, karmasik kavramlari aciklarken farkli yontemler (gorsel araclar, benzetmeler, vaka analizleri vb.) kullanarak anlasilirligi artirmaktadir." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseFeedbackAnswers_CourseFeedbackId_FeedbackQuestionId",
                table: "CourseFeedbackAnswers",
                columns: new[] { "CourseFeedbackId", "FeedbackQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseFeedbackAnswers_FeedbackQuestionId",
                table: "CourseFeedbackAnswers",
                column: "FeedbackQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackQuestions_QuestionNo",
                table: "FeedbackQuestions",
                column: "QuestionNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseFeedbackAnswers");

            migrationBuilder.DropTable(
                name: "FeedbackQuestions");
        }
    }
}
