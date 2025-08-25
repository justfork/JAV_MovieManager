using System;
using System.Collections.Generic;
using System.Linq;

namespace MovieManager.BusinessLogic
{
    public class StudioService
    {
        private MovieService _movieService;

        public StudioService(MovieService movieService)
        {
            _movieService = movieService;
        }

        public List<String> GetUniqueStudios()
        {
            return _movieService.GetMovies().Select(x => x.Studio).Distinct().ToList();
        }
    }
}
