namespace RAGTEST.Models
{
    public class LoadingOverlayViewModel
    {
        public string OverlayId { get; set; } = "loadingOverlay";
        public string MessageId { get; set; } = "loadingMessage";
        public string Title { get; set; } = "Идёт обработка";
        public string InitialMessage { get; set; } = "Пожалуйста, подождите...";
        public string Hint { get; set; } = "Операция может занять некоторое время.";
    }
}
