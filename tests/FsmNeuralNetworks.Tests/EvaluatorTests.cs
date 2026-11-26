using FsmNeuralNetworks;
using Xunit;

namespace FsmNeuralNetworks.Tests;

public sealed class EvaluatorTests
{
    private static EvaluationBudget Budget(int trace = 8, int traceElements = 32) => new(16, 16, 1024, 256, 64, trace, traceElements, TimeSpan.FromSeconds(2));

    private static NeuralGraph Fixture() => new(
        "fixture-v1",
        [
            new InputNode("input-0", 2),
            new DenseNode("dense-0", 2, 2, [2, 1, -1, 3], [-1, 2]),
            new ReluNode("relu-0", 2)
        ],
        [
            new NeuralEdge("input-0", "values", "dense-0", "values"),
            new NeuralEdge("dense-0", "pre-activation", "relu-0", "values")
        ]);

    [Fact]
    public void DenseReluFixtureProducesFourAndThree()
    {
        var result = new CpuReferenceEvaluator().Evaluate(
            Fixture(),
            new Dictionary<string, IReadOnlyList<double>> { ["input-0"] = new double[] { 2, 1 } },
            Budget());

        Assert.Equal("fixture-v1", result.ModelVersion);
        Assert.Equal(new[] { 4d, 3d }, result.Outputs["relu-0.values"]);
    }

    [Fact]
    public void TraceComesFromSameExecutionAndIsBounded()
    {
        var result = new CpuReferenceEvaluator().Evaluate(
            Fixture(),
            new Dictionary<string, IReadOnlyList<double>> { ["input-0"] = new double[] { 2, 1 } },
            Budget(trace: 2, traceElements: 4),
            captureTrace: true);

        Assert.Equal(new[] { "input-0", "dense-0" }, result.Trace.Select(t => t.NodeId));
        Assert.True(result.TraceTruncated);
        Assert.Equal(new[] { 4d, 3d }, result.Trace[1].Values);
    }

    [Fact]
    public void RejectsNonFiniteInput()
    {
        Assert.Throws<NeuralValidationException>(() => new CpuReferenceEvaluator().Evaluate(
            Fixture(),
            new Dictionary<string, IReadOnlyList<double>> { ["input-0"] = new double[] { double.NaN, 1 } },
            Budget()));
    }

    [Fact]
    public void RejectsGraphWorkBudgetOverrun()
    {
        var graph = Fixture();
        var limited = Budget() with { MaxWorkOperations = 3 };

        Assert.Throws<NeuralBudgetExceededException>(() => new CpuReferenceEvaluator().Evaluate(
            graph,
            new Dictionary<string, IReadOnlyList<double>> { ["input-0"] = new double[] { 2, 1 } },
            limited));
    }

    [Fact]
    public void RejectsInputBudgetOverrun()
    {
        Assert.Throws<NeuralBudgetExceededException>(() => new CpuReferenceEvaluator().Evaluate(
            Fixture(),
            new Dictionary<string, IReadOnlyList<double>> { ["input-0"] = new double[] { 2, 1 } },
            new EvaluationBudget(1, 16, 1024, 256, 64, 8, 32, TimeSpan.FromSeconds(2))));
    }

    [Fact]
    public void RejectsCycles()
    {
        Assert.Throws<ArgumentException>(() => new NeuralGraph(
            "cycle",
            [new InputNode("input", 1), new ReluNode("a", 1), new ReluNode("b", 1)],
            [
                new NeuralEdge("a", "values", "b", "values"),
                new NeuralEdge("b", "values", "a", "values")
            ]));
    }

    [Fact]
    public void RejectsShapeMismatch()
    {
        Assert.Throws<ArgumentException>(() => new NeuralGraph(
            "bad-shape",
            [new InputNode("input", 2), new ReluNode("relu", 3)],
            [new NeuralEdge("input", "values", "relu", "values")]));
    }

    [Fact]
    public void RejectsMissingFiniteBudget()
    {
        var invalid = Budget() with { MaxDuration = Timeout.InfiniteTimeSpan };
        Assert.Throws<ArgumentOutOfRangeException>(invalid.Validate);
    }

    [Fact]
    public void ReportsNumericalOverflowInsteadOfReturningPartialOutput()
    {
        var graph = new NeuralGraph("overflow",
            [new InputNode("input", 1), new DenseNode("dense", 1, 1, [2d], [0d])],
            [new NeuralEdge("input", "values", "dense", "values")]);

        Assert.Throws<NeuralNumericalFailureException>(() => new CpuReferenceEvaluator().Evaluate(
            graph,
            new Dictionary<string, IReadOnlyList<double>> { ["input"] = new double[] { 1e308 } },
            Budget()));
    }

    [Fact]
    public void TraceElementBudgetTruncatesWithoutAffectingInference()
    {
        var result = new CpuReferenceEvaluator().Evaluate(
            Fixture(),
            new Dictionary<string, IReadOnlyList<double>> { ["input-0"] = new double[] { 2, 1 } },
            Budget(trace: 8, traceElements: 2),
            captureTrace: true);

        Assert.Single(result.Trace);
        Assert.True(result.TraceTruncated);
        Assert.Equal(new[] { 4d, 3d }, result.Outputs["relu-0.values"]);
    }

    [Fact]
    public void ModelParametersAreSnapshotted()
    {
        var weights = new[] { 2d };
        var bias = new[] { 0d };
        var graph = new NeuralGraph("snapshot", [new InputNode("input", 1), new DenseNode("dense", 1, 1, weights, bias)],
            [new NeuralEdge("input", "values", "dense", "values")]);
        weights[0] = 100;
        var result = new CpuReferenceEvaluator().Evaluate(graph,
            new Dictionary<string, IReadOnlyList<double>> { ["input"] = new double[] { 3 } }, Budget());
        Assert.Equal(new[] { 6d }, result.Outputs["dense.pre-activation"]);
    }
}
