using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using XNode;
using Xprees.Variables.Base;
using Object = UnityEngine.Object;

namespace Xprees.Graph.Core.Editor.Debugger
{
    /// Reflection helper over the variables package (no change to the package): finds the variables a node references and reads or writes
    /// their runtime value through the CurrentValue accessors, so aggregations compute and onValueChanged fires.
    public static class VariableAccess
    {
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new();
        private static readonly Dictionary<Type, PropertyInfo> ValueProperties = new();
        private static readonly Dictionary<Type, Type> ValueTypes = new();
        private static readonly Dictionary<Type, ReferenceAccessor> ReferenceAccessors = new();

        private sealed class ReferenceAccessor
        {
            public FieldInfo UseInlined;
            public FieldInfo Variable;
        }

        /// Values the Variables panel can edit: primitives and enums.
        public static bool IsEditableType(Type type)
        {
            if (type == null) return false;
            if (type.IsEnum) return true;

            return type == typeof(bool) || type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double)
                   || type == typeof(string);
        }

        /// How the Variables panel shows a value: primitive and enum get a field, an Object reference an object field, anything else a read-only view.
        public enum ValueKind
        {
            None,
            Primitive,
            Enum,
            UnityObject,
            Complex,
        }

        public static ValueKind Classify(Type type)
        {
            if (type == null) return ValueKind.None;
            if (type.IsEnum) return ValueKind.Enum;
            if (IsEditableType(type)) return ValueKind.Primitive;
            if (typeof(Object).IsAssignableFrom(type)) return ValueKind.UnityObject;

            return ValueKind.Complex;
        }

        /// Writes an Object reference through the CurrentValue setter (runtime value only). False for other value kinds or an incompatible object.
        public static bool TryWriteObject(VariableBaseSO variable, Object value)
        {
            var type = ValueTypeOf(variable);
            if (Classify(type) != ValueKind.UnityObject || IsAggregation(variable.GetType())) return false;
            if (value && !type.IsInstanceOfType(value)) return false;

            var property = ValuePropertyOf(variable);
            if (property == null || !property.CanWrite) return false;

            try
            {
                property.SetValue(variable, value ? value : null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// Editable when the value type is a primitive or enum and the variable is not a computed aggregation.
        public static bool IsEditable(VariableBaseSO variable)
        {
            if (!variable || IsAggregation(variable.GetType())) return false;

            return IsEditableType(ValueTypeOf(variable));
        }

        public static bool IsAggregation(Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(VariableAggregationBaseSO<>)) return true;
            }

            return false;
        }

        /// The T of VariableBaseSO of T, null when the type is not a typed variable.
        public static Type ValueTypeOf(VariableBaseSO variable)
        {
            if (!variable) return null;

            var type = variable.GetType();
            if (ValueTypes.TryGetValue(type, out var cached)) return cached;

            Type result = null;
            for (var t = type; t != null; t = t.BaseType)
            {
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(VariableBaseSO<>)) continue;

                result = t.GetGenericArguments()[0];
                break;
            }

            ValueTypes[type] = result;
            return result;
        }

        /// Reads through the CurrentValue getter. False when the variable has no typed value or the getter threw.
        public static bool TryRead(VariableBaseSO variable, out object value)
        {
            value = null;
            var property = ValuePropertyOf(variable);
            if (property == null) return false;

            try
            {
                value = property.GetValue(variable);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// Writes through the CurrentValue setter (fires onValueChanged). Runtime value only, the authored default is untouched.
        public static bool TryWrite(VariableBaseSO variable, object value)
        {
            if (!IsEditable(variable)) return false;

            var property = ValuePropertyOf(variable);
            if (property == null || !property.CanWrite) return false;

            try
            {
                var type = ValueTypeOf(variable);
                var converted = type.IsEnum ? Enum.ToObject(type, value) : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                property.SetValue(variable, converted);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string Format(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string text:
                    return text.Length > 60 ? $"\"{text.Substring(0, 57)}...\"" : $"\"{text}\"";
                case float f:
                    return f.ToString("0.###", CultureInfo.InvariantCulture);
                case double d:
                    return d.ToString("0.###", CultureInfo.InvariantCulture);
                case bool b:
                    return b ? "true" : "false";
                case Object obj:
                    return obj ? obj.name : "null";
                case ICollection collection:
                    return $"{value.GetType().Name} ({collection.Count})";
                default:
                    return value.ToString();
            }
        }

        /// The variable a field value stands for: the variable itself, or the one a reference points to (not for inlined references).
        public static VariableBaseSO ResolveVariable(object fieldValue)
        {
            switch (fieldValue)
            {
                case null:
                    return null;
                case VariableBaseSO variable:
                    return variable ? variable : null;
            }

            var accessor = ReferenceAccessorOf(fieldValue.GetType());
            if (accessor == null) return null;
            if (accessor.UseInlined.GetValue(fieldValue) is true) return null;

            var referenced = accessor.Variable.GetValue(fieldValue) as VariableBaseSO;
            return referenced ? referenced : null;
        }

        /// The field of a node type that holds a variable or a reference (single value, not collections), null otherwise.
        public static FieldInfo FindVariableField(Type nodeType, string fieldName)
        {
            foreach (var field in VariableFieldsOf(nodeType))
            {
                if (field.Name == fieldName) return field;
            }

            return null;
        }

        /// Variables the node references, in field order.
        public static void CollectVariables(Node node, List<VariableBaseSO> into)
        {
            if (!node) return;

            foreach (var field in VariableFieldsOf(node.GetType()))
            {
                object value;
                try
                {
                    value = field.GetValue(node);
                }
                catch (Exception)
                {
                    continue;
                }

                if (value is IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                    {
                        AddVariable(item, into);
                    }

                    continue;
                }

                AddVariable(value, into);
            }
        }

        private static void AddVariable(object item, List<VariableBaseSO> into)
        {
            var variable = ResolveVariable(item);
            if (variable && !into.Contains(variable)) into.Add(variable);
        }

        /// Single-value fields only: collection fields are listed by the panel but not drawn inline in the node body.
        public static bool IsCollectionField(FieldInfo field) =>
            field.FieldType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(field.FieldType);

        private static FieldInfo[] VariableFieldsOf(Type nodeType)
        {
            if (FieldCache.TryGetValue(nodeType, out var cached)) return cached;

            var result = new List<FieldInfo>();
            for (var t = nodeType; t != null && t != typeof(Node); t = t.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in t.GetFields(flags))
                {
                    var isSerialized = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                    if (!isSerialized || field.IsNotSerialized) continue;
                    if (IsVariableType(ElementTypeOf(field.FieldType))) result.Add(field);
                }
            }

            var array = result.ToArray();
            FieldCache[nodeType] = array;
            return array;
        }

        private static Type ElementTypeOf(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return type;
        }

        private static bool IsVariableType(Type type) => typeof(VariableBaseSO).IsAssignableFrom(type) || ReferenceAccessorOf(type) != null;

        private static ReferenceAccessor ReferenceAccessorOf(Type type)
        {
            if (ReferenceAccessors.TryGetValue(type, out var cached)) return cached;

            ReferenceAccessor accessor = null;
            for (var t = type; t != null; t = t.BaseType)
            {
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(ReferenceBase<>)) continue;

                var useInlined = t.GetField(nameof(ReferenceBase<int>.useInlined));
                var variable = t.GetField(nameof(ReferenceBase<int>.variable));
                if (useInlined != null && variable != null) accessor = new ReferenceAccessor { UseInlined = useInlined, Variable = variable };
                break;
            }

            ReferenceAccessors[type] = accessor;
            return accessor;
        }

        private static PropertyInfo ValuePropertyOf(VariableBaseSO variable)
        {
            if (!variable) return null;

            var type = variable.GetType();
            if (ValueProperties.TryGetValue(type, out var cached)) return cached;

            PropertyInfo property = null;
            for (var t = type; t != null; t = t.BaseType)
            {
                if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(VariableBaseSO<>)) continue;

                // The virtual property declared on the generic base resolves to the most derived override (aggregations compute their value).
                property = t.GetProperty(nameof(VariableBaseSO<int>.CurrentValue));
                break;
            }

            ValueProperties[type] = property;
            return property;
        }
    }
}