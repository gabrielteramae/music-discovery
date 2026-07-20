using MusicDiscovery.Api.Models;

namespace MusicDiscovery.Api.Services;

public interface IRecommendationEngine
{
    /// <summary>
    /// Rankeia candidatos pela similaridade do vetor de audio features
    /// em relação ao "centróide" (média) de uma playlist de referência.
    /// Isso é o que diferencia o app de só repassar cru a recomendação da Spotify.
    /// </summary>
    IReadOnlyList<ScoredTrack> RankBySimilarity(IReadOnlyList<Track> playlistTracks, IReadOnlyList<Track> candidates);
}

public record ScoredTrack(Track Track, double Score);

public class RecommendationEngine : IRecommendationEngine
{
    // Pesos por feature: dá mais importância a energia/dançabilidade/valência
    // (o que define "vibe") do que a acousticness/instrumentalness (textura sonora)
    private static readonly double[] Weights = [1.2, 1.2, 1.2, 0.5, 0.6, 0.4, 0.4];

    public IReadOnlyList<ScoredTrack> RankBySimilarity(IReadOnlyList<Track> playlistTracks, IReadOnlyList<Track> candidates)
    {
        var reference = playlistTracks
            .Where(t => t.AudioFeatures is not null)
            .Select(t => ToVector(t.AudioFeatures!))
            .ToList();

        if (reference.Count == 0)
            return [];

        var centroid = Centroid(reference);

        return candidates
            .Where(t => t.AudioFeatures is not null)
            .Select(t => new ScoredTrack(t, CosineSimilarity(centroid, ToVector(t.AudioFeatures!))))
            .OrderByDescending(s => s.Score)
            .ToList();
    }

    private static double[] ToVector(AudioFeatures f) =>
    [
        f.Danceability,
        f.Energy,
        f.Valence,
        f.Tempo / 200.0,          // normaliza BPM pra ficar na mesma escala 0-1 das outras features
        f.Acousticness,
        f.Instrumentalness,
        f.Speechiness
    ];

    private static double[] Centroid(List<double[]> vectors)
    {
        var dimensions = vectors[0].Length;
        var centroid = new double[dimensions];

        foreach (var vector in vectors)
            for (var i = 0; i < dimensions; i++)
                centroid[i] += vector[i];

        for (var i = 0; i < dimensions; i++)
            centroid[i] /= vectors.Count;

        return centroid;
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        double dot = 0, normA = 0, normB = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var weight = Weights[i];
            dot += a[i] * b[i] * weight;
            normA += a[i] * a[i] * weight;
            normB += b[i] * b[i] * weight;
        }

        if (normA == 0 || normB == 0) return 0;
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
