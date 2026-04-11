using AutoMapper;
using SyllabusAI.DTOs;
using SyllabusAI.Models;

namespace SyllabusAI.Service.Helpers;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Course, CourseDto>()
            .ForMember(d => d.InstructorName, o => o.MapFrom(s => s.Instructor.FullName ?? s.Instructor.Email))
            .ForMember(d => d.HasSyllabus, o => o.MapFrom(s => !string.IsNullOrWhiteSpace(s.SyllabusContent)))
            .ForMember(d => d.FeedbackResponseCount, o => o.MapFrom(s => s.Feedbacks.Count));

        CreateMap<Course, SyllabusDto>()
            .ForMember(d => d.CourseId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.HighlightKeywords, o => o.MapFrom(s =>
                string.IsNullOrWhiteSpace(s.HighlightKeywords)
                    ? null
                    : s.HighlightKeywords
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));

        CreateMap<SyllabusPdfUpload, SyllabusPdfUploadResponseDto>()
            .ForMember(d => d.FileKind, o => o.Ignore())
            .ForMember(d => d.ExtractedCharacterCount, o => o.Ignore())
            .ForMember(d => d.RagChunkCount, o => o.Ignore());

        CreateMap<CourseFeedback, CourseFeedbackItemDto>()
            .ForMember(d => d.StudentEmail, o => o.MapFrom(s => s.Student.Email))
            .ForMember(d => d.StudentName, o => o.MapFrom(s => s.Student.FullName))
            // Answers alani CourseService icinde soru metniyle elle dolduruluyor.
            .ForMember(d => d.Answers, o => o.Ignore());

        CreateMap<CourseFeedbackAnswer, SurveyQuestionResponseDto>()
            .ForMember(d => d.QuestionNo, o => o.MapFrom(s => s.FeedbackQuestion.QuestionNo))
            .ForMember(d => d.QuestionText, o => o.MapFrom(s => s.FeedbackQuestion.Text));

        CreateMap<User, UserInfo>();
        CreateMap<User, UserSummaryDto>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.Name));

        CreateMap<Role, RoleDto>();
    }
}
