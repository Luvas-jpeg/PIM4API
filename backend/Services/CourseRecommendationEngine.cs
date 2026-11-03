using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Text;

namespace EquipamentosMedicosApi.Services;

public sealed record RecommendationCourse(int Id, string Nome, string Description, string Category,
    decimal Preco, string Image, bool Available);
public sealed record CourseRecommendationItem(int Id, string Nome, string Category, decimal Preco,
    string Image, double Similarity);
public sealed record CourseRecommendationResult(string Status, string Algorithm, string? ModelVersion,
    DateTime? TrainedAt, int TrainingCourseCount, int ClusterCount, IReadOnlyList<CourseRecommendationItem> Items);

/// <summary>Unsupervised text clustering using only public course metadata, never student attributes.</summary>
public sealed class CourseRecommendationEngine
{
    private readonly object sync = new();
    private Snapshot? cached;
    public const int MinimumCourses = 4;
    public const int MaximumCourses = 200;
    public const string Algorithm = "mlnet-kmeans-tfidf";

    public CourseRecommendationResult Recommend(IReadOnlyList<RecommendationCourse> courses, int courseId)
    {
        var ordered = courses.OrderBy(c => c.Id).Take(MaximumCourses).ToArray();
        var input = ordered.Select(c => new TrainingRow
        {
            Id = c.Id,
            Text = $"{Limit(c.Nome, 120)} {Limit(c.Category, 120)} {Limit(c.Description, 2000)}"
        }).ToArray();
        if (input.Length < MinimumCourses || input.Select(c => c.Text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() < 2)
            return new("insufficient-data", Algorithm, null, null, input.Length, 0, []);
        if (!input.Any(c => c.Id == courseId))
            return new("source-unavailable", Algorithm, null, null, input.Length, 0, []);

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
        Snapshot snapshot;
        lock (sync)
        {
            // Cache only the current catalog snapshot. Changes to text trigger retraining.
            if (cached?.Fingerprint != fingerprint) cached = Train(input, fingerprint);
            snapshot = cached;
        }
        var source = snapshot.Courses[courseId];
        var ranked = ordered.Where(c => c.Id != courseId && c.Available)
            .Select(c => (Course: c, Embedding: snapshot.Courses[c.Id]))
            .Select(c => (c.Course, SameCluster: c.Embedding.Cluster == source.Cluster,
                Similarity: Similarity(source.Features, c.Embedding.Features)))
            .Where(c => c.Similarity >= 0.05)
            .OrderByDescending(c => c.SameCluster).ThenByDescending(c => c.Similarity)
            .ThenBy(c => c.Course.Id).Take(3)
            .Select(c => new CourseRecommendationItem(c.Course.Id, c.Course.Nome, c.Course.Category,
                c.Course.Preco, c.Course.Image, Math.Round(c.Similarity, 4))).ToArray();
        return new("ready", Algorithm, fingerprint[..12], snapshot.TrainedAt, input.Length, snapshot.ClusterCount, ranked);
    }

    private static Snapshot Train(TrainingRow[] rows, string fingerprint)
    {
        var ml = new MLContext(seed: 42);
        var data = ml.Data.LoadFromEnumerable(rows);
        var distinct = rows.Select(c => c.Text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var clusters = Math.Min(Math.Min(3, distinct), Math.Max(2, rows.Length / 3));
        var pipeline = ml.Transforms.Text.FeaturizeText("Features", new TextFeaturizingEstimator.Options
        {
            CharFeatureExtractor = null,
            WordFeatureExtractor = new WordBagEstimator.Options
            {
                NgramLength = 1,
                Weighting = NgramExtractingEstimator.WeightingCriteria.TfIdf,
                MaximumNgramsCount = [2048]
            },
            Norm = TextFeaturizingEstimator.NormFunction.L2
        }, nameof(TrainingRow.Text))
        .Append(ml.Clustering.Trainers.KMeans(featureColumnName: "Features", numberOfClusters: clusters));
        var model = pipeline.Fit(data);
        var embedded = ml.Data.CreateEnumerable<EmbeddedCourse>(model.Transform(data), reuseRowObject: false)
            .ToDictionary(c => c.Id);
        return new(fingerprint, DateTime.UtcNow, clusters, embedded);
    }

    private static double Similarity(float[] first, float[] second)
    {
        double dot = 0, a = 0, b = 0;
        for (var i = 0; i < first.Length; i++)
        {
            dot += first[i] * second[i]; a += first[i] * first[i]; b += second[i] * second[i];
        }
        return a == 0 || b == 0 ? 0 : Math.Clamp(dot / Math.Sqrt(a * b), 0, 1);
    }

    private static string Limit(string text, int maximum) => text[..Math.Min(text.Length, maximum)];
    private sealed record Snapshot(string Fingerprint, DateTime TrainedAt, int ClusterCount, Dictionary<int, EmbeddedCourse> Courses);
    public sealed class TrainingRow
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
    }
    public sealed class EmbeddedCourse
    {
        public int Id { get; set; }
        public float[] Features { get; set; } = [];
        [ColumnName("PredictedLabel")]
        public uint Cluster { get; set; }
    }
}
