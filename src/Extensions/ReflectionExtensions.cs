using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace TentBag.Extensions;

public static class ReflectionExtensions {
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly Dictionary<Type, FieldInfo?> FieldCache = new();

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static T? GetField<T>(this object obj, string name) {
        return (T?)GetFieldInfo(obj, name)?.GetValue(obj);
    }

    public static void SetField(this object obj, string name, object? val) {
        GetFieldInfo(obj, name)?.SetValue(obj, val);
    }

    private static FieldInfo? GetFieldInfo(object obj, string name) {
        return FieldCache.ComputeIfAbsent<Type, FieldInfo?>(obj.GetType(), type => type.GetField(name, Flags));
    }
}
