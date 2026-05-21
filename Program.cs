using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Set up the In-Memory Database
builder.Services.AddDbContext<MovieDb>(options =>
    options.UseInMemoryDatabase("MovieWatchlist"));

var app = builder.Build();

// Configure Swagger for testing
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ==========================================
// API ENDPOINTS (The "Controllers")
// ==========================================
var movies = app.MapGroup("/api/movies");

// 1. GET all movies (Includes Search and Genre Filter)
movies.MapGet("/", async (string? search, string? genre, MovieDb db) =>
{
    var query = db.Movies.AsQueryable();

    // String search: Filter by Title
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(m => m.Title.ToLower().Contains(search.ToLower()));
    }

    // Filter by Genre
    if (!string.IsNullOrWhiteSpace(genre))
    {
        query = query.Where(m => m.Genre.ToLower() == genre.ToLower());
    }

    return await query.ToListAsync();
});

// 2. GET a specific movie by ID
movies.MapGet("/{id}", async (int id, MovieDb db) =>
    await db.Movies.FindAsync(id)
        is Movie movie
            ? Results.Ok(movie)
            : Results.NotFound());

// 3. GET Stats (Calculates the average rating)
movies.MapGet("/stats", async (MovieDb db) =>
{
    var watchedMovies = await db.Movies.Where(m => m.IsWatched && m.Rating.HasValue).ToListAsync();
    
    // Calculate average using LINQ
    double avgRating = watchedMovies.Any() ? watchedMovies.Average(m => m.Rating.Value) : 0;

    return Results.Ok(new
    {
        TotalMoviesOnList = await db.Movies.CountAsync(),
        MoviesWatched = watchedMovies.Count,
        AverageRating = Math.Round(avgRating, 1) // Round to 1 decimal place
    });
});

// 4. POST (Add a new movie to the watchlist)
movies.MapPost("/", async (Movie movie, MovieDb db) =>
{
    // Ensure rating is between 1 and 5 if provided
    if (movie.Rating.HasValue && (movie.Rating < 1 || movie.Rating > 5))
    {
        return Results.BadRequest("Rating must be between 1 and 5 stars.");
    }

    db.Movies.Add(movie);
    await db.SaveChangesAsync();
    return Results.Created($"/api/movies/{movie.Id}", movie);
});

// 5. PUT (Update a movie, e.g., marking it watched and leaving a review)
movies.MapPut("/{id}", async (int id, Movie inputMovie, MovieDb db) =>
{
    var movie = await db.Movies.FindAsync(id);
    if (movie is null) return Results.NotFound();

    movie.Title = inputMovie.Title;
    movie.Genre = inputMovie.Genre;
    movie.IsWatched = inputMovie.IsWatched;
    movie.Rating = inputMovie.Rating;
    movie.Review = inputMovie.Review;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

// 6. DELETE a movie
movies.MapDelete("/{id}", async (int id, MovieDb db) =>
{
    if (await db.Movies.FindAsync(id) is Movie movie)
    {
        db.Movies.Remove(movie);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    return Results.NotFound();
});

// Seed some initial data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MovieDb>();
    db.Database.EnsureCreated();
    if (!db.Movies.Any())
    {
        db.Movies.AddRange(
            new Movie { Id = 1, Title = "The Matrix", Genre = "Action", IsWatched = true, Rating = 5, Review = "Mind-blowing!" },
            new Movie { Id = 2, Title = "Dune", Genre = "Sci-Fi", IsWatched = false },
            new Movie { Id = 3, Title = "Superbad", Genre = "Comedy", IsWatched = true, Rating = 4, Review = "Very funny." }
        );
        db.SaveChanges();
    }
}

app.Run();

// ==========================================
// MODELS & DATABASE CONTEXT
// ==========================================
public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public bool IsWatched { get; set; }
    public int? Rating { get; set; } // Nullable because you haven't watched it yet!
    public string? Review { get; set; }
}

public class MovieDb : DbContext
{
    public MovieDb(DbContextOptions<MovieDb> options) : base(options) { }
    public DbSet<Movie> Movies => Set<Movie>();
}
