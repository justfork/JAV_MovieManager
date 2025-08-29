using MovieManager.ClassLibrary;
using MovieManager.Data;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace MovieManager.BusinessLogic
{
    public class PotPlayerService
    {
        private MovieService _movieService;

        public PotPlayerService(MovieService movieService)
        {
            _movieService = movieService;
        }

        public void BuildPlayList(string playListName, string path, List<PlayListItem> movies, string potPlayerExe, FileMode fileMode = FileMode.Create)
        {
            if (playListName.Contains("undefined"))
            { 
                playListName = playListName.Replace("undefined", $"{DateTime.Now.ToString("yyyyMMdd_hhmmss")}");
            }
            try
            {
                var movieLocations = movies.Select(x => x.MovieLocation.Split("|").Where(x => !string.IsNullOrEmpty(x)).ToList()).ToList();
                var imdbIds = movies.Select(x => x.ImdbId).ToList();
                
                // Determine playlist paths to write to
                var playlistPaths = GetPlaylistPaths(path, potPlayerExe);
                
                foreach (var playlistPath in playlistPaths)
                {
                    CreatePlaylistFile(playListName, playlistPath, movieLocations, fileMode);
                }
                
                UpdateMoviePlayCount(imdbIds);
            }
            catch(Exception ex)
            {
                Log.Error($"An error occurs when creating potplayer list. \n\r");
                Log.Error(ex.ToString());
            }
        }

        public void BuildPlayListByActors(string playListName, string path, List<string> actors, string potPlayerExe, FileMode fileMode = FileMode.Create)
        {
            try
            {
                var imdbIds = new HashSet<string>();
                var movieLocations = new List<string>();
                foreach(var actor in actors)
                {
                    var movies = _movieService.GetMoviesByFilters(FilterType.Actors, new List<string> { actor }, false).ToList();
                    foreach (var m in movies)
                    {
                        var imdbId = m.ImdbId;
                        var currentMovies = m.MovieLocation.Split("|").Where(x => !string.IsNullOrEmpty(x)).ToList();
                        if (!string.IsNullOrEmpty(imdbId) && !imdbIds.Contains(imdbId) && currentMovies.Count > 0)
                        {
                            imdbIds.Add(imdbId);
                            movieLocations.AddRange(currentMovies);
                        }
                    }
                }
                
                // Determine playlist paths to write to
                var playlistPaths = GetPlaylistPaths(path, potPlayerExe);
                var movieLocationsList = new List<List<string>>();
                for (int i = 0; i < movieLocations.Count; i++)
                {
                    movieLocationsList.Add(new List<string> { movieLocations[i] });
                }
                
                foreach (var playlistPath in playlistPaths)
                {
                    CreatePlaylistFile(playListName, playlistPath, movieLocationsList, fileMode);
                }
                
                UpdateMoviePlayCount(imdbIds);
            }
            catch (Exception ex)
            {
                Log.Error($"An error occurs when creating potplayer list. \n\r");
                Log.Error(ex.ToString());
            }
        }

        private List<string> GetPlaylistPaths(string defaultPath, string potPlayerExe)
        {
            var paths = new List<string>();
            
            // Ensure Playlist folder exists in potPlayerExe directory
            if (!string.IsNullOrEmpty(potPlayerExe))
            {
                string potPlayerExeDir = Path.GetDirectoryName(potPlayerExe);
                string potPlayerPlaylistPath = Path.Combine(potPlayerExeDir, "Playlist");
                if (!Directory.Exists(potPlayerPlaylistPath))
                {
                    Directory.CreateDirectory(potPlayerPlaylistPath);
                }
            }
            
            // Check if default playlist directory exists
            if (Directory.Exists(defaultPath))
            {
                // If default directory exists, add both default and PotPlayer directories
                paths.Add(defaultPath);
                
                // Get PotPlayer directory from database and add its Playlist folder
                string potPlayerDbDirectory = GetPotPlayerDirectoryFromDatabase();
                if (!string.IsNullOrEmpty(potPlayerDbDirectory))
                {
                    string potPlayerPlaylistPath = Path.Combine(Path.GetDirectoryName(potPlayerDbDirectory), "Playlist");
                    if (!Directory.Exists(potPlayerPlaylistPath))
                    {
                        // Create the Playlist directory if it doesn't exist
                        Directory.CreateDirectory(potPlayerPlaylistPath);
                    }
                    paths.Add(potPlayerPlaylistPath);
                }
            }
            else
            {
                // If default directory doesn't exist, use PotPlayer directory from database
                string potPlayerDbDirectory = GetPotPlayerDirectoryFromDatabase();
                if (!string.IsNullOrEmpty(potPlayerDbDirectory))
                {
                    string potPlayerPlaylistPath = Path.Combine(Path.GetDirectoryName(potPlayerDbDirectory), "Playlist");
                    if (!Directory.Exists(potPlayerPlaylistPath))
                    {
                        Directory.CreateDirectory(potPlayerPlaylistPath);
                    }
                    paths.Add(potPlayerPlaylistPath);
                }
                else
                {
                    // Fallback to default path if database doesn't have PotPlayer directory
                    Directory.CreateDirectory(defaultPath);
                    paths.Add(defaultPath);
                }
            }
            
            return paths;
        }

        private string GetPotPlayerDirectoryFromDatabase()
        {
            try
            {
                using (var context = new DatabaseContext())
                {
                    var sqlString = "select Value from UserSettings where Name = 'PotPlayerDirectory'";
                    var dbValue = context.Database.SqlQuery<string>(sqlString).FirstOrDefault();
                    
                    // If database value is empty, use default location
                    if (string.IsNullOrEmpty(dbValue))
                    {
                        return Path.Combine(Environment.CurrentDirectory, "Potplayer", "PotPlayerMini64.exe");
                    }
                    
                    return dbValue;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting PotPlayer directory from database: {ex}");
                // Return default location as fallback
                return Path.Combine(Environment.CurrentDirectory, "Potplayer", "PotPlayerMini64.exe");
            }
        }

        private void CreatePlaylistFile(string playListName, string path, List<List<string>> movieLocations, FileMode fileMode)
        {
            var fs = new FileStream($"{path}\\{playListName.Replace(":", "-")}.dpl", fileMode);
            using (var writer = new StreamWriter(fs))
            {
                if (fileMode == FileMode.Create)
                {
                    var defaultInput = "DAUMPLAYLIST\nplaytime=0\ntopindex=0\nfoldertype=2\nsaveplaypos=0\n";
                    writer.WriteLine(defaultInput);
                }
                var count = 1;
                for (int i = 0; i < movieLocations.Count; i++)
                {
                    foreach (var movieLocation in movieLocations[i])
                    {
                        var l = $"{count++}*file*{movieLocation}";
                        writer.WriteLine(l);
                    }
                }
            }
        }

        private void UpdateMoviePlayCount(IEnumerable<string> imdbIds)
        {
            using (var context = new DatabaseContext())
            {
                foreach (var imdbId in imdbIds)
                {
                    var movie = context.Movies.Where(x => x.ImdbId == imdbId).FirstOrDefault();
                    if (movie != null)
                    {
                        movie.PlayedCount += 1;
                    }
                    context.SaveChanges();
                }
            }
        }
    }
}
