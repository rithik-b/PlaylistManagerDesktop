using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace PlaylistManager.Models;

public interface ILevelData
{
    /// <summary>
    /// Name of the song
    /// </summary>
    public string SongName { get; }
        
    /// <summary>
    /// Sub name of the song
    /// </summary>
    public string SongSubName { get; }
        
    /// <summary>
    /// Artist of the song
    /// </summary>
    public string SongAuthorName { get; }

    /// <summary>
    /// A dictionary that maps from Characteristic to a List of Difficulties in that Characteristic
    /// </summary>
    public Dictionary<string, List<Difficulty>> Difficulties { get; }

    /// <summary>
    /// Asynchronously loads and parses the cover image of a level
    /// </summary>
    public Task<Bitmap?> GetCoverImageAsync(CancellationToken? cancellationToken = null);
}