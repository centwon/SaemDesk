using System.Threading.Tasks;

namespace SaemDesk.Services.Platform;

public interface IFilePickerService
{
    Task<string?> OpenFileAsync(params string[] extensions);
    Task<string?> SaveFileAsync(string defaultName, params string[] extensions);
    Task<string?> OpenFolderAsync();
}
