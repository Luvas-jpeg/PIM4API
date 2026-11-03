using EquipamentosMedicosApi.Services;

namespace backend.Tests;

public sealed class CourseRecommendationTests
{
    private static RecommendationCourse[] Catalog() => [
        new(1, "Primeiros socorros básico", "socorros emergência urgência primeiros cuidados", "Emergência", 100, "", true),
        new(2, "Primeiros socorros avançado", "socorros emergência urgência primeiros cuidados", "Emergência", 200, "", true),
        new(3, "Gestão financeira", "finanças orçamento investimento contabilidade empresas", "Gestão", 300, "", true),
        new(4, "Gestão de orçamento", "finanças orçamento investimento contabilidade empresas", "Gestão", 200, "", true),
        new(5, "Dermatologia", "pele estética avaliação dermatológica", "Dermatologia", 150, "", true),
        new(6, "Cuidados da pele", "pele estética avaliação dermatológica", "Dermatologia", 150, "", true)
    ];

    [Fact]
    public void TrainsKMeansAndRanksRelatedPublicCourseContent()
    {
        var result = new CourseRecommendationEngine().Recommend(Catalog(), 1);
        Assert.Equal("ready", result.Status);
        Assert.Equal("mlnet-kmeans-tfidf", result.Algorithm);
        Assert.Equal(2, result.ClusterCount);
        Assert.NotNull(result.ModelVersion);
        Assert.Equal(2, result.Items[0].Id);
        Assert.DoesNotContain(result.Items, c => c.Id == 1);
    }

    [Fact]
    public void AvailabilityChangesDoNotRetrainButImmediatelyExcludeUnavailableCourses()
    {
        var engine = new CourseRecommendationEngine();
        var first = engine.Recommend(Catalog(), 1);
        var changed = Catalog().Select(c => c.Id == 2 ? c with { Available = false } : c).ToArray();
        var second = engine.Recommend(changed, 1);
        Assert.Equal(first.TrainedAt, second.TrainedAt);
        Assert.Equal(first.ModelVersion, second.ModelVersion);
        Assert.DoesNotContain(second.Items, c => c.Id == 2);
    }

    [Fact]
    public void RetrainsWhenCatalogTextChanges()
    {
        var engine = new CourseRecommendationEngine();
        var first = engine.Recommend(Catalog(), 1);
        var changed = Catalog().Select(c => c.Id == 2 ? c with { Description = "conteúdo atualizado" } : c).ToArray();
        Assert.NotEqual(first.ModelVersion, engine.Recommend(changed, 1).ModelVersion);
    }

    [Fact]
    public void DoesNotInventModelPredictionsWithoutEnoughCourses()
    {
        var result = new CourseRecommendationEngine().Recommend(Catalog().Take(2).ToArray(), 1);
        Assert.Equal("insufficient-data", result.Status);
        Assert.Null(result.ModelVersion);
        Assert.Empty(result.Items);
    }
}
