using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Yetibyte.Twitch.Bobota.MessageSources.Pokewiki
{
    public class HtmlAgilityPackTriviaScraper : ITriviaScraper
    {
        public IEnumerable<string> GetPokemonTrivia(string pokewikiUrl)
        {
            List<string> trivia = new List<string>();

            HtmlWeb web = new HtmlWeb();

            HtmlDocument htmlDoc = web.Load(pokewikiUrl);

			var triviaHeaderNode = htmlDoc.DocumentNode.Descendants().FirstOrDefault(n => n.Id == "Trivia")?.ParentNode;

			var currNode = triviaHeaderNode;

			while (currNode != null && currNode.Name != "ul")
			{
				currNode = currNode.NextSibling;
			}

			if (currNode is null)
				return Array.Empty<string>();

			foreach (var listItemNode in currNode.ChildNodes.Where(c => c.Name == "li"))
			{
				string triviaLine = listItemNode.InnerText ?? string.Empty;
				triviaLine = Regex.Replace(triviaLine, "&[a-zA-Z0-9#]+?;", string.Empty);

				trivia.Add(triviaLine);
			}

			return trivia;

        }
    }
}
