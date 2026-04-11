namespace Nanny_BackEnd.DTOs.Recommendation;

public record ScoringWeights(
    double Semantic,
    double Salary,
    double Distance,
    double ColdStart
)
{
    public static ScoringWeights Default => new(0.80, 0.12, 0.08, 0.75);
}
