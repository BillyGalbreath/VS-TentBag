using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TentBag.Extensions;

public static class CoreExtensions {
    [SuppressMessage("ReSharper", "InvertIf")]
    public static TV? ComputeIfAbsent<TK, TV>(this Dictionary<TK, TV?> dict, TK key, Func<TK, TV> func) where TK : notnull {
        if (!dict.TryGetValue(key, out TV? value)) {
            value = func(key);
            dict.Add(key, value);
        }

        return value;
    }
}
