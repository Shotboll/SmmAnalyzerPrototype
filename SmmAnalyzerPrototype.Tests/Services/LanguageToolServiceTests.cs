using FluentAssertions;
using SmmAnalyzerPrototype.Api.Services;
using System.Net;
using System.Text;

namespace SmmAnalyzerPrototype.Tests.Services
{
    public class LanguageToolServiceTests
    {
        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public List<HttpRequestMessage> Requests { get; } = new();

            public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(_handler(request));
            }
        }

        private static LanguageToolService CreateService(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK, FakeHttpMessageHandler? handler = null)
        {
            handler ??= new FakeHttpMessageHandler(_ => new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

            var httpClient = new HttpClient(handler);
            return new LanguageToolService(httpClient);
        }

        [Fact]
        public async Task CheckTextAsync_ShouldReturnGrammarErrors_WhenLanguageToolReturnsMatches()
        {

            var text = "Это тестовый тексь для проверки.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": 13,
                  "length": 5,
                  "message": "Возможно, слово написано с ошибкой.",
                  "replacements": [
                    { "value": "текст" }
                  ],
                  "rule": {
                    "issueType": "misspelling"
                  }
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().HaveCount(1);

            result[0].Fragment.Should().Be("тексь");
            result[0].Suggestion.Should().Be("текст");
            result[0].Type.Should().Be("misspelling");
            result[0].Offset.Should().Be(13);
            result[0].Length.Should().Be(5);
            result[0].Message.Should().Be("Возможно, слово написано с ошибкой.");
        }

        [Fact]
        public async Task CheckTextAsync_ShouldReturnMultipleErrors_WhenLanguageToolReturnsSeveralMatches()
        {

            var text = "Тексь плохой и по настоящему странный.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": 0,
                  "length": 5,
                  "message": "Возможно, слово написано с ошибкой.",
                  "replacements": [
                    { "value": "Текст" }
                  ],
                  "rule": {
                    "issueType": "misspelling"
                  }
                },
                {
                  "offset": 15,
                  "length": 13,
                  "message": "Возможно, требуется дефис.",
                  "replacements": [
                    { "value": "по-настоящему" }
                  ],
                  "rule": {
                    "issueType": "typographical"
                  }
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().HaveCount(2);

            result[0].Fragment.Should().Be("Тексь");
            result[0].Suggestion.Should().Be("Текст");
            result[0].Type.Should().Be("misspelling");

            result[1].Fragment.Should().Be("по настоящему");
            result[1].Suggestion.Should().Be("по-настоящему");
            result[1].Type.Should().Be("typographical");
        }

        [Fact]
        public async Task CheckTextAsync_ShouldReturnEmptyList_WhenMatchesIsEmpty()
        {

            var text = "Текст без ошибок.";

            var responseJson = """
            {
              "matches": []
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckTextAsync_ShouldReturnEmptyList_WhenMatchesIsMissing()
        {

            var text = "Текст без ошибок.";

            var responseJson = """
            {
              "software": {
                "name": "LanguageTool"
              }
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckTextAsync_ShouldSkipMatch_WhenOffsetIsNegative()
        {

            var text = "Текст проверки.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": -1,
                  "length": 5,
                  "message": "Некорректное смещение.",
                  "replacements": [
                    { "value": "Текст" }
                  ],
                  "rule": {
                    "issueType": "misspelling"
                  }
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckTextAsync_ShouldSkipMatch_WhenLengthIsZero()
        {

            var text = "Текст проверки.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": 0,
                  "length": 0,
                  "message": "Некорректная длина.",
                  "replacements": [
                    { "value": "Текст" }
                  ],
                  "rule": {
                    "issueType": "misspelling"
                  }
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckTextAsync_ShouldSkipMatch_WhenOffsetAndLengthAreOutsideText()
        {

            var text = "Короткий текст.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": 10,
                  "length": 100,
                  "message": "Некорректные границы фрагмента.",
                  "replacements": [
                    { "value": "исправление" }
                  ],
                  "rule": {
                    "issueType": "misspelling"
                  }
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckTextAsync_ShouldUseEmptySuggestion_WhenReplacementsAreEmpty()
        {

            var text = "Это тексь.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": 4,
                  "length": 5,
                  "message": "Возможно, слово написано с ошибкой.",
                  "replacements": [],
                  "rule": {
                    "issueType": "misspelling"
                  }
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().HaveCount(1);
            result[0].Fragment.Should().Be("тексь");
            result[0].Suggestion.Should().BeEmpty();
            result[0].Type.Should().Be("misspelling");
        }

        [Fact]
        public async Task CheckTextAsync_ShouldUseGrammarType_WhenRuleIsMissing()
        {

            var text = "Это тексь.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": 4,
                  "length": 5,
                  "message": "Возможно, слово написано с ошибкой.",
                  "replacements": [
                    { "value": "текст" }
                  ],
                  "rule": null
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().HaveCount(1);
            result[0].Type.Should().Be("grammar");
        }

        [Fact]
        public async Task CheckTextAsync_ShouldUseEmptyMessage_WhenMessageIsMissing()
        {

            var text = "Это тексь.";

            var responseJson = """
            {
              "matches": [
                {
                  "offset": 4,
                  "length": 5,
                  "replacements": [
                    { "value": "текст" }
                  ],
                  "rule": {
                    "issueType": "misspelling"
                  }
                }
              ]
            }
            """;

            var service = CreateService(responseJson);


            var result = await service.CheckTextAsync(text);


            result.Should().HaveCount(1);
            result[0].Message.Should().BeEmpty();
        }

        [Fact]
        public async Task CheckTextAsync_ShouldSendTextLanguageAndEnabledOnlyParameters()
        {

            var text = "Проверяемый текст.";

            var responseJson = """
            {
              "matches": []
            }
            """;

            HttpRequestMessage? capturedRequest = null;
            string? capturedContent = null;

            var handler = new FakeHttpMessageHandler(request =>
            {
                capturedRequest = request;
                capturedContent = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(handler);
            var service = new LanguageToolService(httpClient);


            var result = await service.CheckTextAsync(text, "ru");


            result.Should().BeEmpty();

            capturedRequest.Should().NotBeNull();
            capturedRequest!.Method.Should().Be(HttpMethod.Post);
            capturedRequest.RequestUri!.ToString().Should().Be("http://localhost:8010/v2/check");

            capturedContent.Should().NotBeNull();
            capturedContent.Should().Contain("text=");
            capturedContent.Should().Contain("language=ru");
            capturedContent.Should().Contain("enabledOnly=false");
        }

        [Fact]
        public async Task CheckTextAsync_ShouldThrowHttpRequestException_WhenLanguageToolReturnsServerError()
        {

            var text = "Текст проверки.";

            var service = CreateService(
                responseJson: "Internal Server Error",
                statusCode: HttpStatusCode.InternalServerError);


            var act = async () => await service.CheckTextAsync(text);


            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}