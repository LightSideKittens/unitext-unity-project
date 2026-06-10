using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class ValidatorTests : LiveEditableTest
    {
        [UnityTest]
        public IEnumerator IntegerValidator_RejectsNonDigitInput()
        {
            yield return BuildField();
            editable.AddBehavior(new IntegerValidator());
            editable.InsertText("12a3");
            Assert.AreEqual(string.Empty, editable.Text);
        }

        [UnityTest]
        public IEnumerator IntegerValidator_AcceptsDigits()
        {
            yield return BuildField();
            editable.AddBehavior(new IntegerValidator());
            editable.InsertText("42");
            Assert.AreEqual("42", editable.Text);
        }

        [UnityTest]
        public IEnumerator DecimalValidator_AcceptsDecimalNumber()
        {
            yield return BuildField();
            editable.AddBehavior(new DecimalValidator());
            editable.InsertText("3.14");
            Assert.AreEqual("3.14", editable.Text);
        }

        [UnityTest]
        public IEnumerator EmailValidator_RejectsSpace()
        {
            yield return BuildField();
            editable.AddBehavior(new EmailValidator());
            editable.InsertText("abc");
            Assert.AreEqual("abc", editable.Text);
            editable.InsertText(" ");
            Assert.AreEqual("abc", editable.Text);
        }
    }
}
