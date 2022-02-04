using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Yetibyte.Twitch.Bobota;

namespace Yetibyte.Twitch.Bobota.MessageSources.Pokewiki
{
    public class PokewikiMessageSource : IMessageSource
    {
        private const string POKEMON_NAMES_CSV_URL = @"https://raw.githubusercontent.com/PokeAPI/pokeapi/master/data/v2/csv/pokemon_species_names.csv";
        private const string POKEWIKI_BASE_URL = @"https://www.pokewiki.de";

        private const int MAX_TRIES_TRIVIA = 3;
        private const int MAX_TRIES_POKEMON = 10;

        private const string LANGUAGE_ID_GERMAN = "6";

        private const string TRIVIA_OUTER_REGEX = "id=\"Trivia\"(.|\\n)*?<ul>(.|\\n)*?</ul>";
        private const string TRIVIA_INNER_REGEX = "<li>(.|\\n)*?</li>";

        private readonly List<string> _pokemonNames = new List<string>();
        private readonly Dictionary<string, IEnumerable<string>> _triviaCache = new Dictionary<string, IEnumerable<string>>();
        private Random _random = new Random();

        public void Initialize()
        {
            DownloadPokemonNames();
        }

        private string GetRandomPokemonName() => _pokemonNames.Any() ? _pokemonNames[_random.Next(_pokemonNames.Count)] : string.Empty;

        public string BuildPokewikiUrl(string pokemonName) => $"{POKEWIKI_BASE_URL}/{pokemonName}";

        public string GetRandomPokewikiUrl()
        {
            string randomPokemonName = GetRandomPokemonName();
            string url = BuildPokewikiUrl(randomPokemonName);

            return url;
        }

        public IEnumerable<string> GetTriviaForPokemon(string pokemonName)
        {
            if (_triviaCache.ContainsKey(pokemonName))
            {
                return _triviaCache[pokemonName];
            }

            List<string> trivia = new List<string>();

            string pokewikiUrl = BuildPokewikiUrl(pokemonName);

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
                        string triviaLine = Regex.Replace(match.Value, "<(.|\\n)*?>", String.Empty);

                        trivia.Add(triviaLine);
                    }
                }
            }
            catch
            {

            }

            if (trivia.Any())
            {
                _triviaCache[pokemonName] = trivia;
            }

            return trivia;
        }

        private void DownloadPokemonNames()
        {
            _pokemonNames.Clear();

            using (HttpClient httpClient = new HttpClient())
            {
                var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, new Uri(POKEMON_NAMES_CSV_URL)));

                using (Stream stream = response.Content.ReadAsStream())
                {
                    using StreamReader streamReader = new StreamReader(stream);
                    string csvLine = null;

                    while((csvLine = streamReader.ReadLine()) != null) {

                        string[] csvColumns = csvLine.Split(',');

                        if (csvColumns is not null && csvColumns.Length >= 3 && csvColumns[1] == LANGUAGE_ID_GERMAN)
                        {
                            string pokemonName = csvColumns[2];

                            _pokemonNames.Add(pokemonName);
                        }

                    }

                }
            }

        }

        public string GetRandomMessage()
        {
            string pokemonName = string.Empty;

            IEnumerable<string> triviaSequence = Array.Empty<string>();

            int tryPokemonCount = 0;

            while (!triviaSequence.Any() && tryPokemonCount++ < MAX_TRIES_POKEMON)
            {
                pokemonName = GetRandomPokemonName();

                int tryTriviaCount = 0;

                while (!triviaSequence.Any() && tryTriviaCount++ < MAX_TRIES_TRIVIA)
                {
                    triviaSequence = GetTriviaForPokemon(pokemonName);
                }
            }

            if (!triviaSequence.Any())
            {
                return "{USER}, ich kann gerade leider keine Trivia abrufen. Tut mir leid :( Versuch es später nochmal!";
            }

            string trivia = triviaSequence.ElementAt(_random.Next(triviaSequence.Count()));

            return "{USER}, " + $"ein interessanter Fakt zu {pokemonName}: {trivia}";

        }
    }
}
