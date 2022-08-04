using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus.DocBot.Types;

namespace DSharpPlus.DocBot.Interfaces
{
    public interface IDocumentationService
    {
        IEnumerable<Page> Search(string searchQuery);
        string? GetCurrentVersion();
        Task ReloadAsync();
    }
}