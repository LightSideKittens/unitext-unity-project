using System;
using System.Collections;
using System.Collections.Generic;
using LightSide;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace LightSide.Tests
{
    /// <summary>Contract coverage for atomic graph ownership and authored modifier identity.</summary>
    public sealed class OwnershipTransactionTests
    {
        private readonly List<UnityEngine.Object> ownedObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (var i = ownedObjects.Count - 1; i >= 0; i--)
                if (ownedObjects[i] != null)
                {
                    Undo.ClearUndo(ownedObjects[i]);
                    UnityEngine.Object.DestroyImmediate(ownedObjects[i]);
                }
            ownedObjects.Clear();
        }

        [Test]
        public void StyleReplacementRejectsDuplicateAndForeignEntriesAtomically()
        {
            var target = CreateAsset<StylePreset>();
            var foreignOwner = CreateAsset<StylePreset>();
            var retained = Style.WholeText(new BoldModifier());
            var candidate = Style.WholeText(new ItalicModifier());
            var foreign = Style.WholeText(new BoldModifier());
            target.Styles.Add(retained);
            foreignOwner.Styles.Add(foreign);

            Assert.Throws<ArgumentException>(() =>
                target.Styles.ReplaceAll(new[] { candidate, candidate }));
            AssertRetained(target.Styles, retained);

            Assert.Throws<InvalidOperationException>(() =>
                target.Styles.ReplaceAll(new[] { candidate, foreign }));
            AssertRetained(target.Styles, retained);
        }

        [Test]
        public void BehaviorReplacementRejectsDuplicateAndForeignEntriesAtomically()
        {
            var target = CreateAsset<InputBehaviorPreset>();
            var foreignOwner = CreateAsset<InputBehaviorPreset>();
            var retained = new IntegerFilter();
            var candidate = new IntegerFilter();
            var foreign = new IntegerFilter();
            target.Behaviors.Add(retained);
            foreignOwner.Behaviors.Add(foreign);

            Assert.Throws<ArgumentException>(() =>
                target.Behaviors.ReplaceAll(new[] { candidate, candidate }));
            AssertRetained(target.Behaviors, retained);

            Assert.Throws<InvalidOperationException>(() =>
                target.Behaviors.ReplaceAll(new[] { candidate, foreign }));
            AssertRetained(target.Behaviors, retained);
        }

        [Test]
        public void ComponentOwnedStyleCannotEnterPresetCollection()
        {
            var gameObject = new GameObject("Owned Style");
            ownedObjects.Add(gameObject);
            gameObject.SetActive(false);
            var text = gameObject.AddComponent<UniText>();
            var style = Style.WholeText(new BoldModifier());
            var preset = CreateAsset<StylePreset>();

            text.Styles.Add(style);
            Assert.That(text.EnsureAttributeParserCreated(), Is.True);
            Assert.That(style.Modifier.Owner, Is.SameAs(text));

            Assert.Throws<InvalidOperationException>(() => preset.Styles.Add(style));
            Assert.That(preset.Styles, Is.Empty);
        }

        [UnityTest]
        public IEnumerator SerializedStyleEditsReconcileThroughOwnershipLifecycle()
        {
            var gameObject = new GameObject("Serialized Styles");
            ownedObjects.Add(gameObject);
            gameObject.SetActive(false);
            var text = gameObject.AddComponent<UniText>();
            text.Styles.Add(Style.Tag(new BoldModifier(), "first"));
            text.Styles.Add(Style.Tag(new ItalicModifier(), "second"));
            Assert.That(text.EnsureAttributeParserCreated(), Is.True);
            StateEditorReceptor.Capture((IEditorSerializedStateOwner)text);
            yield return null;

            MutateSerializedStyles(text, styles => styles.MoveArrayElement(0, 1));
            yield return null;
            text.EnsureAttributeParserCreated();
            Assert.That(text.Styles[0].Modifier.Owner, Is.SameAs(text));
            Assert.That(text.AttributeParser.GetModifierRegistrationOrder(
                text.Styles[0].Modifier), Is.GreaterThanOrEqualTo(0));
            MutateSerializedStyles(text, styles =>
                styles.GetArrayElementAtIndex(0).FindPropertyRelative("modifier")
                    .managedReferenceValue = new BoldModifier());
            yield return null;
            MutateSerializedStyles(text, styles =>
                styles.GetArrayElementAtIndex(0).FindPropertyRelative("source")
                    .managedReferenceValue = new TagRule("replacement"));
            yield return null;
            MutateSerializedStyles(text, styles => styles.DeleteArrayElementAtIndex(1), true);
            yield return null;

            Assert.That(text.Styles.Count, Is.EqualTo(1));
            Assert.That(text.Styles[0].Modifier, Is.TypeOf<BoldModifier>());
            Assert.That(text.Styles[0].Source, Is.TypeOf<TagRule>());

            Undo.PerformUndo();
            Assert.That(text.EnsureAttributeParserCreated(), Is.True);
            yield return null;
            Assert.That(text.Styles.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator SerializedPresetStyleUndoReconcilesRuntimeCopies()
        {
            var preset = CreateAsset<StylePreset>();
            preset.Styles.Add(Style.Tag(new BoldModifier(), "preset"));
            preset.Styles.Add(Style.Tag(new ArcModifier(), "arc"));
            var gameObject = new GameObject("Preset Consumer");
            ownedObjects.Add(gameObject);
            gameObject.SetActive(false);
            var text = gameObject.AddComponent<UniText>();
            text.StylePresets.Add(preset);
            Assert.That(text.EnsureAttributeParserCreated(), Is.True);
            gameObject.SetActive(true);
            StateEditorReceptor.Capture((IEditorSerializedStateOwner)preset);
            StateEditorReceptor.Capture((IEditorSerializedStateOwner)text);
            yield return null;
            Assert.That(text.TryGetStyle<BoldModifier>(out var runtimeStyle), Is.True);
            Assert.That(text.TryGetStyle<ArcModifier>(out var runtimeArcStyle), Is.True);
            Assert.That(text.AttributeParser.GetModifierRegistrationOrder(runtimeStyle.Modifier),
                Is.GreaterThanOrEqualTo(0));

            SetSerializedStyleEnabled(preset, 0, false);
            Assert.That(preset.Styles[0].Enabled, Is.False);
            yield return null;
            text.EnsureAttributeParserCreated();
            Assert.That(text.TryGetStyle<BoldModifier>(out _), Is.False);
            Assert.That(text.AttributeParser.GetModifierRegistrationOrder(runtimeStyle.Modifier),
                Is.EqualTo(-1));
            Assert.That(runtimeStyle.Modifier.Owner, Is.Null);

            SetSerializedStyleEnabled(preset, 0, true);
            Assert.That(preset.Styles[0].Enabled, Is.True);
            yield return null;
            text.EnsureAttributeParserCreated();
            Assert.That(text.TryGetStyle<BoldModifier>(out var restoredStyle), Is.True);
            Assert.That(restoredStyle, Is.SameAs(runtimeStyle));
            Assert.That(text.AttributeParser.GetModifierRegistrationOrder(restoredStyle.Modifier),
                Is.GreaterThanOrEqualTo(0));

            MutateSerializedStyles(preset, styles => styles.MoveArrayElement(0, 1));
            yield return null;
            text.EnsureAttributeParserCreated();
            Assert.That(text.TryGetStyle<ArcModifier>(out var movedStyle), Is.True);
            Assert.That(movedStyle, Is.SameAs(runtimeArcStyle));
            Assert.That(movedStyle.Modifier.Owner, Is.SameAs(text));
            Assert.That(text.AttributeParser.GetModifierRegistrationOrder(movedStyle.Modifier),
                Is.GreaterThanOrEqualTo(0));

            MutateSerializedStyles(preset,
                styles => styles.DeleteArrayElementAtIndex(0), true);
            yield return null;
            Assert.That(preset.Styles.Count, Is.EqualTo(1));
            Assert.That(text.TryGetStyle<BoldModifier>(out var retainedStyle), Is.True);
            Assert.That(retainedStyle, Is.SameAs(runtimeStyle));

            Undo.PerformUndo();
            Assert.That(text.EnsureAttributeParserCreated(), Is.True);
            yield return null;
            Assert.That(preset.Styles.Count, Is.EqualTo(2));
        }

        [Test]
        public void CompositeModifierRejectsDuplicateChildBeforeMutation()
        {
            var composite = new CompositeModifier();
            var child = new BoldModifier();
            composite.Modifiers.Add(child);

            Assert.Throws<InvalidOperationException>(() => composite.Modifiers.Add(child));
            AssertRetained(composite.Modifiers, child);
        }

        [Test]
        public void CompositeParseRuleRejectsDuplicateChildBeforeMutation()
        {
            var composite = new CompositeParseRule();
            var child = new TriggerWordParseRule("@");
            composite.Rules.Add(child);

            Assert.Throws<InvalidOperationException>(() => composite.Rules.Add(child));
            AssertRetained(composite.Rules, child);
        }

        [Test]
        public void ModifierGraphSignatureUsesAuthoredRootBeforeEnable()
        {
            var firstPreset = CreateAsset<ModifierGraphPreset>();
            var matchingPreset = CreateAsset<ModifierGraphPreset>();
            var differentPreset = CreateAsset<ModifierGraphPreset>();
            firstPreset.Root = Composite(new BoldModifier(), new ItalicModifier());
            matchingPreset.Root = Composite(new BoldModifier(), new ItalicModifier());
            differentPreset.Root = Composite(new ItalicModifier(), new BoldModifier());

            var first = new ModifierGraphModifier { Preset = firstPreset };
            var matching = new ModifierGraphModifier { Preset = matchingPreset };
            var different = new ModifierGraphModifier { Preset = differentPreset };

            Assert.That(first.IsInitialized, Is.False);
            Assert.That(matching.IsInitialized, Is.False);
            Assert.That(first.SignatureMatches(matching), Is.True);
            Assert.That(first.SignatureMatches(different), Is.False);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            ownedObjects.Add(asset);
            return asset;
        }

        private static CompositeModifier Composite(params BaseModifier[] children)
        {
            var composite = new CompositeModifier();
            composite.Modifiers.ReplaceAll(children);
            return composite;
        }

        private static void MutateSerializedStyles(UnityEngine.Object target,
            Action<SerializedProperty> mutate,
            bool recordUndo = false)
        {
            using var serialized = new SerializedObject(target);
            mutate(serialized.FindProperty("styles").FindPropertyRelative("items"));
            if (recordUndo) serialized.ApplyModifiedProperties();
            else serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedStyleEnabled(UnityEngine.Object target, int index,
            bool enabled)
        {
            using var serialized = new SerializedObject(target);
            var disabled = serialized.FindProperty("styles").FindPropertyRelative("items")
                .GetArrayElementAtIndex(index).FindPropertyRelative("disabled");
            new SerializedPropertyBinding(disabled).SetValue(!enabled,
                enabled ? "Enable Style" : "Disable Style");
        }

        private static void AssertRetained<T>(IReadOnlyList<T> collection, T retained)
            where T : class
        {
            Assert.That(collection.Count, Is.EqualTo(1));
            Assert.That(collection[0], Is.SameAs(retained));
        }
    }
}
