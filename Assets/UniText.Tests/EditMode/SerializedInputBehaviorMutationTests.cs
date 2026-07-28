using System;
using LightSide;
using NUnit.Framework;
using UnityEngine;

namespace LightSide.Tests
{
    /// <summary>Contract coverage for generated input-behavior state propagation and nested authored roots.</summary>
    public sealed class SerializedInputBehaviorMutationTests
    {
        private InputBehaviorPreset preset;

        [TearDown]
        public void TearDown()
        {
            if (preset != null) UnityEngine.Object.DestroyImmediate(preset);
        }

        [Test]
        public void PresetReceivesNestedBehaviorChangesAndDetachesRemovedBehavior()
        {
            preset = ScriptableObject.CreateInstance<InputBehaviorPreset>();
            var filter = new IntegerFilter();
            var changes = 0;
            preset.Changed += () => changes++;
            preset.Behaviors.Add(filter);
            changes = 0;

            filter.AllowNegative = true;

            Assert.That(changes, Is.EqualTo(1));

            preset.Behaviors.Remove(filter);
            changes = 0;
            filter.AllowNegative = false;

            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void MutableFormatSourceRebindsWithoutRetainingPreviousTarget()
        {
            var behavior = new TextFormattingBehavior();
            var sink = new CountingBehaviorSink();
            behavior.SetChangeSink(sink);
            var first = new MutableFormatStyleSource();
            var second = new MutableFormatStyleSource();
            var command = new TextFormattingBehavior.FormatCommand { Target = first };
            command.SetOwner(behavior);
            sink.Reset();

            first.Publish();

            Assert.That(sink.Count, Is.EqualTo(1));

            command.Target = second;
            sink.Reset();
            first.Publish();
            Assert.That(sink.Count, Is.Zero);

            second.Publish();
            Assert.That(sink.Count, Is.EqualTo(1));

            command.SetOwner(null);
            sink.Reset();
            second.Publish();
            Assert.That(sink.Count, Is.Zero);
        }

        [Test]
        public void ChromeRuleRebindsMutableSelectorAndDetachesWhenUnowned()
        {
            var changes = 0;
            var first = new MutableMarkupSelector();
            var second = new MutableMarkupSelector();
            var rule = new ChromeRule { Selector = first };
            rule.SetChangeCallback(() => changes++);

            first.Publish();

            Assert.That(changes, Is.EqualTo(1));

            rule.Selector = second;
            changes = 0;
            first.Publish();
            Assert.That(changes, Is.Zero);

            second.Publish();
            Assert.That(changes, Is.EqualTo(1));

            rule.SetChangeCallback(null);
            changes = 0;
            second.Publish();
            Assert.That(changes, Is.Zero);
        }

        [Test]
        public void ReferencedStyleTracksOnlyTheCurrentNestedModifier()
        {
            var first = new BoldModifier();
            var second = new BoldModifier();
            var source = new ReferencedStyle { Modifier = first };
            var changes = 0;
            source.Changed += () => changes++;

            first.Weight = 600;
            Assert.That(changes, Is.EqualTo(1));

            source.Modifier = second;
            changes = 0;
            first.Weight = 700;
            Assert.That(changes, Is.Zero);

            second.Weight = 800;
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void NativeConfigurationChangeUsesTheOwningBehaviorMutationPath()
        {
            var behavior = new NativeKeyboardBehavior();
            var sink = new CountingBehaviorSink();
            behavior.SetChangeSink(sink);
            var previous = behavior.Keyboard;
            sink.Reset();

            previous.KeyboardType = KeyboardType.URL;

            Assert.That(sink.Count, Is.EqualTo(1));

            behavior.Keyboard = new NativeKeyboardConfig();
            sink.Reset();
            previous.KeyboardType = KeyboardType.EmailAddress;
            Assert.That(sink.Count, Is.Zero);

            behavior.Keyboard.KeyboardType = KeyboardType.PhonePad;
            Assert.That(sink.Count, Is.EqualTo(1));
        }

        private sealed class CountingBehaviorSink : IInputBehaviorChangeSink
        {
            internal int Count { get; private set; }

            public void MarkInputBehaviorChanged(InputBehavior behavior) => Count++;

            internal void Reset() => Count = 0;
        }

        private sealed class MutableFormatStyleSource : IFormatStyleSource
        {
            public event Action Changed;

            public bool Toggle(UniTextEditable editable) => false;

            internal void Publish() => Changed?.Invoke();
        }

        private sealed class MutableMarkupSelector : IMarkupSelector
        {
            public event Action Changed;

            public int Specificity => 0;

            public bool Matches(ParseRule rule, BaseModifier modifier) => true;

            internal void Publish() => Changed?.Invoke();
        }
    }
}
