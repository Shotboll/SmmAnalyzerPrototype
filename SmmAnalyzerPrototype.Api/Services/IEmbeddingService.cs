namespace SmmAnalyzerPrototype.Api.Services
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text, bool isQuery = false);
    }
}
