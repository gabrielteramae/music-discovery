using MusicDiscovery.Api.Models;

namespace MusicDiscovery.Api.Services;

public interface IPlaylistOrganizerService
{
    /// <summary>
    /// Agrupa uma lista bagunçada de faixas (ex: "Liked Songs") em clusters por vibe,
    /// usando k-means sobre o vetor de audio features. Cada cluster vira uma sugestão de playlist.
    /// </summary>
    IReadOnlyList<PlaylistSuggestion> SuggestGroupings(IReadOnlyList<Track> tracks, int clusterCount = 4);
}

public record PlaylistSuggestion(string SuggestedName, IReadOnlyList<Track> Tracks, ClusterProfile Profile);

public record ClusterProfile(double AvgDanceability, double AvgEnergy, double AvgValence, double AvgTempo);

public class PlaylistOrganizerService : IPlaylistOrganizerService
{
    private const int MaxIterations = 50;

    public IReadOnlyList<PlaylistSuggestion> SuggestGroupings(IReadOnlyList<Track> tracks, int clusterCount = 4)
    {
        var withFeatures = tracks.Where(t => t.AudioFeatures is not null).ToList();
        if (withFeatures.Count < clusterCount)
            return [];

        var vectors = withFeatures.Select(t => ToVector(t.AudioFeatures!)).ToList();
        var assignments = RunKMeans(vectors, clusterCount);

        return Enumerable.Range(0, clusterCount)
            .Select(clusterIndex =>
            {
                var clusterTracks = withFeatures
                    .Where((_, i) => assignments[i] == clusterIndex)
                    .ToList();

                if (clusterTracks.Count == 0) return null;

                var profile = BuildProfile(clusterTracks);
                return new PlaylistSuggestion(NameCluster(profile), clusterTracks, profile);
            })
            .Where(s => s is not null)
            .Select(s => s!)
            .OrderByDescending(s => s.Tracks.Count)
            .ToList();
    }

    private static int[] RunKMeans(List<double[]> vectors, int k)
    {
        var random = new Random(42); // seed fixa: resultado determinístico entre execuções
        var dimensions = vectors[0].Length;

        // Inicializa centróides com pontos aleatórios do próprio dataset (k-means++, versão simplificada)
        var centroids = vectors.OrderBy(_ => random.Next()).Take(k).Select(v => (double[])v.Clone()).ToList();
        var assignments = new int[vectors.Count];

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var changed = false;

            for (var i = 0; i < vectors.Count; i++)
            {
                var closest = ClosestCentroid(vectors[i], centroids);
                if (assignments[i] != closest)
                {
                    assignments[i] = closest;
                    changed = true;
                }
            }

            if (!changed) break;

            for (var c = 0; c < k; c++)
            {
                var members = vectors.Where((_, i) => assignments[i] == c).ToList();
                if (members.Count == 0) continue;

                var newCentroid = new double[dimensions];
                foreach (var m in members)
                    for (var d = 0; d < dimensions; d++)
                        newCentroid[d] += m[d] / members.Count;

                centroids[c] = newCentroid;
            }
        }

        return assignments;
    }

    private static int ClosestCentroid(double[] vector, List<double[]> centroids)
    {
        var best = 0;
        var bestDistance = double.MaxValue;

        for (var c = 0; c < centroids.Count; c++)
        {
            var distance = EuclideanDistance(vector, centroids[c]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = c;
            }
        }

        return best;
    }

    private static double EuclideanDistance(double[] a, double[] b)
    {
        double sum = 0;
        for (var i = 0; i < a.Length; i++)
            sum += Math.Pow(a[i] - b[i], 2);
        return Math.Sqrt(sum);
    }

    private static double[] ToVector(AudioFeatures f) =>
        [f.Danceability, f.Energy, f.Valence, f.Tempo / 200.0];

    private static ClusterProfile BuildProfile(List<Track> tracks)
    {
        var features = tracks.Select(t => t.AudioFeatures!).ToList();
        return new ClusterProfile(
            features.Average(f => f.Danceability),
            features.Average(f => f.Energy),
            features.Average(f => f.Valence),
            features.Average(f => f.Tempo));
    }

    // Dá um nome legível ao cluster baseado no perfil médio de audio features
    private static string NameCluster(ClusterProfile p) => (p.Energy, p.Valence) switch
    {
        ( > 0.7, > 0.6) => "Alta Energia / Animado",
        ( > 0.7, <= 0.4) => "Intenso / Sombrio",
        ( <= 0.4, > 0.6) => "Calmo / Feliz",
        ( <= 0.4, <= 0.4) => "Melancólico / Introspectivo",
        _ => "Vibe Equilibrada"
    };
}
