using FsmNeuralNetworks;
using Xunit;

namespace FsmNeuralNetworks.Tests;

/// <summary>
/// Deterministic evaluator workload for a planar two-joint reach task.
/// Inputs are consumer-derived joint-angle errors; outputs are dimensionless
/// kinematic target proposals, never torque, force, or physical outcomes.
/// </summary>
public sealed class EmbodiedMotorControlWorkloadTests
{
    private static readonly EvaluationBudget WorkloadBudget =
        new(8, 8, 64, 16, 16, 8, 32, TimeSpan.FromSeconds(1));

    private static readonly NeuralGraph ReachProposalGraph = new(
        "planar-reach-proposal-v1",
        [
            new InputNode("joint-errors", 2),
            new DenseNode("joint-proposals", 2, 2,
                [0.5, 0, 0, 0.5],
                [0, 0])
        ],
        [
            new NeuralEdge("joint-errors", "values", "joint-proposals", "values")
        ]);

    [Theory]
    [InlineData(0.4, -0.2, 0.2, -0.1)]
    [InlineData(-0.1, 0.3, -0.05, 0.15)]
    public void ProducesDeterministicStatelessJointTargetProposals(
        double shoulderError,
        double elbowError,
        double expectedShoulderProposal,
        double expectedElbowProposal)
    {
        var evaluator = new CpuReferenceEvaluator();
        var inputs = new Dictionary<string, IReadOnlyList<double>>
        {
            ["joint-errors"] = [shoulderError, elbowError]
        };

        var first = evaluator.Evaluate(ReachProposalGraph, inputs, WorkloadBudget, captureTrace: true);
        var second = evaluator.Evaluate(ReachProposalGraph, inputs, WorkloadBudget);

        Assert.Equal(new[] { expectedShoulderProposal, expectedElbowProposal },
            first.Outputs["joint-proposals.pre-activation"]);
        Assert.Equal(first.Outputs["joint-proposals.pre-activation"],
            second.Outputs["joint-proposals.pre-activation"]);
        Assert.Equal("planar-reach-proposal-v1", first.ModelVersion);
        Assert.Equal(new[] { "joint-errors", "joint-proposals" }, first.Trace.Select(x => x.NodeId));
    }

    [Fact]
    public void ConsumerClampsProposalsWithoutChangingTheNeuralEvaluator()
    {
        var result = new CpuReferenceEvaluator().Evaluate(
            ReachProposalGraph,
            new Dictionary<string, IReadOnlyList<double>>
            {
                ["joint-errors"] = [0.4, -0.2]
            },
            WorkloadBudget);

        var proposals = result.Outputs["joint-proposals.pre-activation"];
        const double maxTargetStepRadians = 0.1;
        var accepted = proposals.Select(value =>
            Math.Clamp(value, -maxTargetStepRadians, maxTargetStepRadians)).ToArray();

        Assert.Equal(new[] { 0.1, -0.1 }, accepted);
        Assert.Equal(new[] { 0.2, -0.1 }, proposals);
    }
}
