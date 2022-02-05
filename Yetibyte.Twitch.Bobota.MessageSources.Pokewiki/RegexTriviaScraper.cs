using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Yetibyte.Twitch.Bobota.MessageSources.Pokewiki
{
    public class RegexTriviaScraper : ITriviaScraper
    {
        private const string TRIVIA_OUTER_REGEX = "id=\"Trivia\"(.|\\n)*?<ul>(.|\\n)*?</ul>";
        private const string TRIVIA_INNER_REGEX = "<li>(.|\\n)*?</li>";

        public IEnumerable<string> GetPokemonTrivia(string pokewikiUrl)
        {
            List<string> trivia = new List<string>();

            string htmlContent = string.Empty;

            try
            {
                using (HttpClient httpClient = new HttpClient())
                {
                    var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, new Uri(pokewikiUrl)));

                    using (Stream stream = response.Content.ReadAsStream())
                    {
                        using StreamReader streamReader = new StreamReader(stream);
                        htmlContent = streamReader.ReadToEnd();
                    }
                }

                Match triviaOuterMatch = Regex.Match(htmlContent, TRIVIA_OUTER_REGEX);

                if (triviaOuterMatch.Success)
                {
                    MatchCollection triviaMatches = Regex.Matches(triviaOuterMatch.Value, TRIVIA_INNER_REGEX);

                    foreach (Match match in triviaMatches)
                    {
                        string triviaLine = Regex.Replace(match.Value, "<(.|\\n)*?>", string.Empty);
                        triviaLine = Regex.Replace(triviaLine, "&[a-zA-Z0-9]+?;", string.Empty);

                        trivia.Add(triviaLine);
                    }
                }
            }
            catch
            {

            }

            return trivia;
        }
    }
}
