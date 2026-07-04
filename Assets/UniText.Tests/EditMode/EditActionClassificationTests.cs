using System;
using System.Collections.Generic;
using System.Linq;
using LightSide;
using NUnit.Framework;

namespace LightSide.Tests
{
    /// <summary>
    /// Exhaustive guard for <see cref="UniTextEditable.IsNonMutatingAction"/>: every
    /// <see cref="EditAction"/> member must be explicitly listed here, so inserting a member
    /// mid-enum (which silently shifts the FirstMove/LastMove range classification) fails loudly.
    /// </summary>
    public class EditActionClassificationTests
    {
        private static readonly HashSet<EditAction> nonMutating = new()
        {
            EditAction.MoveLeft, EditAction.MoveRight, EditAction.MoveUp, EditAction.MoveDown,
            EditAction.MovePageUp, EditAction.MovePageDown, EditAction.MoveWordLeft, EditAction.MoveWordRight,
            EditAction.MoveLineStart, EditAction.MoveLineEnd, EditAction.MoveDocStart, EditAction.MoveDocEnd,
            EditAction.SelectLeft, EditAction.SelectRight, EditAction.SelectUp, EditAction.SelectDown,
            EditAction.SelectPageUp, EditAction.SelectPageDown, EditAction.SelectWordLeft, EditAction.SelectWordRight,
            EditAction.SelectLineStart, EditAction.SelectLineEnd, EditAction.SelectDocStart, EditAction.SelectDocEnd,
            EditAction.SelectAll,
            EditAction.Copy, EditAction.Submit, EditAction.Cancel,
        };

        private static readonly HashSet<EditAction> mutatingOrInert = new()
        {
            EditAction.None, EditAction.Ignore,
            EditAction.InsertNewline, EditAction.InsertTab,
            EditAction.DeletePrev, EditAction.DeleteNext,
            EditAction.DeleteWordPrev, EditAction.DeleteWordNext,
            EditAction.DeleteLineStart, EditAction.DeleteLineEnd,
            EditAction.TransposeChars,
            EditAction.Cut, EditAction.Paste, EditAction.PasteAsPlain,
            EditAction.Undo, EditAction.Redo,
        };

        private static IEnumerable<EditAction> DistinctMembers()
            => Enum.GetValues(typeof(EditAction)).Cast<EditAction>().Distinct();

        [Test]
        public void EveryMemberIsExplicitlyClassified()
        {
            foreach (var action in DistinctMembers())
            {
                var listed = nonMutating.Contains(action) || mutatingOrInert.Contains(action);
                Assert.IsTrue(listed,
                    $"EditAction.{action} is not in this test's expected sets — a new member was added; " +
                    "classify it here AND verify FirstMove/LastMove/FirstSelect/LastSelect still bracket it correctly.");
            }
        }

        [Test]
        public void ClassificationMatchesExpectedSets()
        {
            foreach (var action in DistinctMembers())
            {
                var expected = nonMutating.Contains(action);
                Assert.AreEqual(expected, UniTextEditable.IsNonMutatingAction(action),
                    $"EditAction.{action}: IsNonMutatingAction disagrees with the expected classification — " +
                    "a mid-enum insertion likely shifted the First*/Last* range constants.");
            }
        }

        [Test]
        public void RangeConstantsBracketTheirGroups()
        {
            Assert.AreEqual(EditAction.MoveLeft, EditAction.FirstMove);
            Assert.AreEqual(EditAction.MoveDocEnd, EditAction.LastMove);
            Assert.AreEqual(EditAction.SelectLeft, EditAction.FirstSelect);
            Assert.AreEqual(EditAction.SelectAll, EditAction.LastSelect);
        }
    }
}
