using FluentAssertions;
using SmmAnalyzerPrototype.Api.Services;
using SmmAnalyzerPrototype.Data.Models.DTO.Post;

namespace SmmAnalyzerPrototype.Tests.Services
{
    public class GrammarFalsePositiveFilterServiceTests
    {
        private readonly GrammarFalsePositiveFilterService _service = new();

        [Fact]
        public void Filter_ShouldRejectError_WhenFragmentIsEmpty()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "   ",
                    Suggestion = "исправление",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 3
                }
            };


            var result = _service.Filter(errors, "Тестовый текст");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().HaveCount(1);
            result.RejectedErrors[0].Fragment.Should().Be("   ");
        }

        [Fact]
        public void Filter_ShouldAcceptPunctuationError_WhenMessageMentionsComma()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "однако",
                    Suggestion = ", однако",
                    Type = "typographical",
                    Message = "Возможно, пропущена запятая.",
                    Offset = 10,
                    Length = 6
                }
            };


            var result = _service.Filter(errors, "Мы пришли однако было поздно.");


            result.AcceptedErrors.Should().HaveCount(1);
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().BeEmpty();
            result.AcceptedErrors[0].Message.Should().Contain("запятая");
        }

        [Fact]
        public void Filter_ShouldAcceptPunctuationError_WhenMessageMentionsHyphen()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "по настоящему",
                    Suggestion = "по-настоящему",
                    Type = "grammar",
                    Message = "Возможно, требуется дефис.",
                    Offset = 0,
                    Length = 13
                }
            };


            var result = _service.Filter(errors, "по настоящему важный проект");


            result.AcceptedErrors.Should().HaveCount(1);
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().BeEmpty();
            result.AcceptedErrors[0].Suggestion.Should().Be("по-настоящему");
        }

        [Fact]
        public void Filter_ShouldRejectMisspelling_WhenSuggestionIsEmpty()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "ошибкка",
                    Suggestion = "",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 7
                }
            };


            var result = _service.Filter(errors, "ошибкка в тексте");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().HaveCount(1);
        }

        [Fact]
        public void Filter_ShouldRejectMisspelling_WhenFragmentLooksLikeUppercaseAbbreviation()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "SMM",
                    Suggestion = "сам",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 3
                }
            };


            var result = _service.Filter(errors, "SMM специалист подготовил публикацию.");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().HaveCount(1);
            result.RejectedErrors[0].Fragment.Should().Be("SMM");
        }

        [Fact]
        public void Filter_ShouldRejectMisspelling_WhenFragmentLooksLikeTechToken()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "ASP.NET",
                    Suggestion = "аспект",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 7
                }
            };


            var result = _service.Filter(errors, "ASP.NET используется для разработки веб-приложения.");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().HaveCount(1);
            result.RejectedErrors[0].Fragment.Should().Be("ASP.NET");
        }

        [Fact]
        public void Filter_ShouldRejectMisspelling_WhenFragmentContainsLettersAndDigits()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "GPT4",
                    Suggestion = "ГТО",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 4
                }
            };


            var result = _service.Filter(errors, "GPT4 используется как обозначение модели.");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().HaveCount(1);
        }

        [Fact]
        public void Filter_ShouldReturnSuspicious_WhenFragmentContainsMixedLatinAndCyrillic()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "мaркетинг",
                    Suggestion = "маркетинг",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 9
                }
            };


            var result = _service.Filter(errors, "мaркетинг в социальных сетях");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().HaveCount(1);
            result.RejectedErrors.Should().BeEmpty();
            result.SuspiciousErrors[0].Fragment.Should().Be("мaркетинг");
        }

        [Fact]
        public void Filter_ShouldAcceptSpacingCorrection_WhenFragmentAndSuggestionDifferOnlyBySpace()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "зато",
                    Suggestion = "за то",
                    Type = "misspelling",
                    Message = "Возможно, требуется раздельное написание.",
                    Offset = 0,
                    Length = 4
                }
            };


            var result = _service.Filter(errors, "зато решение стало понятнее");


            result.AcceptedErrors.Should().HaveCount(1);
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().BeEmpty();
            result.AcceptedErrors[0].Suggestion.Should().Be("за то");
        }

        [Fact]
        public void Filter_ShouldAcceptHyphenCorrection_WhenFragmentAndSuggestionDifferOnlyByHyphen()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "по настоящему",
                    Suggestion = "по-настоящему",
                    Type = "misspelling",
                    Message = "Возможно, требуется дефис.",
                    Offset = 0,
                    Length = 13
                }
            };


            var result = _service.Filter(errors, "по настоящему полезный инструмент");


            result.AcceptedErrors.Should().HaveCount(1);
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().BeEmpty();
        }

        [Fact]
        public void Filter_ShouldRejectBadSuggestionForHyphenWord_WhenSuggestionIsDifferentSingleWord()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "SMM-специалист",
                    Suggestion = "специалист",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 15
                }
            };


            var result = _service.Filter(errors, "SMM-специалист подготовил публикацию.");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().HaveCount(1);
        }

        [Fact]
        public void Filter_ShouldAcceptTypicalTsyaCorrection()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "нравиться",
                    Suggestion = "нравится",
                    Type = "misspelling",
                    Message = "Возможно, ошибка в написании глагола.",
                    Offset = 10,
                    Length = 9
                }
            };


            var result = _service.Filter(errors, "Пользователю нравиться новый формат.");


            result.AcceptedErrors.Should().HaveCount(1);
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().BeEmpty();
            result.AcceptedErrors[0].Fragment.Should().Be("нравиться");
            result.AcceptedErrors[0].Suggestion.Should().Be("нравится");
        }

        [Fact]
        public void Filter_ShouldAcceptObviousSingleWordTypo()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "тексь",
                    Suggestion = "текст",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 5
                }
            };


            var result = _service.Filter(errors, "тексь публикации был изменен");


            result.AcceptedErrors.Should().HaveCount(1);
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().BeEmpty();
        }

        [Fact]
        public void Filter_ShouldRejectSemanticallyWeirdReplacement_WhenWordsAreTooDifferent()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "кот",
                    Suggestion = "маркетинг",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 3
                }
            };


            var result = _service.Filter(errors, "кот используется в примере текста");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().BeEmpty();
            result.RejectedErrors.Should().HaveCount(1);
        }

        [Fact]
        public void Filter_ShouldReturnSuspicious_WhenLooksLikeLexicalSubstitution()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "финтехом",
                    Suggestion = "физтехом",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 8
                }
            };


            var result = _service.Filter(errors, "финтехом интересуются многие студенты");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().HaveCount(1);
            result.RejectedErrors.Should().BeEmpty();
        }

        [Fact]
        public void Filter_ShouldReturnSuspicious_WhenErrorTypeIsUnknownAndMessageDoesNotMentionPunctuation()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "сомнительный",
                    Suggestion = "сомнительное",
                    Type = "grammar",
                    Message = "Возможно, требуется проверка согласования.",
                    Offset = 0,
                    Length = 13
                }
            };


            var result = _service.Filter(errors, "сомнительный ошибка в предложении");


            result.AcceptedErrors.Should().BeEmpty();
            result.SuspiciousErrors.Should().HaveCount(1);
            result.RejectedErrors.Should().BeEmpty();
        }

        [Fact]
        public void Filter_ShouldProcessMultipleErrorsIntoDifferentGroups()
        {

            var errors = new List<GrammarErrorDto>
            {
                new()
                {
                    Fragment = "тексь",
                    Suggestion = "текст",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 0,
                    Length = 5
                },
                new()
                {
                    Fragment = "SMM",
                    Suggestion = "сам",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 10,
                    Length = 3
                },
                new()
                {
                    Fragment = "мaркетинг",
                    Suggestion = "маркетинг",
                    Type = "misspelling",
                    Message = "Возможно, слово написано с ошибкой.",
                    Offset = 20,
                    Length = 9
                }
            };


            var result = _service.Filter(errors, "тексь для SMM и мaркетинга");


            result.AcceptedErrors.Should().HaveCount(1);
            result.AcceptedErrors[0].Fragment.Should().Be("тексь");

            result.RejectedErrors.Should().HaveCount(1);
            result.RejectedErrors[0].Fragment.Should().Be("SMM");

            result.SuspiciousErrors.Should().HaveCount(1);
            result.SuspiciousErrors[0].Fragment.Should().Be("мaркетинг");
        }
    }
}