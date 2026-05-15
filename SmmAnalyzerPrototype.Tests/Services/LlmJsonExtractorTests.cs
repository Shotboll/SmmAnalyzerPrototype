using FluentAssertions;
using SmmAnalyzerPrototype.Api.Services;

namespace SmmAnalyzerPrototype.Tests.Services
{
    public class LlmJsonExtractorTests
    {
        [Fact]
        public void ExtractJson_ShouldReturnEmptyArray_WhenResponseIsEmpty()
        {
            var result = LlmJsonExtractor.ExtractJson("");

            result.Should().Be("[]");
        }

        [Fact]
        public void ExtractJson_ShouldReturnEmptyArray_WhenResponseIsWhitespace()
        {
            var result = LlmJsonExtractor.ExtractJson("   \n   ");

            result.Should().Be("[]");
        }

        [Fact]
        public void ExtractJson_ShouldExtractObjectFromJsonCodeFence()
        {
            var response = """
            ```json
            {
              "hasViolations": false,
              "violations": [],
              "comment": "Нарушений не найдено"
            }
            ```
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("""
            {
              "hasViolations": false,
              "violations": [],
              "comment": "Нарушений не найдено"
            }
            """);
        }

        [Fact]
        public void ExtractJson_ShouldExtractArrayFromJsonCodeFence()
        {
            var response = """
            ```json
            [
              {
                "index": 0,
                "explanation": "Ошибка в слове",
                "hint": "Исправьте написание"
              }
            ]
            ```
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("""
            [
              {
                "index": 0,
                "explanation": "Ошибка в слове",
                "hint": "Исправьте написание"
              }
            ]
            """);
        }

        [Fact]
        public void ExtractJson_ShouldExtractJsonFromCodeFenceWithoutLanguage()
        {
            var response = """
            ```
            {
              "assessment": "соответствует",
              "summary": "Текст подходит",
              "strengths": [],
              "issues": [],
              "recommendations": []
            }
            ```
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("""
            {
              "assessment": "соответствует",
              "summary": "Текст подходит",
              "strengths": [],
              "issues": [],
              "recommendations": []
            }
            """);
        }

        [Fact]
        public void ExtractJson_ShouldExtractEmbeddedObject_WhenModelAddsTextBeforeAndAfterJson()
        {
            var response = """
            Вот результат анализа:
            {
              "hasViolations": true,
              "violations": [
                {
                  "ruleNumber": 1,
                  "ruleShort": "Запрещенная тема",
                  "matchedText": "пример",
                  "explanation": "Нарушение правила"
                }
              ],
              "comment": "Есть нарушение"
            }
            Анализ завершен.
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("""
            {
              "hasViolations": true,
              "violations": [
                {
                  "ruleNumber": 1,
                  "ruleShort": "Запрещенная тема",
                  "matchedText": "пример",
                  "explanation": "Нарушение правила"
                }
              ],
              "comment": "Есть нарушение"
            }
            """);
        }

        [Fact]
        public void ExtractJson_ShouldExtractEmbeddedArray_WhenModelAddsTextBeforeArray()
        {
            var response = """
            Ответ:
            [
              {
                "index": 0,
                "explanation": "Пояснение",
                "hint": "Совет"
              }
            ]
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("""
            [
              {
                "index": 0,
                "explanation": "Пояснение",
                "hint": "Совет"
              }
            ]
            """);
        }

        [Fact]
        public void ExtractJson_ShouldReturnOriginalObject_WhenResponseAlreadyLooksLikeJsonObject()
        {
            var response = """
            {
              "hasViolations": false,
              "violations": [],
              "comment": "Нарушений нет"
            }
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("""
            {
              "hasViolations": false,
              "violations": [],
              "comment": "Нарушений нет"
            }
            """);
        }

        [Fact]
        public void ExtractJson_ShouldReturnOriginalArray_WhenResponseAlreadyLooksLikeJsonArray()
        {
            var response = """
            [
              {
                "index": 0,
                "explanation": "Ошибка",
                "hint": "Совет"
              }
            ]
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("""
            [
              {
                "index": 0,
                "explanation": "Ошибка",
                "hint": "Совет"
              }
            ]
            """);
        }

        [Fact]
        public void ExtractJson_ShouldReturnEmptyArray_WhenResponseDoesNotContainJson()
        {
            var response = "Модель не смогла сформировать корректный ответ.";

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Be("[]");
        }

        [Fact]
        public void ExtractJson_ShouldHandleNestedJsonObjects()
        {
            var response = """
            Ответ модели:
            {
              "hasViolations": true,
              "violations": [
                {
                  "ruleNumber": 2,
                  "ruleShort": "Правило",
                  "matchedText": "фрагмент",
                  "explanation": "Объект внутри массива обработан корректно"
                }
              ],
              "comment": "Проверка завершена"
            }
            Конец ответа.
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Contain("\"hasViolations\": true");
            result.Should().Contain("\"violations\"");
            result.Should().Contain("\"ruleNumber\": 2");
            result.Should().Contain("\"comment\": \"Проверка завершена\"");
            result.Trim().Should().StartWith("{");
            result.Trim().Should().EndWith("}");
        }

        [Fact]
        public void ExtractJson_ShouldHandleBracesInsideStringValue()
        {
            var response = """
            Результат:
            {
              "hasViolations": false,
              "violations": [],
              "comment": "Фигурные скобки внутри строки: {пример}"
            }
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Contain("\"comment\": \"Фигурные скобки внутри строки: {пример}\"");
            result.Trim().Should().StartWith("{");
            result.Trim().Should().EndWith("}");
        }

        [Fact]
        public void ExtractJson_ShouldHandleEscapedQuotesInsideStringValue()
        {
            var response = """
            Ответ:
            {
              "hasViolations": false,
              "violations": [],
              "comment": "Фрагмент \"пример\" обработан"
            }
            """;

            var result = LlmJsonExtractor.ExtractJson(response);

            result.Should().Contain("\\\"пример\\\"");
            result.Trim().Should().StartWith("{");
            result.Trim().Should().EndWith("}");
        }

        [Fact]
        public void ExtractJsonGrammar_ShouldReturnEmptyArray_WhenResponseIsEmpty()
        {
            var result = LlmJsonExtractor.ExtractJsonGrammar("");

            result.Should().Be("[]");
        }

        [Fact]
        public void ExtractJsonGrammar_ShouldExtractJsonCodeFence()
        {
            var response = """
            ```json
            [
              {
                "index": 0,
                "explanation": "Ошибка",
                "hint": "Исправьте"
              }
            ]
            ```
            """;

            var result = LlmJsonExtractor.ExtractJsonGrammar(response);

            result.Should().Be("""
            [
              {
                "index": 0,
                "explanation": "Ошибка",
                "hint": "Исправьте"
              }
            ]
            """);
        }

        [Fact]
        public void ExtractJsonGrammar_ShouldExtractCodeFenceWithoutLanguage()
        {
            var response = """
            ```
            [
              {
                "index": 0,
                "explanation": "Ошибка",
                "hint": "Исправьте"
              }
            ]
            ```
            """;

            var result = LlmJsonExtractor.ExtractJsonGrammar(response);

            result.Should().Be("""
            [
              {
                "index": 0,
                "explanation": "Ошибка",
                "hint": "Исправьте"
              }
            ]
            """);
        }

        [Fact]
        public void ExtractJsonGrammar_ShouldReturnTrimmedResponse_WhenThereIsNoCodeFence()
        {
            var response = """
              
              [{"index":0,"explanation":"Ошибка","hint":"Совет"}]
              
            """;

            var result = LlmJsonExtractor.ExtractJsonGrammar(response);

            result.Should().Be("""[{"index":0,"explanation":"Ошибка","hint":"Совет"}]""");
        }
    }
}