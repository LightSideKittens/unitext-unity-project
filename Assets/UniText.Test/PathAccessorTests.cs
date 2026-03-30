using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LightSide;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using Debug = UnityEngine.Debug;
using FaceInfo = UnityEngine.TextCore.FaceInfo;
using GlyphMetrics = UnityEngine.TextCore.GlyphMetrics;
using GlyphRect = UnityEngine.TextCore.GlyphRect;

public static class PathAccessorTests
{
    #region Test Data Structures

    [StructLayout(LayoutKind.Sequential)]
    public struct PrimitiveStruct
    {
        public int intField;
        public float floatField;
        public bool boolField;
        public double doubleField;
        public long longField;
        public byte byteField;
        public short shortField;
        public uint uintField;
        public ulong ulongField;
        public char charField;
        public sbyte sbyteField;
        public ushort ushortField;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UnityTypesStruct
    {
        public Vector2 vector2Field;
        public Vector3 vector3Field;
        public Vector4 vector4Field;
        public Color colorField;
        public Color32 color32Field;
        public Quaternion quaternionField;
        public Rect rectField;
        public Vector2Int vector2IntField;
        public Vector3Int vector3IntField;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CustomStruct
    {
        public int a;
        public float b;
        public Vector3 c;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InnerStruct
    {
        public int value;
        public float scale;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MiddleStruct
    {
        public InnerStruct inner;
        public int middleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OuterStruct
    {
        public MiddleStruct middle;
        public int outerValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StructWithRef
    {
        public int id;
        public string name;
        public object data;
    }

    public class SimpleClass
    {
        public int publicInt;
        private int privateInt;
        public readonly int readonlyInt;
        public float floatValue;
        public string stringValue;
        public Vector3 vectorValue;

        public int PrivateInt
        {
            get => privateInt;
            set => privateInt = value;
        }

        public SimpleClass()
        {
            readonlyInt = 42;
        }

        public SimpleClass(int pubInt, int privInt, float fVal, string sVal, Vector3 vVal)
        {
            publicInt = pubInt;
            privateInt = privInt;
            floatValue = fVal;
            stringValue = sVal;
            vectorValue = vVal;
            readonlyInt = 42;
        }
    }

    public class OuterClass
    {
        public SimpleClass nested;
        public int outerValue;
    }

    public class ClassWithStruct
    {
        public PrimitiveStruct primitiveStruct;
        public UnityTypesStruct unityStruct;
        public InnerStruct innerStruct;
    }

    [Preserve]
    public class PropertyClass
    {
        private int _backingField;
        private Vector3 _vector;

        [Preserve]
        public int ReadWriteProperty
        {
            get => _backingField;
            set => _backingField = value;
        }

        [Preserve]
        public int ReadOnlyProperty => _backingField * 2;

        [Preserve]
        public Vector3 VectorProperty
        {
            get => _vector;
            set => _vector = value;
        }
    }

    public class ArrayClass
    {
        public int[] intArray;
        public float[] floatArray;
        public Vector3[] vectorArray;
        public SimpleClass[] classArray;
    }

    public class ListClass
    {
        public List<int> intList;
        public List<Vector3> vectorList;
        public List<SimpleClass> classList;
    }

    public class Level1
    {
        public Level2 level2;
    }

    public class Level2
    {
        public Level3 level3;
    }

    public class Level3
    {
        public Level4 level4;
    }

    public class Level4
    {
        public int deepValue;
        public InnerStruct deepStruct;
    }

    #endregion

    #region Main Test Runner

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Tests/Run All PathAccessor Tests")]
#endif
    public static void RunAllTests()
    {
        var passed = 0;
        var failed = 0;
        var sw = Stopwatch.StartNew();

        Debug.Log("========== PathAccessor Tests Started ==========");

        RunTest("Primitive: int", TestPrimitiveInt, ref passed, ref failed);
        RunTest("Primitive: float", TestPrimitiveFloat, ref passed, ref failed);
        RunTest("Primitive: bool", TestPrimitiveBool, ref passed, ref failed);
        RunTest("Primitive: double", TestPrimitiveDouble, ref passed, ref failed);
        RunTest("Primitive: long", TestPrimitiveLong, ref passed, ref failed);
        RunTest("Primitive: byte", TestPrimitiveByte, ref passed, ref failed);
        RunTest("Primitive: short", TestPrimitiveShort, ref passed, ref failed);
        RunTest("Primitive: uint", TestPrimitiveUint, ref passed, ref failed);
        RunTest("Primitive: ulong", TestPrimitiveUlong, ref passed, ref failed);
        RunTest("Primitive: char", TestPrimitiveChar, ref passed, ref failed);
        RunTest("Primitive: sbyte", TestPrimitiveSbyte, ref passed, ref failed);
        RunTest("Primitive: ushort", TestPrimitiveUshort, ref passed, ref failed);

        RunTest("Unity: Vector2", TestUnityVector2, ref passed, ref failed);
        RunTest("Unity: Vector3", TestUnityVector3, ref passed, ref failed);
        RunTest("Unity: Vector4", TestUnityVector4, ref passed, ref failed);
        RunTest("Unity: Color", TestUnityColor, ref passed, ref failed);
        RunTest("Unity: Color32", TestUnityColor32, ref passed, ref failed);
        RunTest("Unity: Quaternion", TestUnityQuaternion, ref passed, ref failed);
        RunTest("Unity: Rect", TestUnityRect, ref passed, ref failed);
        RunTest("Unity: Vector2Int", TestUnityVector2Int, ref passed, ref failed);
        RunTest("Unity: Vector3Int", TestUnityVector3Int, ref passed, ref failed);

        RunTest("Custom: Arbitrary struct", TestCustomStruct, ref passed, ref failed);

        RunTest("Chain: Single level", TestStructChainSingle, ref passed, ref failed);
        RunTest("Chain: Two levels", TestStructChainTwo, ref passed, ref failed);
        RunTest("Chain: Three levels", TestStructChainThree, ref passed, ref failed);

        RunTest("Ref: String in struct", TestStringInStruct, ref passed, ref failed);
        RunTest("Ref: Object in struct", TestObjectInStruct, ref passed, ref failed);

        RunTest("Class: Public int", TestClassPublicInt, ref passed, ref failed);
        RunTest("Class: Private int", TestClassPrivateInt, ref passed, ref failed);
        RunTest("Class: Float", TestClassFloat, ref passed, ref failed);
        RunTest("Class: String", TestClassString, ref passed, ref failed);
        RunTest("Class: Vector3", TestClassVector3, ref passed, ref failed);
        RunTest("Class: Readonly int (read only)", TestClassReadonlyInt, ref passed, ref failed);

        RunTest("Nested: Class in class", TestNestedClass, ref passed, ref failed);
        RunTest("Nested: Deep path", TestDeepNestedPath, ref passed, ref failed);

        RunTest("ClassStruct: Primitive struct field", TestClassWithPrimitiveStruct, ref passed, ref failed);
        RunTest("ClassStruct: Unity struct field", TestClassWithUnityStruct, ref passed, ref failed);
        RunTest("ClassStruct: Nested struct in class", TestClassWithNestedStruct, ref passed, ref failed);

        RunTest("Property: Read/Write", TestPropertyReadWrite, ref passed, ref failed);
        RunTest("Property: Read-only", TestPropertyReadOnly, ref passed, ref failed);
        RunTest("Property: Vector3", TestPropertyVector, ref passed, ref failed);

        RunTest("Array: int[]", TestArrayInt, ref passed, ref failed);
        RunTest("Array: float[]", TestArrayFloat, ref passed, ref failed);
        RunTest("Array: Vector3[]", TestArrayVector, ref passed, ref failed);
        RunTest("Array: class[]", TestArrayClass, ref passed, ref failed);
        RunTest("Array: Nested access", TestArrayNestedAccess, ref passed, ref failed);

        RunTest("List: int", TestListInt, ref passed, ref failed);
        RunTest("List: Vector3", TestListVector, ref passed, ref failed);
        RunTest("List: class", TestListClass, ref passed, ref failed);

        RunTest("Complex: Array->Field", TestComplexArrayField, ref passed, ref failed);
        RunTest("Complex: List->Field->Struct", TestComplexListFieldStruct, ref passed, ref failed);
        RunTest("Complex: Class->Struct->Struct->Field", TestComplexDeepStructChain, ref passed, ref failed);

        RunTest("FaceInfo: m_FaceIndex", TestFaceInfoFaceIndex, ref passed, ref failed);
        RunTest("FaceInfo: m_PointSize", TestFaceInfoPointSize, ref passed, ref failed);
        RunTest("FaceInfo: m_LineHeight", TestFaceInfoLineHeight, ref passed, ref failed);

        RunTest("GlyphRect: All fields", TestGlyphRect, ref passed, ref failed);

        RunTest("GlyphMetrics: All fields", TestGlyphMetrics, ref passed, ref failed);

        RunTest("Cache: Same accessor reused", TestCacheReuse, ref passed, ref failed);

        RunTest("Write: Primitive in struct", TestWritePrimitiveStruct, ref passed, ref failed);
        RunTest("Write: Unity type in struct", TestWriteUnityStruct, ref passed, ref failed);
        RunTest("Write: Class field", TestWriteClassField, ref passed, ref failed);
        RunTest("Write: Property", TestWriteProperty, ref passed, ref failed);
        RunTest("Write: Array element", TestWriteArrayElement, ref passed, ref failed);

        RunTest("Cached: Get simple field", TestCachedGetSimple, ref passed, ref failed);
        RunTest("Cached: Set simple field", TestCachedSetSimple, ref passed, ref failed);
        RunTest("Cached: GetRef modify", TestCachedGetRef, ref passed, ref failed);
        RunTest("Cached: Nested path", TestCachedNestedPath, ref passed, ref failed);
        RunTest("Cached: Rebind", TestCachedRebind, ref passed, ref failed);

        RunTest("Null: String field read", TestNullStringRead, ref passed, ref failed);
        RunTest("Null: Object field read", TestNullObjectRead, ref passed, ref failed);

        RunTest("Perf: 10000 boxed struct reads", TestPerformanceStructRead, ref passed, ref failed);
        RunTest("Perf: 10000 class reads", TestPerformanceClassRead, ref passed, ref failed);
        RunTest("Perf: 10000 class->struct reads", TestPerformanceClassNestedStruct, ref passed, ref failed);
        RunTest("Perf: vs Direct access", TestPerformanceVsDirectAccess, ref passed, ref failed);
        RunTest("Perf: vs Reflection", TestPerformanceVsReflection, ref passed, ref failed);
        RunTest("Perf: DirectFieldAccessor", TestPerformanceDirectAccessor, ref passed, ref failed);
        RunTest("Perf: DirectFieldAccessor nested", TestPerformanceDirectAccessorNestedStruct, ref passed, ref failed);
        RunTest("Perf: ALL METHODS COMPARISON", TestPerformanceAllMethods, ref passed, ref failed);

        RunTest("Thread: Concurrent reads", TestConcurrentReads, ref passed, ref failed);
        RunTest("Thread: Concurrent different keys", TestConcurrentDifferentKeys, ref passed, ref failed);

        sw.Stop();
        Debug.Log($"========== PathAccessor Tests Finished ==========");
        Debug.Log($"Passed: {passed}, Failed: {failed}, Total: {passed + failed}");
        Debug.Log($"Time: {sw.ElapsedMilliseconds}ms");

        if (failed > 0)
            Debug.LogError($"{failed} TESTS FAILED!");
        else
            Debug.Log("ALL TESTS PASSED!");
    }

    private static void RunTest(string name, Func<bool> test, ref int passed, ref int failed)
    {
        try
        {
            if (test())
            {
                passed++;
                Debug.Log($"[PASS] {name}");
            }
            else
            {
                failed++;
                Debug.LogError($"[FAIL] {name}");
            }
        }
        catch (Exception e)
        {
            failed++;
            Debug.LogError($"[EXCEPTION] {name}: {e.Message}\n{e.StackTrace}");
        }
    }

    #endregion

    #region Primitive Type Tests

    private static bool TestPrimitiveInt()
    {
        var s = new PrimitiveStruct { intField = 12345 };
        var acc = PathAccessor.Get<int>(s, "intField");
        return acc.Get(s) == 12345;
    }

    private static bool TestPrimitiveFloat()
    {
        var s = new PrimitiveStruct { floatField = 3.14159f };
        var acc = PathAccessor.Get<float>(s, "floatField");
        return Math.Abs(acc.Get(s) - 3.14159f) < 0.0001f;
    }

    private static bool TestPrimitiveBool()
    {
        var s = new PrimitiveStruct { boolField = true };
        var acc = PathAccessor.Get<bool>(s, "boolField");
        return acc.Get(s);
    }

    private static bool TestPrimitiveDouble()
    {
        var s = new PrimitiveStruct { doubleField = 2.718281828 };
        var acc = PathAccessor.Get<double>(s, "doubleField");
        return Math.Abs(acc.Get(s) - 2.718281828) < 0.0000001;
    }

    private static bool TestPrimitiveLong()
    {
        var s = new PrimitiveStruct { longField = 9223372036854775807L };
        var acc = PathAccessor.Get<long>(s, "longField");
        return acc.Get(s) == 9223372036854775807L;
    }

    private static bool TestPrimitiveByte()
    {
        var s = new PrimitiveStruct { byteField = 255 };
        var acc = PathAccessor.Get<byte>(s, "byteField");
        return acc.Get(s) == 255;
    }

    private static bool TestPrimitiveShort()
    {
        var s = new PrimitiveStruct { shortField = -32768 };
        var acc = PathAccessor.Get<short>(s, "shortField");
        return acc.Get(s) == -32768;
    }

    private static bool TestPrimitiveUint()
    {
        var s = new PrimitiveStruct { uintField = 4294967295U };
        var acc = PathAccessor.Get<uint>(s, "uintField");
        return acc.Get(s) == 4294967295U;
    }

    private static bool TestPrimitiveUlong()
    {
        var s = new PrimitiveStruct { ulongField = 18446744073709551615UL };
        var acc = PathAccessor.Get<ulong>(s, "ulongField");
        return acc.Get(s) == 18446744073709551615UL;
    }

    private static bool TestPrimitiveChar()
    {
        var s = new PrimitiveStruct { charField = 'Z' };
        var acc = PathAccessor.Get<char>(s, "charField");
        return acc.Get(s) == 'Z';
    }

    private static bool TestPrimitiveSbyte()
    {
        var s = new PrimitiveStruct { sbyteField = -128 };
        var acc = PathAccessor.Get<sbyte>(s, "sbyteField");
        return acc.Get(s) == -128;
    }

    private static bool TestPrimitiveUshort()
    {
        var s = new PrimitiveStruct { ushortField = 65535 };
        var acc = PathAccessor.Get<ushort>(s, "ushortField");
        return acc.Get(s) == 65535;
    }

    #endregion

    #region Unity Type Tests

    private static bool TestUnityVector2()
    {
        var s = new UnityTypesStruct { vector2Field = new Vector2(1.5f, 2.5f) };
        var acc = PathAccessor.Get<Vector2>(s, "vector2Field");
        var v = acc.Get(s);
        return v == new Vector2(1.5f, 2.5f);
    }

    private static bool TestUnityVector3()
    {
        var s = new UnityTypesStruct { vector3Field = new Vector3(1, 2, 3) };
        var acc = PathAccessor.Get<Vector3>(s, "vector3Field");
        return acc.Get(s) == new Vector3(1, 2, 3);
    }

    private static bool TestUnityVector4()
    {
        var s = new UnityTypesStruct { vector4Field = new Vector4(1, 2, 3, 4) };
        var acc = PathAccessor.Get<Vector4>(s, "vector4Field");
        return acc.Get(s) == new Vector4(1, 2, 3, 4);
    }

    private static bool TestUnityColor()
    {
        var s = new UnityTypesStruct { colorField = new Color(0.5f, 0.6f, 0.7f, 0.8f) };
        var acc = PathAccessor.Get<Color>(s, "colorField");
        return acc.Get(s) == new Color(0.5f, 0.6f, 0.7f, 0.8f);
    }

    private static bool TestUnityColor32()
    {
        var s = new UnityTypesStruct { color32Field = new Color32(100, 150, 200, 255) };
        var acc = PathAccessor.Get<Color32>(s, "color32Field");
        var c = acc.Get(s);
        return c.r == 100 && c.g == 150 && c.b == 200 && c.a == 255;
    }

    private static bool TestUnityQuaternion()
    {
        var s = new UnityTypesStruct { quaternionField = Quaternion.Euler(45, 90, 180) };
        var acc = PathAccessor.Get<Quaternion>(s, "quaternionField");
        return acc.Get(s) == Quaternion.Euler(45, 90, 180);
    }

    private static bool TestUnityRect()
    {
        var s = new UnityTypesStruct { rectField = new Rect(10, 20, 100, 200) };
        var acc = PathAccessor.Get<Rect>(s, "rectField");
        return acc.Get(s) == new Rect(10, 20, 100, 200);
    }

    private static bool TestUnityVector2Int()
    {
        var s = new UnityTypesStruct { vector2IntField = new Vector2Int(42, 84) };
        var acc = PathAccessor.Get<Vector2Int>(s, "vector2IntField");
        return acc.Get(s) == new Vector2Int(42, 84);
    }

    private static bool TestUnityVector3Int()
    {
        var s = new UnityTypesStruct { vector3IntField = new Vector3Int(1, 2, 3) };
        var acc = PathAccessor.Get<Vector3Int>(s, "vector3IntField");
        return acc.Get(s) == new Vector3Int(1, 2, 3);
    }

    #endregion

    #region Custom Struct Tests

    private static bool TestCustomStruct()
    {
        var s = new CustomStruct { a = 10, b = 20.5f, c = new Vector3(1, 2, 3) };
        var accA = PathAccessor.Get<int>(s, "a");
        var accB = PathAccessor.Get<float>(s, "b");
        var accC = PathAccessor.Get<Vector3>(s, "c");
        return accA.Get(s) == 10 &&
               Math.Abs(accB.Get(s) - 20.5f) < 0.001f &&
               accC.Get(s) == new Vector3(1, 2, 3);
    }

    #endregion

    #region Struct Chain Tests

    private static bool TestStructChainSingle()
    {
        var s = new MiddleStruct { inner = new InnerStruct { value = 123 } };
        var acc = PathAccessor.Get<int>(s, "inner.value");
        return acc.Get(s) == 123;
    }

    private static bool TestStructChainTwo()
    {
        var s = new OuterStruct
        {
            middle = new MiddleStruct
            {
                inner = new InnerStruct { value = 456, scale = 2.5f }
            }
        };
        var accValue = PathAccessor.Get<int>(s, "middle.inner.value");
        var accScale = PathAccessor.Get<float>(s, "middle.inner.scale");
        return accValue.Get(s) == 456 && Math.Abs(accScale.Get(s) - 2.5f) < 0.001f;
    }

    private static bool TestStructChainThree()
    {
        var s = new OuterStruct
        {
            middle = new MiddleStruct
            {
                inner = new InnerStruct { value = 789 },
                middleValue = 111
            },
            outerValue = 222
        };
        var accInner = PathAccessor.Get<int>(s, "middle.inner.value");
        var accMiddle = PathAccessor.Get<int>(s, "middle.middleValue");
        var accOuter = PathAccessor.Get<int>(s, "outerValue");
        return accInner.Get(s) == 789 && accMiddle.Get(s) == 111 && accOuter.Get(s) == 222;
    }

    #endregion

    #region Reference Type in Struct Tests

    private static bool TestStringInStruct()
    {
        var s = new StructWithRef { name = "TestString" };
        var acc = PathAccessor.GetRef(s, "name");
        return (string)acc.Get(s) == "TestString";
    }

    private static bool TestObjectInStruct()
    {
        var obj = new SimpleClass { publicInt = 999 };
        var s = new StructWithRef { data = obj };
        var acc = PathAccessor.GetRef(s, "data");
        var result = acc.Get(s) as SimpleClass;
        return result != null && result.publicInt == 999;
    }

    #endregion

    #region Class Field Tests

    private static bool TestClassPublicInt()
    {
        var c = new SimpleClass { publicInt = 54321 };
        var acc = PathAccessor.Get<int>(c, "publicInt");
        return acc.Get(c) == 54321;
    }

    private static bool TestClassPrivateInt()
    {
        var c = new SimpleClass();
        c.PrivateInt = 11111;
        var acc = PathAccessor.Get<int>(c, "privateInt");
        return acc.Get(c) == 11111;
    }

    private static bool TestClassFloat()
    {
        var c = new SimpleClass { floatValue = 99.99f };
        var acc = PathAccessor.Get<float>(c, "floatValue");
        return Math.Abs(acc.Get(c) - 99.99f) < 0.001f;
    }

    private static bool TestClassString()
    {
        var c = new SimpleClass { stringValue = "Hello World" };
        var acc = PathAccessor.GetRef(c, "stringValue");
        return (string)acc.Get(c) == "Hello World";
    }

    private static bool TestClassVector3()
    {
        var c = new SimpleClass { vectorValue = new Vector3(7, 8, 9) };
        var acc = PathAccessor.Get<Vector3>(c, "vectorValue");
        return acc.Get(c) == new Vector3(7, 8, 9);
    }

    private static bool TestClassReadonlyInt()
    {
        var c = new SimpleClass();
        var acc = PathAccessor.Get<int>(c, "readonlyInt");
        return acc.Get(c) == 42;
    }

    #endregion

    #region Nested Class Tests

    private static bool TestNestedClass()
    {
        var c = new OuterClass
        {
            nested = new SimpleClass { publicInt = 777 },
            outerValue = 888
        };
        var accNested = PathAccessor.Get<int>(c, "nested.publicInt");
        var accOuter = PathAccessor.Get<int>(c, "outerValue");
        return accNested.Get(c) == 777 && accOuter.Get(c) == 888;
    }

    private static bool TestDeepNestedPath()
    {
        var c = new Level1
        {
            level2 = new Level2
            {
                level3 = new Level3
                {
                    level4 = new Level4
                    {
                        deepValue = 12345,
                        deepStruct = new InnerStruct { value = 67890 }
                    }
                }
            }
        };
        var accDeep = PathAccessor.Get<int>(c, "level2.level3.level4.deepValue");
        var accStruct = PathAccessor.Get<int>(c, "level2.level3.level4.deepStruct.value");
        return accDeep.Get(c) == 12345 && accStruct.Get(c) == 67890;
    }

    #endregion

    #region Class with Struct Tests

    private static bool TestClassWithPrimitiveStruct()
    {
        var c = new ClassWithStruct
        {
            primitiveStruct = new PrimitiveStruct { intField = 111, floatField = 2.22f }
        };
        var accInt = PathAccessor.Get<int>(c, "primitiveStruct.intField");
        var accFloat = PathAccessor.Get<float>(c, "primitiveStruct.floatField");
        return accInt.Get(c) == 111 && Math.Abs(accFloat.Get(c) - 2.22f) < 0.001f;
    }

    private static bool TestClassWithUnityStruct()
    {
        var c = new ClassWithStruct
        {
            unityStruct = new UnityTypesStruct { vector3Field = new Vector3(4, 5, 6) }
        };
        var acc = PathAccessor.Get<Vector3>(c, "unityStruct.vector3Field");
        return acc.Get(c) == new Vector3(4, 5, 6);
    }

    private static bool TestClassWithNestedStruct()
    {
        var c = new ClassWithStruct
        {
            innerStruct = new InnerStruct { value = 333, scale = 4.44f }
        };
        var accValue = PathAccessor.Get<int>(c, "innerStruct.value");
        var accScale = PathAccessor.Get<float>(c, "innerStruct.scale");
        return accValue.Get(c) == 333 && Math.Abs(accScale.Get(c) - 4.44f) < 0.001f;
    }

    #endregion

    #region Property Tests

    private static bool TestPropertyReadWrite()
    {
        var c = new PropertyClass { ReadWriteProperty = 555 };
        var acc = PathAccessor.Get<int>(c, "ReadWriteProperty");
        return acc.Get(c) == 555;
    }

    private static bool TestPropertyReadOnly()
    {
        var c = new PropertyClass { ReadWriteProperty = 100 };
        var acc = PathAccessor.Get<int>(c, "ReadOnlyProperty");
        return acc.Get(c) == 200;
    }

    private static bool TestPropertyVector()
    {
        var c = new PropertyClass { VectorProperty = new Vector3(11, 22, 33) };
        var acc = PathAccessor.Get<Vector3>(c, "VectorProperty");
        return acc.Get(c) == new Vector3(11, 22, 33);
    }

    #endregion

    #region Array Tests

    private static bool TestArrayInt()
    {
        var c = new ArrayClass { intArray = new[] { 10, 20, 30, 40, 50 } };
        var acc0 = PathAccessor.Get<int>(c, "intArray[0]");
        var acc2 = PathAccessor.Get<int>(c, "intArray[2]");
        var acc4 = PathAccessor.Get<int>(c, "intArray[4]");
        return acc0.Get(c) == 10 && acc2.Get(c) == 30 && acc4.Get(c) == 50;
    }

    private static bool TestArrayFloat()
    {
        var c = new ArrayClass { floatArray = new[] { 1.1f, 2.2f, 3.3f } };
        var acc = PathAccessor.Get<float>(c, "floatArray[1]");
        return Math.Abs(acc.Get(c) - 2.2f) < 0.001f;
    }

    private static bool TestArrayVector()
    {
        var c = new ArrayClass
        {
            vectorArray = new[] { Vector3.zero, Vector3.one, new Vector3(5, 6, 7) }
        };
        var acc = PathAccessor.Get<Vector3>(c, "vectorArray[2]");
        return acc.Get(c) == new Vector3(5, 6, 7);
    }

    private static bool TestArrayClass()
    {
        var c = new ArrayClass
        {
            classArray = new[]
            {
                new SimpleClass { publicInt = 100 },
                new SimpleClass { publicInt = 200 },
                new SimpleClass { publicInt = 300 }
            }
        };
        var acc = PathAccessor.GetRef(c, "classArray[1]");
        var result = acc.Get(c) as SimpleClass;
        return result != null && result.publicInt == 200;
    }

    private static bool TestArrayNestedAccess()
    {
        var c = new ArrayClass
        {
            classArray = new[]
            {
                new SimpleClass { publicInt = 111, vectorValue = new Vector3(1, 2, 3) },
                new SimpleClass { publicInt = 222, vectorValue = new Vector3(4, 5, 6) }
            }
        };
        var accInt = PathAccessor.Get<int>(c, "classArray[1].publicInt");
        var accVec = PathAccessor.Get<Vector3>(c, "classArray[0].vectorValue");
        return accInt.Get(c) == 222 && accVec.Get(c) == new Vector3(1, 2, 3);
    }

    #endregion

    #region List Tests

    private static bool TestListInt()
    {
        var c = new ListClass { intList = new List<int> { 100, 200, 300 } };
        var acc = PathAccessor.Get<int>(c, "intList[1]");
        return acc.Get(c) == 200;
    }

    private static bool TestListVector()
    {
        var c = new ListClass
        {
            vectorList = new List<Vector3> { Vector3.zero, new Vector3(9, 8, 7) }
        };
        var acc = PathAccessor.Get<Vector3>(c, "vectorList[1]");
        return acc.Get(c) == new Vector3(9, 8, 7);
    }

    private static bool TestListClass()
    {
        var c = new ListClass
        {
            classList = new List<SimpleClass>
            {
                new SimpleClass { publicInt = 1000 },
                new SimpleClass { publicInt = 2000 }
            }
        };
        var acc = PathAccessor.GetRef(c, "classList[0]");
        var result = acc.Get(c) as SimpleClass;
        return result != null && result.publicInt == 1000;
    }

    #endregion

    #region Complex Path Tests

    private static bool TestComplexArrayField()
    {
        var c = new ArrayClass
        {
            classArray = new[]
            {
                new SimpleClass { floatValue = 1.23f, stringValue = "test" }
            }
        };
        var accFloat = PathAccessor.Get<float>(c, "classArray[0].floatValue");
        var accStr = PathAccessor.GetRef(c, "classArray[0].stringValue");
        return Math.Abs(accFloat.Get(c) - 1.23f) < 0.001f && (string)accStr.Get(c) == "test";
    }

    private static bool TestComplexListFieldStruct()
    {
        var c = new ListClass
        {
            classList = new List<SimpleClass>
            {
                new SimpleClass { vectorValue = new Vector3(100, 200, 300) }
            }
        };
        var acc = PathAccessor.Get<Vector3>(c, "classList[0].vectorValue");
        return acc.Get(c) == new Vector3(100, 200, 300);
    }

    private static bool TestComplexDeepStructChain()
    {
        var c = new Level1
        {
            level2 = new Level2
            {
                level3 = new Level3
                {
                    level4 = new Level4
                    {
                        deepStruct = new InnerStruct { value = 99999, scale = 88.88f }
                    }
                }
            }
        };
        var accVal = PathAccessor.Get<int>(c, "level2.level3.level4.deepStruct.value");
        var accScale = PathAccessor.Get<float>(c, "level2.level3.level4.deepStruct.scale");
        return accVal.Get(c) == 99999 && Math.Abs(accScale.Get(c) - 88.88f) < 0.01f;
    }

    #endregion

    #region FaceInfo Tests (Real-world)

    private static bool TestFaceInfoFaceIndex()
    {
        var faceInfo = new FaceInfo();
        var acc = PathAccessor.Get<int>(faceInfo, "m_FaceIndex");
        return acc.Get(faceInfo) == 0;
    }

    private static bool TestFaceInfoPointSize()
    {
        var faceInfo = new FaceInfo();
        var acc = PathAccessor.Get<int>(faceInfo, "m_PointSize");
        return acc.Get(faceInfo) == 0;
    }

    private static bool TestFaceInfoLineHeight()
    {
        var faceInfo = new FaceInfo();
        var acc = PathAccessor.Get<float>(faceInfo, "m_LineHeight");
        return !float.IsNaN(acc.Get(faceInfo));
    }

    #endregion

    #region GlyphRect Tests

    private static bool TestGlyphRect()
    {
        var rect = new GlyphRect(10, 20, 100, 200);
        var accX = PathAccessor.Get<int>(rect, "m_X");
        var accY = PathAccessor.Get<int>(rect, "m_Y");
        var accW = PathAccessor.Get<int>(rect, "m_Width");
        var accH = PathAccessor.Get<int>(rect, "m_Height");
        return accX.Get(rect) == 10 &&
               accY.Get(rect) == 20 &&
               accW.Get(rect) == 100 &&
               accH.Get(rect) == 200;
    }

    #endregion

    #region GlyphMetrics Tests

    private static bool TestGlyphMetrics()
    {
        var metrics = new GlyphMetrics(50, 60, 5, 55, 70);
        var accW = PathAccessor.Get<float>(metrics, "m_Width");
        var accH = PathAccessor.Get<float>(metrics, "m_Height");
        var accBearingX = PathAccessor.Get<float>(metrics, "m_HorizontalBearingX");
        var accBearingY = PathAccessor.Get<float>(metrics, "m_HorizontalBearingY");
        var accAdvance = PathAccessor.Get<float>(metrics, "m_HorizontalAdvance");
        return Math.Abs(accW.Get(metrics) - 50) < 0.001f &&
               Math.Abs(accH.Get(metrics) - 60) < 0.001f &&
               Math.Abs(accBearingX.Get(metrics) - 5) < 0.001f &&
               Math.Abs(accBearingY.Get(metrics) - 55) < 0.001f &&
               Math.Abs(accAdvance.Get(metrics) - 70) < 0.001f;
    }

    #endregion

    #region Cache Tests

    private static bool TestCacheReuse()
    {
        var s1 = new PrimitiveStruct { intField = 100 };
        var s2 = new PrimitiveStruct { intField = 200 };

        var acc1 = PathAccessor.Get<int>(s1, "intField");
        var acc2 = PathAccessor.Get<int>(s2, "intField");

        var sameInstance = ReferenceEquals(acc1, acc2);

        return sameInstance && acc1.Get(s1) == 100 && acc2.Get(s2) == 200;
    }

    #endregion

    #region Write Tests

    private static bool TestWritePrimitiveStruct()
    {
        var s = new PrimitiveStruct { intField = 100 };
        var acc = PathAccessor.Get<int>(s, "intField");

        object boxed = s;
        acc.Set(boxed, 999);

        var result = acc.Get(boxed);
        return result == 999;
    }

    private static bool TestWriteUnityStruct()
    {
        var s = new UnityTypesStruct { vector3Field = Vector3.zero };
        object boxed = s;
        var acc = PathAccessor.Get<Vector3>(boxed, "vector3Field");
        acc.Set(boxed, new Vector3(111, 222, 333));
        return acc.Get(boxed) == new Vector3(111, 222, 333);
    }

    private static bool TestWriteClassField()
    {
        var c = new SimpleClass { publicInt = 0 };
        var acc = PathAccessor.Get<int>(c, "publicInt");
        acc.Set(c, 54321);
        return c.publicInt == 54321;
    }

    private static bool TestWriteProperty()
    {
        var c = new PropertyClass { ReadWriteProperty = 0 };
        var acc = PathAccessor.Get<int>(c, "ReadWriteProperty");
        acc.Set(c, 777);
        return c.ReadWriteProperty == 777;
    }

    private static bool TestWriteArrayElement()
    {
        var c = new ArrayClass { intArray = new[] { 1, 2, 3 } };
        var acc = PathAccessor.Get<int>(c, "intArray[1]");
        acc.Set(c, 999);
        return c.intArray[1] == 999;
    }

    #endregion

    #region CachedAccessor Tests

    private static bool TestCachedGetSimple()
    {
        var c = new SimpleClass { publicInt = 42 };
        var cached = PathAccessor.GetCached<int>(c, "publicInt");
        cached.Bind(c);
        return cached.Get() == 42;
    }

    private static bool TestCachedSetSimple()
    {
        var c = new SimpleClass { publicInt = 42 };
        var cached = PathAccessor.GetCached<int>(c, "publicInt");
        cached.Bind(c);
        cached.Set(999);
        return c.publicInt == 999;
    }

    private static bool TestCachedGetRef()
    {
        var c = new SimpleClass { publicInt = 42 };
        var cached = PathAccessor.GetCached<int>(c, "publicInt");
        cached.Bind(c);
        ref var val = ref cached.GetRef();
        val = 123;
        return c.publicInt == 123;
    }

    private static bool TestCachedNestedPath()
    {
        var c = new ClassWithStruct { innerStruct = new InnerStruct { value = 77 } };
        var cached = PathAccessor.GetCached<int>(c, "innerStruct.value");
        cached.Bind(c);

        if (cached.Get() != 77) return false;

        cached.Set(888);
        return c.innerStruct.value == 888;
    }

    private static bool TestCachedRebind()
    {
        var c1 = new SimpleClass { publicInt = 100 };
        var c2 = new SimpleClass { publicInt = 200 };

        var cached = PathAccessor.GetCached<int>(typeof(SimpleClass), "publicInt");

        cached.Bind(c1);
        if (cached.Get() != 100) return false;

        cached.Bind(c2);
        if (cached.Get() != 200) return false;

        cached.Set(300);
        return c2.publicInt == 300 && c1.publicInt == 100;
    }

    #endregion

    #region Null Handling Tests

    private static bool TestNullStringRead()
    {
        var s = new StructWithRef { name = null };
        var acc = PathAccessor.GetRef(s, "name");
        return acc.Get(s) == null;
    }

    private static bool TestNullObjectRead()
    {
        var s = new StructWithRef { data = null };
        var acc = PathAccessor.GetRef(s, "data");
        return acc.Get(s) == null;
    }

    #endregion

    #region Performance Tests

    private static bool TestPerformanceStructRead()
    {
        var s = new PrimitiveStruct { intField = 42 };
        var acc = PathAccessor.Get<int>(s, "intField");

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10000; i++)
        {
            var _ = acc.Get(s);
        }
        sw.Stop();

        Debug.Log($"    10000 boxed struct reads (reflection): {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks / 10000.0:F2} ticks/read)");
        return sw.ElapsedMilliseconds < 1000;
    }

    private static bool TestPerformanceClassRead()
    {
        var c = new SimpleClass { publicInt = 42 };
        var acc = PathAccessor.Get<int>(c, "publicInt");

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10000; i++)
        {
            var _ = acc.Get(c);
        }
        sw.Stop();

        Debug.Log($"    10000 class reads (UnsafeUtility): {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks / 10000.0:F2} ticks/read)");
        return sw.ElapsedMilliseconds < 1000;
    }

    private static bool TestPerformanceClassNestedStruct()
    {
        var c = new ClassWithStruct
        {
            innerStruct = new InnerStruct { value = 42 }
        };
        var acc = PathAccessor.Get<int>(c, "innerStruct.value");

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 10000; i++)
        {
            var _ = acc.Get(c);
        }
        sw.Stop();

        Debug.Log($"    10000 class->struct reads (UnsafeUtility): {sw.ElapsedMilliseconds}ms ({sw.ElapsedTicks / 10000.0:F2} ticks/read)");
        return sw.ElapsedMilliseconds < 1000;
    }

    private static bool TestPerformanceVsDirectAccess()
    {
        var c = new SimpleClass { publicInt = 42 };
        var acc = PathAccessor.Get<int>(c, "publicInt");

        const int iterations = 10000000;

        var swPath = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = acc.Get(c);
        }
        swPath.Stop();

        var swDirect = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = c.publicInt;
        }
        swDirect.Stop();

        var pathTicks = swPath.ElapsedTicks;
        var directTicks = swDirect.ElapsedTicks;
        var ratio = directTicks > 0 ? (double)pathTicks / directTicks : 0;

        Debug.Log($"    PathAccessor: {pathTicks} ticks, Direct: {directTicks} ticks, Ratio: {ratio:F1}x slower");

        return true;
    }

    private static bool TestPerformanceVsReflection()
    {
        var c = new SimpleClass { publicInt = 42 };
        var acc = PathAccessor.Get<int>(c, "publicInt");
        var field = typeof(SimpleClass).GetField("publicInt");

        const int iterations = 10000;

        var swPath = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = acc.Get(c);
        }
        swPath.Stop();

        var swRefl = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = (int)field.GetValue(c);
        }
        swRefl.Stop();

        var pathMs = swPath.ElapsedMilliseconds;
        var reflMs = swRefl.ElapsedMilliseconds;
        var pathTicks = swPath.ElapsedTicks;
        var reflTicks = swRefl.ElapsedTicks;
        var ratio = reflTicks > 0 ? (double)pathTicks / reflTicks : 0;

        Debug.Log($"    PathAccessor: {pathTicks} ticks, Reflection: {reflTicks} ticks, Ratio: {ratio:F2}x");

        return pathTicks <= reflTicks * 2;
    }

    private static bool TestPerformanceDirectAccessor()
    {
        var c = new SimpleClass { publicInt = 42 };
        var acc = PathAccessor.GetDirect<int>(c, "publicInt");

        const int iterations = 10000000;

        var swDirect = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = acc.Get(c);
        }
        swDirect.Stop();

        var swNative = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = c.publicInt;
        }
        swNative.Stop();

        var directTicks = swDirect.ElapsedTicks;
        var nativeTicks = swNative.ElapsedTicks;
        var ratio = nativeTicks > 0 ? (double)directTicks / nativeTicks : 0;

        Debug.Log($"    DirectFieldAccessor: {directTicks} ticks, Native: {nativeTicks} ticks, Ratio: {ratio:F1}x");

        return true;
    }

    private static bool TestPerformanceDirectAccessorNestedStruct()
    {
        var c = new ClassWithStruct { innerStruct = new InnerStruct { value = 42 } };
        var acc = PathAccessor.GetDirect<int>(c, "innerStruct.value");

        const int iterations = 10000000;

        var swDirect = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = acc.Get(c);
        }
        swDirect.Stop();

        var swNative = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var _ = c.innerStruct.value;
        }
        swNative.Stop();

        var directTicks = swDirect.ElapsedTicks;
        var nativeTicks = swNative.ElapsedTicks;
        var ratio = nativeTicks > 0 ? (double)directTicks / nativeTicks : 0;

        Debug.Log($"    DirectFieldAccessor (nested): {directTicks} ticks, Native: {nativeTicks} ticks, Ratio: {ratio:F1}x");

        return true;
    }

    private static bool TestPerformanceAllMethods()
    {
        var c = new SimpleClass { publicInt = 42 };
        var typedAcc = PathAccessor.Get<int>(c, "publicInt");
        var directAcc = PathAccessor.GetDirect<int>(c, "publicInt");
        var cachedAcc = directAcc.CreateCached();
        cachedAcc.Bind(c);
        var field = typeof(SimpleClass).GetField("publicInt");

        const int iterations = 10000000;
        var sum = 0L;

        var swNative = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) { sum += c.publicInt; }
        swNative.Stop();

        var swCached = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) { sum += cachedAcc.Get(); }
        swCached.Stop();

        var swDirectAcc = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) { sum += directAcc.Get(c); }
        swDirectAcc.Stop();

        var swTyped = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) { sum += typedAcc.Get(c); }
        swTyped.Stop();

        var swRefl = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++) { sum += (int)field.GetValue(c); }
        swRefl.Stop();

        var native = swNative.ElapsedTicks;
        var cached = swCached.ElapsedTicks;
        var direct = swDirectAcc.ElapsedTicks;
        var typed = swTyped.ElapsedTicks;
        var refl = swRefl.ElapsedTicks;

        Debug.Log($"    Native: {native}, CachedAccessor: {cached} ({(double)cached/native:F1}x), DirectAccessor: {direct} ({(double)direct/native:F1}x), TypedAccessor: {typed} ({(double)typed/native:F1}x), Reflection: {refl} ({(double)refl/native:F1}x) [sum={sum}]");

        return true;
    }
    
    public static void TestNativeVsCached()
    {
        Debug.Log("========== Native vs CachedAccessor Test ==========");

        var c = new SimpleClass { publicInt = 12345 };
        var cachedAcc = PathAccessor.GetCached<int>(c, "publicInt");
        cachedAcc.Bind(c);

        const int iterations = 10000000;

        var swNative = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            c.publicInt ^= i;
        }
        swNative.Stop();
        var nativeResult = c.publicInt;

        c.publicInt = 12345;

        var swCached = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            cachedAcc.Set(cachedAcc.Get() ^ i);
        }
        swCached.Stop();
        var cachedResult = cachedAcc.Get();

        var native = swNative.ElapsedTicks;
        var cached = swCached.ElapsedTicks;
        var ratio = native > 0 ? (double)cached / native : 0;

        Debug.Log($"    Native: {native} ticks [result={nativeResult}]");
        Debug.Log($"    CachedAccessor: {cached} ticks [result={cachedResult}]");
        Debug.Log($"    Ratio: {ratio:F2}x");
        Debug.Log("====================================================");
    }

    #endregion

    #region Thread Safety Tests

    private static bool TestConcurrentReads()
    {
        var s = new PrimitiveStruct { intField = 12345 };
        var acc = PathAccessor.Get<int>(s, "intField");
        var errors = 0;
        var iterations = 1000;

        Parallel.For(0, iterations, _ =>
        {
            try
            {
                var val = acc.Get(s);
                if (val != 12345)
                    Interlocked.Increment(ref errors);
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        });

        Debug.Log($"    Concurrent reads: {iterations} iterations, {errors} errors");
        return errors == 0;
    }

    private static bool TestConcurrentDifferentKeys()
    {
        var structs = new PrimitiveStruct[100];
        for (var i = 0; i < structs.Length; i++)
            structs[i] = new PrimitiveStruct { intField = i };

        var errors = 0;
        var iterations = 1000;

        Parallel.For(0, iterations, i =>
        {
            try
            {
                var idx = i % structs.Length;
                var acc = PathAccessor.Get<int>(structs[idx], "intField");
                var val = acc.Get(structs[idx]);
                if (val != idx)
                    Interlocked.Increment(ref errors);
            }
            catch
            {
                Interlocked.Increment(ref errors);
            }
        });

        Debug.Log($"    Concurrent different keys: {iterations} iterations, {errors} errors");
        return errors == 0;
    }

    #endregion
}
