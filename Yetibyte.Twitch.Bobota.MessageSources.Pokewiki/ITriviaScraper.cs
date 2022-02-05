using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yetibyte.Twitch.Bobota.MessageSources.Pokewiki
{
    interface ITriviaScraper
    {
        IEnumerable<string> GetPokemonTrivia(string pokewikiUrl);
    }
}
