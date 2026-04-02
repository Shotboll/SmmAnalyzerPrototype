using SmmAnalyzerPrototype.Data.Models.DTO.Post;
using System.Text.RegularExpressions;

namespace SmmAnalyzerPrototype.Api.Services
{
    public class GrammarFalsePositiveFilterService
    {
        public GrammarFilterResult Filter(List<GrammarErrorDto> errors, string originalText)
        {
            var result = new GrammarFilterResult();

            foreach (var error in errors)
            {
                var decision = Classify(error, originalText);

                switch (decision)
                {
                    case GrammarErrorDecision.Accept:
                        result.AcceptedErrors.Add(error);
                        break;

                    case GrammarErrorDecision.Suspicious:
                        result.SuspiciousErrors.Add(error);
                        break;

                    case GrammarErrorDecision.Reject:
                        result.RejectedErrors.Add(error);
                        break;
                }
            }

            return result;
        }

        private GrammarErrorDecision Classify(GrammarErrorDto error, string originalText)
        {
            var originalFragment = error.Fragment?.Trim() ?? string.Empty;
            var originalSuggestion = error.Suggestion?.Trim() ?? string.Empty;

            var fragment = Normalize(error.Fragment);
            var suggestion = Normalize(error.Suggestion);
            var type = Normalize(error.Type);
            var message = Normalize(error.Message);

            if (string.IsNullOrWhiteSpace(fragment))
                return GrammarErrorDecision.Reject;

            if (IsPunctuationLike(error))
                return GrammarErrorDecision.Accept;

            if (type == "misspelling")
            {
                if (string.IsNullOrWhiteSpace(suggestion))
                    return GrammarErrorDecision.Reject;

                if (LooksLikeAbbreviationOrTechToken(originalFragment))
                    return GrammarErrorDecision.Reject;

                if (ContainsMixedLatinAndCyrillic(fragment))
                    return GrammarErrorDecision.Suspicious;

                if (LooksLikeBadSuggestionForHyphenWord(fragment, suggestion))
                    return GrammarErrorDecision.Reject;

                if (!fragment.Contains(' ') && !fragment.Contains('-') && suggestion.Contains(' '))
                {
                    if (IsSpacingOrHyphenCorrection(fragment, suggestion) &&
                        MentionsHyphenOrCommaOrSpacing(message))
                    {
                        return GrammarErrorDecision.Accept;
                    }

                    return GrammarErrorDecision.Reject;
                }

                if (IsSpacingOrHyphenCorrection(fragment, suggestion))
                    return GrammarErrorDecision.Accept;

                if (LooksSemanticallyWeirdReplacement(fragment, suggestion))
                    return GrammarErrorDecision.Reject;

                if (IsLikelyRealMisspelling(fragment, suggestion))
                {
                    if (IsSafeAutoAcceptMisspelling(fragment, suggestion, message))
                        return GrammarErrorDecision.Accept;

                    if (IsObviousSingleWordTypo(fragment, suggestion))
                        return GrammarErrorDecision.Accept;

                    if (LooksLikeLexicalSubstitution(fragment, suggestion))
                        return GrammarErrorDecision.Suspicious;

                    return GrammarErrorDecision.Suspicious;
                }

                return GrammarErrorDecision.Suspicious;
            }

            if (MentionsHyphenOrCommaOrSpacing(message))
                return GrammarErrorDecision.Accept;

            return GrammarErrorDecision.Suspicious;
        }

        private static bool LooksLikeLexicalSubstitution(string fragment, string suggestion)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            if (fragment.Contains(' ') || fragment.Contains('-') || suggestion.Contains(' ') || suggestion.Contains('-'))
                return false;

            int distance = LevenshteinDistance(fragment, suggestion);
            if (distance > 2)
                return false;

            int prefix = CommonPrefixLength(fragment, suggestion);
            int suffix = CommonSuffixLength(fragment, suggestion);

            // Похожие слова, но изменена внутренняя часть слова
            // финтехом -> физтехом
            if (fragment.Length >= 7 &&
                suggestion.Length >= 7 &&
                prefix >= 2 &&
                suffix >= 4)
            {
                if (!IsTypicalEndingCorrection(fragment, suggestion))
                    return true;
            }

            return false;
        }

        private static bool IsTypicalEndingCorrection(string fragment, string suggestion)
        {
            if (fragment.EndsWith("ться") && suggestion.EndsWith("тся"))
                return true;

            if (fragment.EndsWith("тся") && suggestion.EndsWith("ться"))
                return true;

            return false;
        }

        private static bool IsObviousSingleWordTypo(string fragment, string suggestion)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            if (fragment.Contains(' ') || fragment.Contains('-') || suggestion.Contains(' ') || suggestion.Contains('-'))
                return false;

            int distance = LevenshteinDistance(fragment, suggestion);
            if (distance != 1)
                return false;

            int prefix = CommonPrefixLength(fragment, suggestion);
            int suffix = CommonSuffixLength(fragment, suggestion);

            // Только короткие слова
            if (fragment.Length <= 7 && suggestion.Length <= 7 && prefix >= 2 && suffix >= 3)
                return true;

            return false;
        }

        private static bool IsSafeAutoAcceptMisspelling(string fragment, string suggestion, string message)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            // Сообщение явно про дефис/запятую/пробел
            if (MentionsHyphenOrCommaOrSpacing(message))
                return true;

            // Случаи с пробелами и дефисами безопаснее
            if (fragment.Contains(' ') || fragment.Contains('-') || suggestion.Contains(' ') || suggestion.Contains('-'))
                return true;

            // Типовой случай ться/тся
            if (IsTypicalEndingCorrection(fragment, suggestion))
                return true;

            // Очень короткая однословная опечатка
            int distance = LevenshteinDistance(fragment, suggestion);
            if (distance == 1 && fragment.Length <= 5 && suggestion.Length <= 5)
                return true;

            return false;
        }

        private static bool IsSpacingOrHyphenCorrection(string fragment, string suggestion)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            var normalizedFragment = RemoveSpacesAndHyphens(fragment);
            var normalizedSuggestion = RemoveSpacesAndHyphens(suggestion);

            if (normalizedFragment != normalizedSuggestion)
                return false;

            if (normalizedFragment.Length <= 5)
                return true;

            bool fragmentHasSeparator = fragment.Contains(' ') || fragment.Contains('-');
            bool suggestionHasSeparator = suggestion.Contains(' ') || suggestion.Contains('-');

            if (fragmentHasSeparator && suggestionHasSeparator && normalizedFragment.Length <= 8)
                return true;

            return false;
        }

        private static string Normalize(string? value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static bool IsPunctuationLike(GrammarErrorDto error)
        {
            var type = Normalize(error.Type);
            var message = Normalize(error.Message);
            var fragment = Normalize(error.Fragment);
            var suggestion = Normalize(error.Suggestion);

            if (type.Contains("typographical"))
                return true;

            if (message.Contains("запят") ||
                message.Contains("дефис") ||
                message.Contains("пробел") ||
                message.Contains("пунктуа"))
                return true;

            bool fragmentHasSeparator = fragment.Contains(' ') || fragment.Contains('-');
            bool suggestionHasSeparator = suggestion.Contains(' ') || suggestion.Contains('-');

            var normalizedFragment = RemoveSpacesAndHyphens(fragment);
            var normalizedSuggestion = RemoveSpacesAndHyphens(suggestion);

            if (fragmentHasSeparator &&
                suggestionHasSeparator &&
                normalizedFragment == normalizedSuggestion &&
                normalizedFragment.Length <= 8)
            {
                return true;
            }

            if (fragment.Contains(' ') && suggestion.Contains(','))
                return true;

            return false;
        }

        private static bool ContainsMixedLatinAndCyrillic(string text)
        {
            bool hasLatin = text.Any(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'));
            bool hasCyrillic = text.Any(c =>
                (c >= 'А' && c <= 'я') ||
                c == 'Ё' || c == 'ё' ||
                c == 'І' || c == 'і');

            return hasLatin && hasCyrillic;
        }

        private static bool LooksLikeAbbreviationOrTechToken(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var original = text.Trim();

            // Полностью верхний регистр: LLM, API, SMM, ИИ
            if (original.Length >= 2 && original.All(c => !char.IsLetter(c) || char.IsUpper(c)))
                return true;

            // Техно-форматы: ASP.NET, CI/CD, REST_API, GPT-4
            if (original.Contains('.') || original.Contains('/') || original.Contains('\\') || original.Contains('_'))
                return true;

            // Смешение букв и цифр: GPT4, .NET8
            bool hasLetter = original.Any(char.IsLetter);
            bool hasDigit = original.Any(char.IsDigit);
            if (hasLetter && hasDigit)
                return true;

            // Аббревиатура с дефисом в начале: ИИ-решения, AI-model
            var parts = original.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0].Length >= 2 && parts[0].All(char.IsUpper))
                return true;

            return false;
        }

        private static bool LooksLikeBadSuggestionForHyphenWord(string fragment, string suggestion)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            if (!fragment.Contains('-'))
                return false;

            // Если различие только в дефисе/пробеле, это может быть нормальная коррекция
            var normalizedFragment = RemoveSpacesAndHyphens(fragment);
            var normalizedSuggestion = RemoveSpacesAndHyphens(suggestion);

            if (normalizedFragment == normalizedSuggestion)
                return false;

            // Только если дефисное слово превратилось в совсем другое слово — это плохой случай
            if (!suggestion.Contains('-') && !suggestion.Contains(' '))
                return true;

            return false;
        }

        private static bool IsSuggestionTooDifferent(string fragment, string suggestion)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            var distance = LevenshteinDistance(fragment, suggestion);
            var maxLen = Math.Max(fragment.Length, suggestion.Length);

            if (maxLen == 0)
                return false;

            double ratio = (double)distance / maxLen;

            return ratio > 0.45;
        }

        private static bool LooksSemanticallyWeirdReplacement(string fragment, string suggestion)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            if (fragment == suggestion)
                return false;

            if (!fragment.Contains(' ') && !fragment.Contains('-') && suggestion.Contains(' '))
                return true;

            int prefix = CommonPrefixLength(fragment, suggestion);
            int suffix = CommonSuffixLength(fragment, suggestion);

            if (prefix < 2 && suffix < 2)
                return true;

            return false;
        }

        private static bool IsLikelyRealMisspelling(string fragment, string suggestion)
        {
            if (string.IsNullOrWhiteSpace(fragment) || string.IsNullOrWhiteSpace(suggestion))
                return false;

            if (fragment == suggestion)
                return false;

            if (!fragment.Contains(' ') && !fragment.Contains('-') && suggestion.Contains(' '))
                return false;

            int distance = LevenshteinDistance(fragment, suggestion);
            int maxLen = Math.Max(fragment.Length, suggestion.Length);

            if (maxLen == 0)
                return false;

            double ratio = (double)distance / maxLen;

            int prefix = CommonPrefixLength(fragment, suggestion);
            int suffix = CommonSuffixLength(fragment, suggestion);

            if (ratio <= 0.35 && (prefix >= 2 || suffix >= 2))
                return true;

            return false;
        }

        private static bool MentionsHyphenOrCommaOrSpacing(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            if (message.Contains("дефис"))
                return true;

            if (message.Contains("запята"))
                return true;

            if (message.Contains("раздель"))
                return true;

            if (message.Contains("слитно"))
                return true;

            if (message.Contains("пробел"))
                return true;

            return false;
        }

        private static string RemoveSpacesAndHyphens(string input)
        {
            return input.Replace(" ", "").Replace("-", "");
        }

        private static int CommonPrefixLength(string a, string b)
        {
            int length = Math.Min(a.Length, b.Length);
            int count = 0;

            for (int i = 0; i < length; i++)
            {
                if (a[i] != b[i])
                    break;

                count++;
            }

            return count;
        }

        private static int CommonSuffixLength(string a, string b)
        {
            int i = a.Length - 1;
            int j = b.Length - 1;
            int count = 0;

            while (i >= 0 && j >= 0)
            {
                if (a[i] != b[j])
                    break;

                count++;
                i--;
                j--;
            }

            return count;
        }

        private static int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return target?.Length ?? 0;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            int[,] matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                    matrix[i, j] = Math.Min(
                        Math.Min(
                            matrix[i - 1, j] + 1,
                            matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }

        private enum GrammarErrorDecision
        {
            Accept,
            Suspicious,
            Reject
        }
    }
}