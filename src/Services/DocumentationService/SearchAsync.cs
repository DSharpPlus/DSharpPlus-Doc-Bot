using System.Collections.Generic;
using System.Linq;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using FuzzySharp;
using Microsoft.Extensions.Configuration;

namespace DSharpPlus.DocBot.Services
{
    public sealed partial class DocumentationService : IDocumentationService
    {
        public IEnumerable<Page> Search(string searchQuery)
        {
            searchQuery = searchQuery.ToLowerInvariant();
            List<(int, Page)> results = new(Configuration.GetValue("documentation:search:max_results", 75));
            foreach ((string key, Page page) in Documentation)
            {
                // Make sure the results aren't too large.
                if (results.Count >= results.Capacity)
                {
                    return results.OrderBy(x => x.Item1).ThenByDescending(x => x.Item2.MemberType).Select(x => x.Item2);
                }

                // dsharpplus.ansicolor == dsharpplus.ansicolor
                if (key == searchQuery)
                {
                    results.Add((110, page));
                    continue;
                }
                // ansicolor == "dsharpplus.ansicolor".Split('.')[1]
                else if (!searchQuery.Contains('.') && key.Split('.')[1] == searchQuery)
                {
                    results.Add((105, page));
                    continue;
                }
                // "dsharpplus.entities.discorduser".Contains("discorduser")
                else if (key.Contains(searchQuery))
                {
                    results.Add((100, page));
                    continue;
                }

                // Fuzzy matching. Not the best but kinda works.
                int score = Fuzz.PartialTokenAbbreviationRatio(key, searchQuery);
                if (score >= 90)
                {
                    results.Add((score, page));
                }
            };

            // Sort the results by score and MemberType (Type, Property, Method, etc) and return them.
            return results.OrderBy(x => x.Item1).ThenByDescending(x => x.Item2.MemberType).Select(x => x.Item2);
        }
    }
}
