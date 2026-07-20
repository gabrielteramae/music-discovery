using Microsoft.EntityFrameworkCore;
using MusicDiscovery.Api.Models;

namespace MusicDiscovery.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();
    public DbSet<AudioFeatures> AudioFeatures => Set<AudioFeatures>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.SpotifyUserId)
            .IsUnique();

        modelBuilder.Entity<Track>()
            .HasIndex(t => t.SpotifyTrackId)
            .IsUnique();

        modelBuilder.Entity<Playlist>()
            .HasIndex(p => p.SpotifyPlaylistId)
            .IsUnique();

        // Chave composta da tabela de junção Playlist <-> Track
        modelBuilder.Entity<PlaylistTrack>()
            .HasKey(pt => new { pt.PlaylistId, pt.TrackId });

        modelBuilder.Entity<PlaylistTrack>()
            .HasOne(pt => pt.Playlist)
            .WithMany(p => p.PlaylistTracks)
            .HasForeignKey(pt => pt.PlaylistId);

        modelBuilder.Entity<PlaylistTrack>()
            .HasOne(pt => pt.Track)
            .WithMany(t => t.PlaylistTracks)
            .HasForeignKey(pt => pt.TrackId);

        // 1:1 entre Track e AudioFeatures
        modelBuilder.Entity<AudioFeatures>()
            .HasKey(af => af.TrackId);

        modelBuilder.Entity<AudioFeatures>()
            .HasOne(af => af.Track)
            .WithOne(t => t.AudioFeatures)
            .HasForeignKey<AudioFeatures>(af => af.TrackId);
    }
}
