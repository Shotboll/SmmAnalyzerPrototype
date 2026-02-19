using System.Text;
using System.Text.Json;

namespace SmmAnalyzerPrototype.Data
{
    public class LlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;

        public LlmService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _model = configuration["Llm:Model"] ?? "saiga_yandexgpt_8b";
        }

        public async Task<string> AnalyzePostAsync(string postText)
        {
            var prompt = $@"
                Ты — SMM-модератор. Проверяй посты на стиль, тон, жаргон, оскорбления, политику, рекламу, стиль общения.
                Отвечай в формате:
                - Нарушения: []
                - Рекомендации по исправлению(если есть): ...
                - Оценка: [подходит/нужно исправить/неподходит]

                Проверь:
                Пост: ""{postText}""
                ";

            var requestBody = new
            {
                model = _model,
                prompt = prompt,
                stream = false
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("http://localhost:11434/api/generate", content);
            var result = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(result);
            return doc.RootElement.GetProperty("response").GetString();
        }
    }
}
