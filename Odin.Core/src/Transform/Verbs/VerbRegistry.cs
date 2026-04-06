#nullable enable

using System;
using System.Collections.Generic;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Registry of all available verb functions. Provides a singleton instance with all
/// built-in verbs pre-registered, and supports custom verb registration.
/// </summary>
public sealed class VerbRegistry
{
    private readonly Dictionary<string, Func<DynValue[], VerbContext, DynValue>> _builtins
        = new Dictionary<string, Func<DynValue[], VerbContext, DynValue>>();

    private readonly Dictionary<string, Func<DynValue[], VerbContext, DynValue>> _custom
        = new Dictionary<string, Func<DynValue[], VerbContext, DynValue>>();

    private static readonly Lazy<VerbRegistry> _instance = new Lazy<VerbRegistry>(() => new VerbRegistry());

    /// <summary>
    /// Gets the singleton verb registry instance with all built-in verbs registered.
    /// </summary>
    public static VerbRegistry Instance => _instance.Value;

    /// <summary>
    /// Creates a new VerbRegistry and registers all built-in verbs.
    /// </summary>
    public VerbRegistry()
    {
        RegisterBuiltins();
    }

    /// <summary>
    /// Invokes a verb by name with the given arguments and context.
    /// Custom verbs take priority over built-in verbs with the same name.
    /// </summary>
    /// <param name="name">The verb name (e.g., "upper", "concat").</param>
    /// <param name="args">The arguments to pass to the verb.</param>
    /// <param name="ctx">The execution context.</param>
    /// <returns>The result of the verb invocation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the verb name is not registered.</exception>
    public DynValue Invoke(string name, DynValue[] args, VerbContext ctx)
    {
        if (_custom.TryGetValue(name, out var customFunc))
            return customFunc(args, ctx);

        if (_builtins.TryGetValue(name, out var builtinFunc))
            return builtinFunc(args, ctx);

        throw new InvalidOperationException($"Unknown verb: '{name}'");
    }

    /// <summary>
    /// Registers a custom verb function. Custom verbs override built-in verbs
    /// with the same name.
    /// </summary>
    /// <param name="name">The verb name.</param>
    /// <param name="func">The verb implementation function.</param>
    public void RegisterCustom(string name, Func<DynValue[], VerbContext, DynValue> func)
    {
        _custom[name] = func;
    }

    /// <summary>
    /// Registers all built-in verbs by delegating to the static Register methods
    /// on each verb category class.
    /// </summary>
    private void RegisterBuiltins()
    {
        CoreVerbs.Register(_builtins);
        CoercionVerbs.Register(_builtins);
        LogicVerbs.Register(_builtins);
        StringVerbs.Register(_builtins);
        NumericVerbs.Register(_builtins);
        AggregationVerbs.Register(_builtins);
        EncodingVerbs.Register(_builtins);
        FinancialVerbs.Register(_builtins);
        GenerationVerbs.Register(_builtins);
        GeoVerbs.Register(_builtins);
        DateTimeVerbs.Register(_builtins);
        CollectionVerbs.Register(_builtins);
        ObjectVerbs.Register(_builtins);
    }

    /// <summary>
    /// Returns all built-in verbs as a dictionary. Used to populate TransformEngine.VerbRegistry.
    /// </summary>
    public Dictionary<string, Func<DynValue[], VerbContext, DynValue>> ToDictionary()
    {
        var dict = new Dictionary<string, Func<DynValue[], VerbContext, DynValue>>(_builtins);
        foreach (var kv in _custom)
            dict[kv.Key] = kv.Value;
        return dict;
    }
}
