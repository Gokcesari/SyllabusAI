using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyllabusAI.Data.Migrations;

/// <inheritdoc />
public partial class FeedbackQuestionsEnglish : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 1, "Text", "Course objectives and content were clearly communicated at the start of the term.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 2, "Text", "Topics covered so far align with the syllabus.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 3, "Text", "Digital platforms used were adequate for following the course and interaction.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 4, "Text", "Course materials (notes, slides, etc.) were sufficient and useful.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 5, "Text", "Additional resources and readings suggested in the course were easy to access.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 6, "Text", "So far I understand the topics covered at a theoretical level.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 7, "Text", "I think more in-class review or practice is needed to reinforce topics.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 8, "Text", "During instruction, students with different learning speeds and levels were considered.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 9, "Text", "Examples and activities in the course were well linked to real-world or professional scenarios.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 10, "Text", "The pace of the course suits my ability to digest topics and take notes.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 11, "Text", "The difficulty of topics matches my prior knowledge and skills.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 12, "Text", "I feel encouraged to ask questions, join discussions, and share ideas in class.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 13, "Text", "Assignments, projects, or quizzes meaningfully support my learning.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 14, "Text", "The content and the instructor’s style keep my interest and motivation for the course.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 15, "Text", "The instructor explains complex concepts using different methods to improve clarity.");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 1, "Text", "Dersin hedefleri ve icerigi donem basinda acik sekilde paylasildi.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 2, "Text", "Ders plani (syllabus) ile islenen konular su ana kadar uyumluydu.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 3, "Text", "Kullanilan dijital platformlar ders takibi ve etkilesim icin yeterliydi.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 4, "Text", "Sunulan ders materyalleri (notlar, sunumlar vb.) yeterli ve faydaliydi.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 5, "Text", "Ders kapsaminda onerilen ek kaynaklara ve okumalara erisim kolaydi.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 6, "Text", "Islenen ders konularini su ana kadar teorik duzeyde iyi anlayabildim.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 7, "Text", "Konularin pekismesi icin sinifta daha fazla tekrar veya uygulama yapilmasi gerektigini dusunuyorum.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 8, "Text", "Dersin islenisi sirasinda farkli ogrenme hizlarina ve seviyelerine sahip ogrenciler gozetildi.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 9, "Text", "Ders kapsaminda yapilan ornekler ve uygulamalar, gercek hayattaki/mesleki senaryolarla iyi iliskilendirildi.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 10, "Text", "Dersin islenis temposu (ilerleyis hizi), konulari sindirmem ve not almam icin uygundur.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 11, "Text", "Anlatilan konularin zorluk seviyesi, sahip oldugum on bilgilerle ve yetkinligimle ortusmektedir.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 12, "Text", "Ders sirasinda soru sorma, tartismaya katilma ve fikir beyan etme konusunda kendimi tesvik edilmis hissediyorum.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 13, "Text", "Verilen odevler, projeler veya kisa sinavlar ogrenme surecime gercek anlamda katki saglamaktadir.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 14, "Text", "Dersin icerigi ve hocanin anlatim tarzi, konuya olan merakimi ve derse olan motivasyonumu canli tutmaktadir.");
        migrationBuilder.UpdateData("FeedbackQuestions", "Id", 15, "Text", "Egitmen, karmasik kavramlari aciklarken farkli yontemler (gorsel araclar, benzetmeler, vaka analizleri vb.) kullanarak anlasilirligi artirmaktadir.");
    }
}
