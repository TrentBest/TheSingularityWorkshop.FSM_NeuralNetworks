using System.Collections.ObjectModel;
using System.Diagnostics;

namespace FsmNeuralNetworks;

/// <summary>Stable semantic node identity plus a fixed operation profile.</summary>
public abstract class NeuralNode
{
    protected NeuralNode(string id, int width)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Node id is required.", nameof(id));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Id = id;
        Width = width;
    }

    public string Id { get; }
    public int Width { get; }
    public abstract string OutputPort { get; }
    internal abstract string? InputPort { get; }
}

public sealed class InputNode(string id, int width) : NeuralNode(id, width)
{
    public override string OutputPort => "values";
    internal override string? InputPort => null;
}

public sealed class DenseNode : NeuralNode
{
    private readonly double[] _weights;
    private readonly double[] _bias;

    public DenseNode(string id, int inputWidth, int outputWidth, IEnumerable<double> weights, IEnumerable<double> bias)
        : base(id, outputWidth)
    {
        if (inputWidth <= 0) throw new ArgumentOutOfRangeException(nameof(inputWidth));
        InputWidth = inputWidth;
        _weights = weights?.ToArray() ?? throw new ArgumentNullException(nameof(weights));
        _bias = bias?.ToArray() ?? throw new ArgumentNullException(nameof(bias));
        if (_weights.Length != checked(inputWidth * outputWidth))
            throw new ArgumentException("Weights must be row-major [outputWidth,inputWidth].", nameof(weights));
        if (_bias.Length != outputWidth)
            throw new ArgumentException("Bias length must equal output width.", nameof(bias));
        if (_weights.Any(x => !double.IsFinite(x)) || _bias.Any(x => !double.IsFinite(x)))
            throw new ArgumentException("Model parameters must all be finite.");
    }

    public int InputWidth { get; }
    public override string OutputPort => "pre-activation";
    internal override string InputPort => "values";
    internal double Weight(int output, int input) => _weights[output * InputWidth + input];
    internal double Bias(int output) => _bias[output];
}

public sealed class ReluNode(string id, int width) : NeuralNode(id, width)
{
    public override string OutputPort => "values";
    internal override string InputPort => "values";
}

public sealed record NeuralEdge(string SourceNodeId, string SourcePort, string TargetNodeId, string TargetPort);

public sealed class NeuralGraph
{
    private readonly ReadOnlyCollection<NeuralNode> _nodes;
    private readonly ReadOnlyCollection<NeuralEdge> _edges;
    private readonly ReadOnlyCollection<NeuralNode> _executionOrder;

    public NeuralGraph(string modelVersion, IEnumerable<NeuralNode> nodes, IEnumerable<NeuralEdge> edges)
    {
        if (string.IsNullOrWhiteSpace(modelVersion)) throw new ArgumentException("Model version is required.", nameof(modelVersion));
        ModelVersion = modelVersion;
        var nodeArray = nodes?.ToArray() ?? throw new ArgumentNullException(nameof(nodes));
        var edgeArray = edges?.ToArray() ?? throw new ArgumentNullException(nameof(edges));
        if (nodeArray.Length == 0) throw new ArgumentException("A graph must contain nodes.", nameof(nodes));
        if (nodeArray.Select(n => n.Id).Distinct(StringComparer.Ordinal).Count() != nodeArray.Length)
            throw new ArgumentException("Node ids must be unique.", nameof(nodes));

        var byId = nodeArray.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var incoming = new Dictionary<string, NeuralEdge>(StringComparer.Ordinal);
        var outgoing = nodeArray.ToDictionary(n => n.Id, _ => new List<string>(), StringComparer.Ordinal);
        var indegree = nodeArray.ToDictionary(n => n.Id, _ => 0, StringComparer.Ordinal);
        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var edge in edgeArray)
        {
            if (!byId.TryGetValue(edge.SourceNodeId, out var source) ||
                !byId.TryGetValue(edge.TargetNodeId, out var target))
                throw new ArgumentException("Every edge endpoint must identify a graph node.", nameof(edges));
            if (!string.Equals(edge.SourcePort, source.OutputPort, StringComparison.Ordinal))
                throw new ArgumentException($"Unknown output port '{edge.SourcePort}' on '{source.Id}'.", nameof(edges));
            if (!string.Equals(edge.TargetPort, target.InputPort, StringComparison.Ordinal))
                throw new ArgumentException($"Unknown input port '{edge.TargetPort}' on '{target.Id}'.", nameof(edges));
            var key = $"{edge.SourceNodeId}\0{edge.SourcePort}\0{edge.TargetNodeId}\0{edge.TargetPort}";
            if (!edgeKeys.Add(key)) throw new ArgumentException("Duplicate edges are not allowed.", nameof(edges));
            if (!incoming.TryAdd(target.Id, edge))
                throw new ArgumentException($"Node '{target.Id}' has more than one producer.", nameof(edges));
            if (source.Width != (target switch
                {
                    DenseNode dense => dense.InputWidth,
                    _ => target.Width
                }))
                throw new ArgumentException($"Shape mismatch on edge '{source.Id}' -> '{target.Id}'.", nameof(edges));
            outgoing[source.Id].Add(target.Id);
            indegree[target.Id]++;
        }

        foreach (var node in nodeArray)
        {
            if (node is InputNode && incoming.ContainsKey(node.Id))
                throw new ArgumentException($"Input node '{node.Id}' cannot have an incoming edge.", nameof(edges));
            if (node is not InputNode && !incoming.ContainsKey(node.Id))
                throw new ArgumentException($"Operation node '{node.Id}' requires one input edge.", nameof(edges));
        }

        var ready = new Queue<NeuralNode>(nodeArray.Where(n => indegree[n.Id] == 0));
        var ordered = new List<NeuralNode>(nodeArray.Length);
        while (ready.TryDequeue(out var current))
        {
            ordered.Add(current);
            foreach (var nextId in outgoing[current.Id])
            {
                if (--indegree[nextId] == 0) ready.Enqueue(byId[nextId]);
            }
        }
        if (ordered.Count != nodeArray.Length) throw new ArgumentException("Cycles are not supported by the stateless DAG evaluator.", nameof(edges));

        _nodes = Array.AsReadOnly(nodeArray);
        _edges = Array.AsReadOnly(edgeArray);
        _executionOrder = Array.AsReadOnly(ordered.ToArray());
    }

    public string ModelVersion { get; }
    public IReadOnlyList<NeuralNode> Nodes => _nodes;
    public IReadOnlyList<NeuralEdge> Edges => _edges;
    internal IReadOnlyList<NeuralNode> ExecutionOrder => _executionOrder;
    internal IReadOnlyDictionary<string, NeuralEdge> Incoming =>
        new ReadOnlyDictionary<string, NeuralEdge>(_edges.ToDictionary(e => e.TargetNodeId, StringComparer.Ordinal));
}

/// <summary>All dimensions are finite, explicit limits. No unlimited mode exists.</summary>
public sealed record EvaluationBudget(
    int MaxInputElements,
    int MaxNodes,
    int MaxIntermediateElements,
    int MaxTraceRecords,
    int MaxTraceElements,
    TimeSpan MaxDuration)
{
    public void Validate()
    {
        if (MaxInputElements <= 0 || MaxNodes <= 0 || MaxIntermediateElements <= 0 ||
            MaxTraceRecords < 0 || MaxTraceElements < 0 || MaxDuration <= TimeSpan.Zero || MaxDuration == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(EvaluationBudget), "Every budget dimension must be finite and positive (trace may be zero).");
    }
}

public sealed record NodeTrace(string ModelVersion, string NodeId, string OutputPort, int Width, IReadOnlyList<double> Values);

public sealed class EvaluationResult
{
    internal EvaluationResult(string modelVersion, IDictionary<string, double[]> outputs, IReadOnlyList<NodeTrace> trace, bool traceTruncated)
    {
        ModelVersion = modelVersion;
        Outputs = new ReadOnlyDictionary<string, IReadOnlyList<double>>(
            outputs.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<double>)Array.AsReadOnly(kv.Value), StringComparer.Ordinal));
        Trace = trace;
        TraceTruncated = traceTruncated;
    }

    public string ModelVersion { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<double>> Outputs { get; }
    public IReadOnlyList<NodeTrace> Trace { get; }
    public bool TraceTruncated { get; }
}

public sealed class NeuralValidationException(string message) : Exception(message) { }
public sealed class NeuralNumericalFailureException(string message) : ArithmeticException(message) { }
public sealed class NeuralBudgetExceededException(string message) : Exception(message) { }

/// <summary>Deterministic binary64 CPU reference evaluator for Input, Dense, and ReLU DAGs.</summary>
public sealed class CpuReferenceEvaluator
{
    public EvaluationResult Evaluate(
        NeuralGraph graph,
        IReadOnlyDictionary<string, IReadOnlyList<double>> inputs,
        EvaluationBudget budget,
        bool captureTrace = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(budget);
        budget.Validate();
        if (graph.Nodes.Count > budget.MaxNodes) throw new NeuralBudgetExceededException("Graph node budget exceeded.");

        var inputNodes = graph.Nodes.OfType<InputNode>().ToArray();
        var inputCount = 0;
        foreach (var node in inputNodes)
        {
            if (!inputs.TryGetValue(node.Id, out var values) || values.Count != node.Width)
                throw new NeuralValidationException($"Input '{node.Id}' is missing or has the wrong shape.");
            inputCount = checked(inputCount + values.Count);
            if (values.Any(v => !double.IsFinite(v)))
                throw new NeuralValidationException($"Input '{node.Id}' contains a non-finite value.");
        }
        if (inputCount > budget.MaxInputElements) throw new NeuralBudgetExceededException("Input element budget exceeded.");
        if (inputs.Keys.Any(key => inputNodes.All(n => n.Id != key)))
            throw new NeuralValidationException("An input was supplied for an unknown input node.");

        var watch = Stopwatch.StartNew();
        var valuesByNode = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var outputs = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var trace = new List<NodeTrace>();
        var retainedElements = 0;
        var traceTruncated = false;
        var traceElements = 0;
        var incoming = graph.Incoming;

        foreach (var node in graph.ExecutionOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (watch.Elapsed > budget.MaxDuration) throw new NeuralBudgetExceededException("Execution time budget exceeded.");

            if (retainedElements + node.Width > budget.MaxIntermediateElements)
                throw new NeuralBudgetExceededException("Intermediate/result element budget exceeded before allocation.");

            double[] result;
            switch (node)
            {
                case InputNode:
                    result = inputs[node.Id].ToArray();
                    break;
                case DenseNode dense:
                {
                    var edge = incoming[dense.Id];
                    var source = valuesByNode[edge.SourceNodeId];
                    result = new double[dense.Width];
                    for (var output = 0; output < dense.Width; output++)
                    {
                        var sum = 0d;
                        for (var input = 0; input < dense.InputWidth; input++)
                        {
                            sum += source[input] * dense.Weight(output, input);
                            if ((input & 1023) == 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (watch.Elapsed > budget.MaxDuration)
                                    throw new NeuralBudgetExceededException("Execution time budget exceeded.");
                            }
                            if (!double.IsFinite(sum))
                                throw new NeuralNumericalFailureException($"Non-finite accumulation at node '{dense.Id}', output {output}.");
                        }
                        sum += dense.Bias(output);
                        if (!double.IsFinite(sum))
                            throw new NeuralNumericalFailureException($"Non-finite bias result at node '{dense.Id}', output {output}.");
                        result[output] = sum;
                    }
                    break;
                }
                case ReluNode:
                {
                    var source = valuesByNode[incoming[node.Id].SourceNodeId];
                    result = new double[source.Length];
                    for (var i = 0; i < source.Length; i++) result[i] = Math.Max(0d, source[i]);
                    break;
                }
                default:
                    throw new NeuralValidationException($"Unsupported node type '{node.GetType().Name}'.");
            }

            if (result.Any(v => !double.IsFinite(v)))
                throw new NeuralNumericalFailureException($"Node '{node.Id}' produced a non-finite value.");
            retainedElements = checked(retainedElements + result.Length);
            if (retainedElements > budget.MaxIntermediateElements)
                throw new NeuralBudgetExceededException("Intermediate/result element budget exceeded.");
            valuesByNode.Add(node.Id, result);

            if (captureTrace)
            {
                if (trace.Count < budget.MaxTraceRecords && traceElements + result.Length <= budget.MaxTraceElements)
                {
                    trace.Add(new NodeTrace(graph.ModelVersion, node.Id, node.OutputPort, node.Width, Array.AsReadOnly((double[])result.Clone())));
                    traceElements += result.Length;
                }
                else
                    traceTruncated = true;
            }

            if (!graph.Edges.Any(e => e.SourceNodeId == node.Id))
                outputs.Add($"{node.Id}.{node.OutputPort}", result);
        }

        if (watch.Elapsed > budget.MaxDuration) throw new NeuralBudgetExceededException("Execution time budget exceeded.");
        return new EvaluationResult(graph.ModelVersion, outputs, Array.AsReadOnly(trace.ToArray()), traceTruncated);
    }
}
