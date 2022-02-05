using System;
using System.Collections.Generic;
using System.Globalization;
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

        private const string POKEMON_NUMBER_OUTPUT_FORMAT = "000";

        private readonly List<string> _pokemonNames = new List<string>();
        private readonly Dictionary<string, IEnumerable<string>> _triviaCache = new Dictionary<string, IEnumerable<string>>();
        private Random _random = new Random();

        public void Initialize()
        {
            DownloadPokemonNames();
        }

        private bool IsValidPokemonNumber(int number) => number > 0 && number <= _pokemonNames.Count;

        private bool ValidatePokemonName(string name) => ValidatePokemonName(name, out _);

        private bool ValidatePokemonName(string name, out string displayName)
        {
            displayName = string.Empty;

            string pokemonName = _pokemonNames.FirstOrDefault(pn => string.Compare(pn, name, CultureInfo.CurrentCulture, CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreCase) == 0);

            if (!string.IsNullOrWhiteSpace(pokemonName))
            {
                displayName = pokemonName;
                return true;
            }

            return false;
        }

        private int GetPokemonNumberByName(string pokemonName)
        {
            return ValidatePokemonName(pokemonName, out string displayName)
                ? _pokemonNames.IndexOf(displayName)
                : -1;
        }

        private string GetPokemonNameByNumber(int pokemonNumber) => IsValidPokemonNumber(pokemonNumber) ? _pokemonNames[pokemonNumber - 1] : string.Empty;

        private string GetRandomPokemonName() => _pokemonNames.Any() ? _pokemonNames[_random.Next(_pokemonNames.Count)] : string.Empty;

        private string BuildPokewikiUrl(string pokemonName) => $"{POKEWIKI_BASE_URL}/{pokemonName}";

        private string GetRandomPokewikiUrl()
        {
            string randomPokemonName = GetRandomPokemonName();
            string url = BuildPokewikiUrl(randomPokemonName);

            return url;
        }

        private IEnumerable<string> GetTriviaForPokemon(string pokemonName)
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
                        string triviaLine = Regex.Replace(match.Value, "<(.|\\n)*?>", string.Empty);
                        triviaLine = Regex.Replace(triviaLine, "&[a-zA-Z0-9]+?;", string.Empty);

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

        public string GetRandomMessage(string command, string[] parameters)
        {
            string pokemonNameParam = string.Empty;

            if (parameters != null && parameters.Any())
            {
                pokemonNameParam = parameters[0].Trim().Trim('-', '_', '+', '"', '\'');

                if (int.TryParse(pokemonNameParam, out int pokemonNumberParam))
                {
                    if (!IsValidPokemonNumber(pokemonNumberParam))
                        return "Sorry, {USER}! Aber es gibt leider kein Pokémon mit der Nummer " + $"{pokemonNumberParam.ToString(POKEMON_NUMBER_OUTPUT_FORMAT)}.";

                    pokemonNameParam = GetPokemonNameByNumber(pokemonNumberParam);
                }
                else if (!ValidatePokemonName(pokemonNameParam, out pokemonNameParam))
                {
                    return "Sorry, {USER}! Aber ein Pokémon mit dem Namen '" + pokemonNameParam + "' kenne ich nicht. :(";
                }

            }

            string pokemonName = string.Empty;

            IEnumerable<string> triviaSequence = Array.Empty<string>();

            int tryPokemonCount = 0;

            while (!triviaSequence.Any() && tryPokemonCount++ < MAX_TRIES_POKEMON)
            {
                pokemonName = string.IsNullOrWhiteSpace(pokemonNameParam) ? GetRandomPokemonName() : pokemonNameParam;

                int tryTriviaCount = 0;

                while (!triviaSequence.Any() && tryTriviaCount++ < MAX_TRIES_TRIVIA)
                {
                    triviaSequence = GetTriviaForPokemon(pokemonName);
                }

                if (!string.IsNullOrWhiteSpace(pokemonNameParam))
                    break;
            }

            if (!triviaSequence.Any())
            {
                return string.IsNullOrWhiteSpace(pokemonNameParam)
                    ? "{USER}, ich kann gerade leider keine Trivia abrufen. Tut mir leid :( Versuch es später nochmal!"
                    : "{USER}, zu diesem Pokémon fällt mir leider überhaupt nichts Interessantes ein. Tut mir leid :(";
            }

            string trivia = triviaSequence.ElementAt(_random.Next(triviaSequence.Count()));

            int pokemonOutputNumber = GetPokemonNumberByName(pokemonName);

            return "{USER}, " + $"ein interessanter Fakt zu {pokemonName} (#{pokemonOutputNumber.ToString(POKEMON_NUMBER_OUTPUT_FORMAT)}): {trivia}";

        }
    }
}
