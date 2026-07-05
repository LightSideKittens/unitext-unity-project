using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace LightSide
{
    public class TypedPathAccessor<TValue>
    {
        public readonly Func<object, TValue> Get;
        public readonly Action<object, TValue> Set;

        internal TypedPathAccessor(Func<object, TValue> get, Action<object, TValue> set)
        {
            Get = get;
            Set = set;
        }
    }

    public sealed class DirectFieldAccessor<TValue> where TValue : unmanaged
    {
        private readonly int _offset;

        internal DirectFieldAccessor(int offset)
        {
            _offset = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe TValue Get(object obj)
        {
            var ptr = (byte*)Unsafe.GetObjectPointer(obj);
            return *(TValue*)(ptr + _offset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Set(object obj, TValue value)
        {
            var ptr = (byte*)Unsafe.GetObjectPointer(obj);
            *(TValue*)(ptr + _offset) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CachedAccessor<TValue> CreateCached() => new(_offset);
    }

    public sealed class CachedAccessor<TValue> where TValue : unmanaged
    {
        private readonly int _offset;
        private unsafe byte* _ptr;

        internal CachedAccessor(int offset)
        {
            _offset = offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Bind(object obj)
        {
            _ptr = (byte*)Unsafe.GetObjectPointer(obj);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe TValue Get() => *(TValue*)(_ptr + _offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Set(TValue value) => *(TValue*)(_ptr + _offset) = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe ref TValue GetRef() => ref *(TValue*)(_ptr + _offset);
    }

    public class ObjectPathAccessor
    {
        public readonly Func<object, object> Get;
        public readonly Action<object, object> Set;

        internal ObjectPathAccessor(Func<object, object> get, Action<object, object> set)
        {
            Get = get;
            Set = set;
        }
    }

    public static class PathAccessor
    {
        public static TypedPathAccessor<TValue> Get<TValue>(object rootObj, string path)
            => Cache<TValue>.Get(rootObj, path);

        public static ObjectPathAccessor GetRef(object rootObj, string path)
            => RefCache.Get(rootObj, path);

        public static DirectFieldAccessor<TValue> GetDirect<TValue>(Type ownerType, string path) where TValue : unmanaged
            => DirectCache<TValue>.Get(ownerType, path);

        public static DirectFieldAccessor<TValue> GetDirect<TValue>(object ownerInstance, string path) where TValue : unmanaged
            => DirectCache<TValue>.Get(ownerInstance.GetType(), path);

        public static CachedAccessor<TValue> GetCached<TValue>(Type ownerType, string path) where TValue : unmanaged
            => DirectCache<TValue>.Get(ownerType, path).CreateCached();

        public static CachedAccessor<TValue> GetCached<TValue>(object ownerInstance, string path) where TValue : unmanaged
        {
            var cached = DirectCache<TValue>.Get(ownerInstance.GetType(), path).CreateCached();
            cached.Bind(ownerInstance);
            return cached;
        }

        #region Caches

        private static class Cache<TValue>
        {
            private static readonly ConcurrentDictionary<(Type, string), TypedPathAccessor<TValue>> dic = new();

            public static TypedPathAccessor<TValue> Get(object rootObj, string path)
            {
                var rootType = rootObj.GetType();
                var key = (rootType, path);

                if (dic.TryGetValue(key, out var acc))
                    return acc;

                acc = Build(rootObj, path);
                return dic.GetOrAdd(key, acc);
            }

            private static TypedPathAccessor<TValue> Build(object rootObj, string path)
            {
                var chain = BuildAccessorChain(rootObj, path);

                Func<object, TValue> getter = null;
                Action<object, TValue> setter = null;

                if (chain.CanRead)
                    getter = root => (TValue)chain.GetValue(root);

                if (chain.CanWrite)
                    setter = (root, val) => chain.SetValue(root, val);

                return new TypedPathAccessor<TValue>(getter, setter);
            }
        }

        private static class RefCache
        {
            private static readonly ConcurrentDictionary<(Type, string), ObjectPathAccessor> dic = new();

            public static ObjectPathAccessor Get(object rootObj, string path)
            {
                var rootType = rootObj.GetType();
                var key = (rootType, path);

                if (dic.TryGetValue(key, out var acc))
                    return acc;

                acc = Build(rootObj, path);
                return dic.GetOrAdd(key, acc);
            }

            private static ObjectPathAccessor Build(object rootObj, string path)
            {
                var chain = BuildAccessorChain(rootObj, path);

                Func<object, object> getter = chain.CanRead ? chain.GetValue : null;
                Action<object, object> setter = chain.CanWrite ? chain.SetValue : null;

                return new ObjectPathAccessor(getter, setter);
            }
        }

        private static class DirectCache<TValue> where TValue : unmanaged
        {
            private static readonly ConcurrentDictionary<(Type, string), DirectFieldAccessor<TValue>> dic = new();

            public static DirectFieldAccessor<TValue> Get(Type ownerType, string path)
            {
                var key = (ownerType, path);

                if (dic.TryGetValue(key, out var acc))
                    return acc;

                acc = Build(ownerType, path);
                return dic.GetOrAdd(key, acc);
            }

            private static DirectFieldAccessor<TValue> Build(Type ownerType, string path)
            {
                var totalOffset = 0;
                var currentType = ownerType;

                var parts = path.Split('.');
                foreach (var part in parts)
                {
                    var field = currentType.GetField(part, BF);
                    if (field == null)
                        throw new MissingFieldException(currentType.Name, part);

                    totalOffset += UnsafeUtility.GetFieldOffset(field);
                    currentType = field.FieldType;
                }

                return new DirectFieldAccessor<TValue>(totalOffset);
            }
        }

        #endregion

        #region Chain Building

        private static AccessorChain BuildAccessorChain(object rootInstance, string path)
        {
            ReadOnlySpan<char> input = path.AsSpan();
            var tokens = ArrayPool<Token>.Shared.Rent(32);
            int length = Tokenize(input, tokens);

            var steps = new List<IStep>();
            object curObj = rootInstance;
            Type curType = rootInstance.GetType();

            int i = 0;
            while (i < length)
            {
                var t = tokens[i];

                if (!t.IsIndex)
                {
                    var name = t.GetName(input);
                    var field = curType.GetField(name, BF);

                    if (field != null)
                    {
                        var (step, newObj, newType, consumed) =
                            TryBuildFieldChain(curObj, field, tokens, i, length, input);

                        steps.Add(step);
                        curObj = newObj;
                        curType = newType;
                        i += consumed;
                        continue;
                    }

                    var prop = curType.GetProperty(name, BF);
                    if (prop != null)
                    {
                        steps.Add(new PropertyStep(prop, curType));
                        curObj = prop.GetValue(curObj);
                        curType = curObj?.GetType() ?? prop.PropertyType;
                        i++;
                        continue;
                    }

                    throw new MissingMemberException(curType.Name, name);
                }
                else
                {
                    var index = t.Index;

                    if (curType.IsArray)
                    {
                        steps.Add(new ArrayStep(index));
                        curObj = ((Array)curObj)?.GetValue(index);
                        curType = curObj?.GetType() ?? curType.GetElementType();
                    }
                    else
                    {
                        var itemProp = curType.GetProperty("Item", BF, null, null, new[] { typeof(int) }, null)
                                       ?? throw new MissingMemberException(curType.Name, "Item[int]");

                        steps.Add(new IndexerStep(itemProp, index));
                        curObj = itemProp.GetValue(curObj, new object[] { index });
                        curType = curObj?.GetType() ?? itemProp.PropertyType;
                    }

                    i++;
                }
            }

            ArrayPool<Token>.Shared.Return(tokens);

            var lastStep = steps[steps.Count - 1];
            return new AccessorChain(steps.ToArray(), lastStep.CanRead, lastStep.CanWrite);
        }

        private static (IStep step, object curObj, Type curType, int consumed) TryBuildFieldChain(
            object startObj, FieldInfo firstField,
            Token[] tokens, int startIndex, int length, ReadOnlySpan<char> input)
        {
            var ownerIsValueType = startObj.GetType().IsValueType;

            var fields = new List<FieldInfo> { firstField };
            var offsets = ownerIsValueType ? null : new List<int> { UnsafeUtility.GetFieldOffset(firstField) };

            object curObj = firstField.GetValue(startObj);
            Type curType = curObj?.GetType() ?? firstField.FieldType;
            int consumed = 1;

            if (!firstField.FieldType.IsValueType || startIndex + 1 >= length)
            {
                var step = ownerIsValueType
                    ? (IStep)new ReflectionFieldStep(firstField)
                    : new UnsafeFieldStep(firstField);
                return (step, curObj, curType, consumed);
            }

            for (int i = startIndex + 1; i < length; i++)
            {
                var t = tokens[i];
                if (t.IsIndex) break;

                var name = t.GetName(input);
                var field = curType.GetField(name, BF);

                if (field == null) break;

                fields.Add(field);
                offsets?.Add(UnsafeUtility.GetFieldOffset(field));

                curObj = field.GetValue(curObj);
                var nextType = curObj?.GetType() ?? field.FieldType;
                consumed++;

                if (!field.FieldType.IsValueType)
                {
                    curType = nextType;
                    break;
                }

                curType = nextType;

                if (i + 1 >= length) break;
            }

            if (fields.Count == 1)
            {
                var step = ownerIsValueType
                    ? (IStep)new ReflectionFieldStep(firstField)
                    : new UnsafeFieldStep(firstField);
                return (step, curObj, curType, consumed);
            }

            var chainStep = ownerIsValueType
                ? (IStep)new ReflectionFieldChainStep(fields.ToArray())
                : new UnsafeFieldChainStep(fields.ToArray(), offsets.ToArray());
            return (chainStep, curObj, curType, consumed);
        }

        #endregion

        #region Steps

        private interface IStep
        {
            bool CanRead { get; }
            bool CanWrite { get; }
            object Get(object obj);
            void Set(object obj, object value);
        }

        private sealed class UnsafeFieldStep : IStep
        {
            private readonly int _offset;
            private readonly Type _fieldType;
            private readonly bool _isValueType;
            private readonly bool _canWrite;

            public bool CanRead => true;
            public bool CanWrite => _canWrite;

            public UnsafeFieldStep(FieldInfo field)
            {
                _offset = UnsafeUtility.GetFieldOffset(field);
                _fieldType = field.FieldType;
                _isValueType = _fieldType.IsValueType;
                _canWrite = !field.IsInitOnly && !field.IsLiteral;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe object Get(object obj)
            {
                var ptr = (byte*)Unsafe.GetObjectPointer(obj);
                var fieldPtr = ptr + _offset;
                return _isValueType ? ReadValueType(fieldPtr, _fieldType) : Unsafe.Read(fieldPtr);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe void Set(object obj, object value)
            {
                var ptr = (byte*)Unsafe.GetObjectPointer(obj);
                var fieldPtr = ptr + _offset;
                if (_isValueType)
                    WriteValueType(fieldPtr, value, _fieldType);
                else
                    Unsafe.Write(fieldPtr, value);
            }
        }

        private sealed class ReflectionFieldStep : IStep
        {
            private readonly FieldInfo _field;
            private readonly bool _canWrite;

            public bool CanRead => true;
            public bool CanWrite => _canWrite;

            public ReflectionFieldStep(FieldInfo field)
            {
                _field = field;
                _canWrite = !field.IsInitOnly && !field.IsLiteral;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object Get(object obj) => _field.GetValue(obj);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(object obj, object value) => _field.SetValue(obj, value);
        }

        private sealed class UnsafeFieldChainStep : IStep
        {
            private readonly int _totalOffset;
            private readonly Type _finalType;
            private readonly bool _finalIsValueType;
            private readonly bool _canWrite;

            public bool CanRead => true;
            public bool CanWrite => _canWrite;

            public UnsafeFieldChainStep(FieldInfo[] fields, int[] offsets)
            {
                _totalOffset = 0;
                for (int i = 0; i < offsets.Length; i++)
                    _totalOffset += offsets[i];

                var lastField = fields[fields.Length - 1];
                _finalType = lastField.FieldType;
                _finalIsValueType = _finalType.IsValueType;
                _canWrite = !lastField.IsInitOnly && !lastField.IsLiteral;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe object Get(object obj)
            {
                var ptr = (byte*)Unsafe.GetObjectPointer(obj);
                var fieldPtr = ptr + _totalOffset;
                return _finalIsValueType ? ReadValueType(fieldPtr, _finalType) : Unsafe.Read(fieldPtr);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe void Set(object obj, object value)
            {
                var ptr = (byte*)Unsafe.GetObjectPointer(obj);
                var fieldPtr = ptr + _totalOffset;
                if (_finalIsValueType)
                    WriteValueType(fieldPtr, value, _finalType);
                else
                    Unsafe.Write(fieldPtr, value);
            }
        }

        private sealed class ReflectionFieldChainStep : IStep
        {
            private readonly FieldInfo[] _fields;
            private readonly bool _canWrite;

            public bool CanRead => true;
            public bool CanWrite => _canWrite;

            public ReflectionFieldChainStep(FieldInfo[] fields)
            {
                _fields = fields;
                var lastField = fields[fields.Length - 1];
                _canWrite = !lastField.IsInitOnly && !lastField.IsLiteral;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object Get(object obj)
            {
                var cur = obj;
                for (int i = 0; i < _fields.Length; i++)
                    cur = _fields[i].GetValue(cur);
                return cur;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(object obj, object value)
            {
                if (_fields.Length == 1)
                {
                    _fields[0].SetValue(obj, value);
                    return;
                }

                var values = new object[_fields.Length];
                values[0] = obj;
                for (int i = 0; i < _fields.Length - 1; i++)
                    values[i + 1] = _fields[i].GetValue(values[i]);

                _fields[_fields.Length - 1].SetValue(values[_fields.Length - 1], value);

                for (int i = _fields.Length - 2; i >= 0; i--)
                    _fields[i].SetValue(values[i], values[i + 1]);
            }
        }

        private sealed class PropertyStep : IStep
        {
            private readonly Func<object, object> _getter;
            private readonly Action<object, object> _setter;

            public bool CanRead => _getter != null;
            public bool CanWrite => _setter != null;

            private static readonly MethodInfo GetterHelperMethod = typeof(PropertyStep)
                .GetMethod(nameof(CreateTypedGetter), BindingFlags.NonPublic | BindingFlags.Static);

            private static readonly MethodInfo SetterHelperMethod = typeof(PropertyStep)
                .GetMethod(nameof(CreateTypedSetter), BindingFlags.NonPublic | BindingFlags.Static);

            public PropertyStep(PropertyInfo prop, Type ownerType)
            {
                if (prop.CanRead && prop.GetMethod != null)
                    _getter = CreateGetter(prop, ownerType);

                if (prop.CanWrite && prop.SetMethod != null)
                    _setter = CreateSetter(prop, ownerType);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object Get(object obj) => _getter(obj);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(object obj, object value) => _setter(obj, value);

            private static Func<object, object> CreateGetter(PropertyInfo prop, Type ownerType)
            {
                var helper = GetterHelperMethod.MakeGenericMethod(ownerType, prop.PropertyType);
                return (Func<object, object>)helper.Invoke(null, new object[] { prop.GetMethod });
            }

            private static Func<object, object> CreateTypedGetter<TOwner, TValue>(MethodInfo getMethod)
            {
                var getter = (Func<TOwner, TValue>)Delegate.CreateDelegate(typeof(Func<TOwner, TValue>), getMethod);
                return obj => getter((TOwner)obj);
            }

            private static Action<object, object> CreateSetter(PropertyInfo prop, Type ownerType)
            {
                var helper = SetterHelperMethod.MakeGenericMethod(ownerType, prop.PropertyType);
                return (Action<object, object>)helper.Invoke(null, new object[] { prop.SetMethod });
            }

            private static Action<object, object> CreateTypedSetter<TOwner, TValue>(MethodInfo setMethod)
            {
                var setter = (Action<TOwner, TValue>)Delegate.CreateDelegate(typeof(Action<TOwner, TValue>), setMethod);
                return (obj, val) => setter((TOwner)obj, (TValue)val);
            }
        }

        private sealed class ArrayStep : IStep
        {
            private readonly int _index;

            public bool CanRead => true;
            public bool CanWrite => true;

            public ArrayStep(int index)
            {
                _index = index;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object Get(object obj) => ((Array)obj).GetValue(_index);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(object obj, object value) => ((Array)obj).SetValue(value, _index);
        }

        private sealed class IndexerStep : IStep
        {
            private readonly PropertyInfo _prop;
            private readonly object[] _indexArgs;

            public bool CanRead => _prop.CanRead;
            public bool CanWrite => _prop.CanWrite;

            public IndexerStep(PropertyInfo prop, int index)
            {
                _prop = prop;
                _indexArgs = new object[] { index };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object Get(object obj) => _prop.GetValue(obj, _indexArgs);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(object obj, object value) => _prop.SetValue(obj, value, _indexArgs);
        }

        #endregion

        #region Value Type Helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe object ReadValueType(byte* ptr, Type type)
        {
            if (type == typeof(int)) return *(int*)ptr;
            if (type == typeof(float)) return *(float*)ptr;
            if (type == typeof(bool)) return *(bool*)ptr;
            if (type == typeof(double)) return *(double*)ptr;
            if (type == typeof(long)) return *(long*)ptr;
            if (type == typeof(byte)) return *ptr;
            if (type == typeof(short)) return *(short*)ptr;
            if (type == typeof(uint)) return *(uint*)ptr;
            if (type == typeof(ulong)) return *(ulong*)ptr;
            if (type == typeof(char)) return *(char*)ptr;
            if (type == typeof(sbyte)) return *(sbyte*)ptr;
            if (type == typeof(ushort)) return *(ushort*)ptr;

            if (type == typeof(UnityEngine.Vector2)) return *(UnityEngine.Vector2*)ptr;
            if (type == typeof(UnityEngine.Vector3)) return *(UnityEngine.Vector3*)ptr;
            if (type == typeof(UnityEngine.Vector4)) return *(UnityEngine.Vector4*)ptr;
            if (type == typeof(UnityEngine.Color)) return *(UnityEngine.Color*)ptr;
            if (type == typeof(UnityEngine.Color32)) return *(UnityEngine.Color32*)ptr;
            if (type == typeof(UnityEngine.Quaternion)) return *(UnityEngine.Quaternion*)ptr;
            if (type == typeof(UnityEngine.Rect)) return *(UnityEngine.Rect*)ptr;
            if (type == typeof(UnityEngine.Vector2Int)) return *(UnityEngine.Vector2Int*)ptr;
            if (type == typeof(UnityEngine.Vector3Int)) return *(UnityEngine.Vector3Int*)ptr;

            return ReadArbitraryValueType(ptr, type);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteValueType(byte* ptr, object value, Type type)
        {
            if (type == typeof(int)) { *(int*)ptr = (int)value; return; }
            if (type == typeof(float)) { *(float*)ptr = (float)value; return; }
            if (type == typeof(bool)) { *(bool*)ptr = (bool)value; return; }
            if (type == typeof(double)) { *(double*)ptr = (double)value; return; }
            if (type == typeof(long)) { *(long*)ptr = (long)value; return; }
            if (type == typeof(byte)) { *ptr = (byte)value; return; }
            if (type == typeof(short)) { *(short*)ptr = (short)value; return; }
            if (type == typeof(uint)) { *(uint*)ptr = (uint)value; return; }
            if (type == typeof(ulong)) { *(ulong*)ptr = (ulong)value; return; }
            if (type == typeof(char)) { *(char*)ptr = (char)value; return; }
            if (type == typeof(sbyte)) { *(sbyte*)ptr = (sbyte)value; return; }
            if (type == typeof(ushort)) { *(ushort*)ptr = (ushort)value; return; }

            if (type == typeof(UnityEngine.Vector2)) { *(UnityEngine.Vector2*)ptr = (UnityEngine.Vector2)value; return; }
            if (type == typeof(UnityEngine.Vector3)) { *(UnityEngine.Vector3*)ptr = (UnityEngine.Vector3)value; return; }
            if (type == typeof(UnityEngine.Vector4)) { *(UnityEngine.Vector4*)ptr = (UnityEngine.Vector4)value; return; }
            if (type == typeof(UnityEngine.Color)) { *(UnityEngine.Color*)ptr = (UnityEngine.Color)value; return; }
            if (type == typeof(UnityEngine.Color32)) { *(UnityEngine.Color32*)ptr = (UnityEngine.Color32)value; return; }
            if (type == typeof(UnityEngine.Quaternion)) { *(UnityEngine.Quaternion*)ptr = (UnityEngine.Quaternion)value; return; }
            if (type == typeof(UnityEngine.Rect)) { *(UnityEngine.Rect*)ptr = (UnityEngine.Rect)value; return; }
            if (type == typeof(UnityEngine.Vector2Int)) { *(UnityEngine.Vector2Int*)ptr = (UnityEngine.Vector2Int)value; return; }
            if (type == typeof(UnityEngine.Vector3Int)) { *(UnityEngine.Vector3Int*)ptr = (UnityEngine.Vector3Int)value; return; }

            WriteArbitraryValueType(ptr, value, type);
        }

        private static unsafe object ReadArbitraryValueType(byte* ptr, Type type)
        {
            var size = UnsafeUtility.SizeOf(type);
            var boxed = Activator.CreateInstance(type);

            var handle = GCHandle.Alloc(boxed, GCHandleType.Pinned);
            try
            {
                var boxedPtr = (byte*)handle.AddrOfPinnedObject();
                UnsafeUtility.MemCpy(boxedPtr, ptr, size);
            }
            finally
            {
                handle.Free();
            }

            return boxed;
        }

        private static unsafe void WriteArbitraryValueType(byte* ptr, object value, Type type)
        {
            var size = UnsafeUtility.SizeOf(type);

            var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            try
            {
                var boxedPtr = (byte*)handle.AddrOfPinnedObject();
                UnsafeUtility.MemCpy(ptr, boxedPtr, size);
            }
            finally
            {
                handle.Free();
            }
        }

        #endregion

        #region Accessor Chain

        private sealed class AccessorChain
        {
            private readonly IStep[] _steps;
            public readonly bool CanRead;
            public readonly bool CanWrite;

            public AccessorChain(IStep[] steps, bool canRead, bool canWrite)
            {
                _steps = steps;
                CanRead = canRead;
                CanWrite = canWrite;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public object GetValue(object root)
            {
                var cur = root;
                for (int i = 0; i < _steps.Length; i++)
                    cur = _steps[i].Get(cur);
                return cur;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetValue(object root, object value)
            {
                var cur = root;
                var last = _steps.Length - 1;
                for (int i = 0; i < last; i++)
                    cur = _steps[i].Get(cur);
                _steps[last].Set(cur, value);
            }
        }

        #endregion

        #region Tokenizer

        private readonly struct Token
        {
            private readonly int _start;
            private readonly short _length;
            public readonly int Index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Token(int idx) { _start = 0; _length = 0; Index = idx; }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Token(int start, int length) { _start = start; _length = (short)length; Index = -1; }

            public bool IsIndex => Index >= 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public string GetName(ReadOnlySpan<char> src) => new(src.Slice(_start, _length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Tokenize(ReadOnlySpan<char> input, Span<Token> dst)
        {
            int len = input.Length, i = 0, count = 0;

            while (i < len)
            {
                switch (input[i])
                {
                    case '.':
                        i++;
                        continue;

                    case '[':
                        int val = 0;
                        for (i++; i < len && input[i] != ']'; i++)
                            val = val * 10 + (input[i] - '0');
                        i++;
                        dst[count++] = new Token(val);
                        continue;
                }

                int start = i;
                int rel = input[i..].IndexOfAny('.', '[');
                i = rel >= 0 ? i + rel : len;
                dst[count++] = new Token(start, i - start);
            }

            return count;
        }

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        #endregion
    }

    internal static unsafe class Unsafe
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RefHolder { public object Ref; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void* GetObjectPointer(object obj)
        {
            RefHolder holder;
            holder.Ref = obj;
            return *(void**)UnsafeUtility.AddressOf(ref holder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object Read(void* source)
        {
            RefHolder holder = default;
            *(IntPtr*)UnsafeUtility.AddressOf(ref holder) = *(IntPtr*)source;
            return holder.Ref;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write(void* destination, object value)
        {
            RefHolder holder;
            holder.Ref = value;
            *(IntPtr*)destination = *(IntPtr*)UnsafeUtility.AddressOf(ref holder);
        }
    }

}
