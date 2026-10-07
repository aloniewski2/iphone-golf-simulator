// Just enough of NUnit.Framework (3.x API shape) to run Unity/Assets/Tests/EditMode/*.cs unchanged outside Unity.
// Attributes: Test, TestFixture, TestCase(args), SetUp, TearDown, OneTimeSetUp, OneTimeTearDown, Ignore, Category, Description, Explicit.
// Assert: AreEqual (exact, with delta, with message), AreNotEqual, AreSame, IsTrue/IsFalse/True/False, IsNull/IsNotNull, Greater/GreaterOrEqual/Less/LessOrEqual,
//         StringAssert.Contains/DoesNotContain/StartsWith/EndsWith/AreEqualIgnoringCase,
//         IsEmpty/IsNotEmpty, Contains, Fail/Pass/Ignore/Inconclusive, Throws<T>/Throws(Type)/DoesNotThrow, Zero/NotZero, That(bool) and That(actual, constraint).
// Constraints: Is.EqualTo(x)[.Within(t)], Is.True/False/Null/NotNull/Empty/Positive/Negative/Zero, Is.GreaterThan/LessThan/...OrEqualTo, Is.InRange(a,b), Is.Not.<any>, Is.SameAs, Is.InstanceOf<T>.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)] public sealed class TestAttribute : Attribute { public string Description; }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)] public sealed class TestFixtureAttribute : Attribute { public TestFixtureAttribute() { } public TestFixtureAttribute(params object[] args) { } }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)] public sealed class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)] public sealed class TearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)] public sealed class OneTimeSetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)] public sealed class OneTimeTearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)] public sealed class IgnoreAttribute : Attribute { public readonly string Reason; public IgnoreAttribute(string reason) { Reason = reason; } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)] public sealed class CategoryAttribute : Attribute { public CategoryAttribute(string name) { } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)] public sealed class ExplicitAttribute : Attribute { public ExplicitAttribute() { } public ExplicitAttribute(string reason) { } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)] public sealed class DescriptionAttribute : Attribute { public DescriptionAttribute(string text) { } }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute
    {
        public readonly object[] Arguments;
        public object ExpectedResult;
        public string TestName;
        public TestCaseAttribute(params object[] arguments) { Arguments = arguments ?? new object[] { null }; }
    }

    public delegate void TestDelegate();

    public class AssertionException : Exception { public AssertionException(string message) : base(message) { } }
    public class IgnoreException : Exception { public IgnoreException(string message) : base(message) { } }
    public class InconclusiveException : Exception { public InconclusiveException(string message) : base(message) { } }
    public class SuccessException : Exception { public SuccessException(string message) : base(message) { } }

    // ---------------------------------------------------------------------------------------------- equality / comparison helpers
    static class Eq
    {
        public static bool IsNumeric(object o) => o is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

        public static string Show(object o)
        {
            if (o == null) return "null";
            if (o is string s) return "\"" + s + "\"";
            if (o is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            if (o is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (o is IEnumerable e && o is not string)
                return "< " + string.Join(", ", e.Cast<object>().Take(40).Select(Show)) + " >";
            if (o is IFormattable fm) return fm.ToString(null, CultureInfo.InvariantCulture);
            return o.ToString();
        }

        public static bool Equal(object a, object b, double? tolerance)
        {
            if (a == null || b == null) return a == null && b == null;
            if (IsNumeric(a) && IsNumeric(b))
            {
                double x = Convert.ToDouble(a, CultureInfo.InvariantCulture), y = Convert.ToDouble(b, CultureInfo.InvariantCulture);
                if (double.IsNaN(x) && double.IsNaN(y)) return true;
                if (double.IsInfinity(x) || double.IsInfinity(y)) return x == y;
                return Math.Abs(x - y) <= (tolerance ?? 0);
            }
            if (a is string sa && b is string sb) return sa == sb;
            if (a is IEnumerable ea && b is IEnumerable eb && a is not string && b is not string)
            {
                var la = ea.Cast<object>().ToList(); var lb = eb.Cast<object>().ToList();
                if (la.Count != lb.Count) return false;
                for (int i = 0; i < la.Count; i++) if (!Equal(la[i], lb[i], tolerance)) return false;
                return true;
            }
            return a.Equals(b);
        }

        public static int Compare(object a, object b)
        {
            if (IsNumeric(a) && IsNumeric(b)) return Convert.ToDouble(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(b, CultureInfo.InvariantCulture));
            if (a is IComparable c) return c.CompareTo(b);
            throw new ArgumentException($"cannot compare {a?.GetType().Name} with {b?.GetType().Name}");
        }
    }

    // ---------------------------------------------------------------------------------------------- constraints
    public abstract class Constraint
    {
        public abstract bool Matches(object actual);
        public abstract string Describe();
        public virtual Constraint Not => new NotConstraint(this);
        public override string ToString() => Describe();
    }

    public class NotConstraint : Constraint
    {
        readonly Constraint inner;
        public NotConstraint(Constraint inner) { this.inner = inner; }
        public override bool Matches(object actual) => !inner.Matches(actual);
        public override string Describe() => "not " + inner.Describe();
        public override Constraint Not => inner;
    }

    public sealed class EqualConstraint : Constraint
    {
        readonly object expected; readonly bool negate; double? tolerance;
        public EqualConstraint(object expected, bool negate = false) { this.expected = expected; this.negate = negate; }
        public EqualConstraint Within(double amount) { tolerance = amount; return this; }
        public EqualConstraint Within(int amount) { tolerance = amount; return this; }
        public override bool Matches(object actual) => Eq.Equal(expected, actual, tolerance ?? 0) != negate;
        public override string Describe() => (negate ? "not " : "") + Eq.Show(expected) + (tolerance.HasValue ? " +/- " + Eq.Show(tolerance.Value) : "");
        public override Constraint Not => new EqualConstraint(expected, !negate) { tolerance = tolerance };
    }

    public sealed class PredicateConstraint : Constraint
    {
        readonly Func<object, bool> test; readonly string text;
        public PredicateConstraint(string text, Func<object, bool> test) { this.text = text; this.test = test; }
        public override bool Matches(object actual) { try { return test(actual); } catch (Exception) { return false; } }
        public override string Describe() => text;
    }

    public static class Is
    {
        public static EqualConstraint EqualTo(object expected) => new EqualConstraint(expected);
        public static Constraint True => new PredicateConstraint("True", a => a is bool b && b);
        public static Constraint False => new PredicateConstraint("False", a => a is bool b && !b);
        public static Constraint Null => new PredicateConstraint("null", a => a == null);
        public static Constraint NotNull => new PredicateConstraint("not null", a => a != null);
        public static Constraint Empty => new PredicateConstraint("<empty>", a => a is string s ? s.Length == 0 : a is IEnumerable e && !e.GetEnumerator().MoveNext());
        public static Constraint Positive => new PredicateConstraint("greater than 0", a => Eq.Compare(a, 0) > 0);
        public static Constraint Negative => new PredicateConstraint("less than 0", a => Eq.Compare(a, 0) < 0);
        public static Constraint Zero => new PredicateConstraint("0", a => Eq.Compare(a, 0) == 0);
        public static Constraint NaN => new PredicateConstraint("NaN", a => a is double d && double.IsNaN(d));
        public static Constraint GreaterThan(object x) => new PredicateConstraint("greater than " + Eq.Show(x), a => Eq.Compare(a, x) > 0);
        public static Constraint GreaterThanOrEqualTo(object x) => new PredicateConstraint("greater than or equal to " + Eq.Show(x), a => Eq.Compare(a, x) >= 0);
        public static Constraint AtLeast(object x) => GreaterThanOrEqualTo(x);
        public static Constraint LessThan(object x) => new PredicateConstraint("less than " + Eq.Show(x), a => Eq.Compare(a, x) < 0);
        public static Constraint LessThanOrEqualTo(object x) => new PredicateConstraint("less than or equal to " + Eq.Show(x), a => Eq.Compare(a, x) <= 0);
        public static Constraint AtMost(object x) => LessThanOrEqualTo(x);
        public static Constraint InRange(object lo, object hi) => new PredicateConstraint($"in range ({Eq.Show(lo)},{Eq.Show(hi)})", a => Eq.Compare(a, lo) >= 0 && Eq.Compare(a, hi) <= 0);
        public static Constraint SameAs(object x) => new PredicateConstraint("same as " + Eq.Show(x), a => ReferenceEquals(a, x));
        public static Constraint InstanceOf<T>() => new PredicateConstraint("instance of " + typeof(T).Name, a => a is T);
        public static NotBuilder Not => new NotBuilder();

        public sealed class NotBuilder
        {
            public EqualConstraint EqualTo(object expected) => new EqualConstraint(expected, true);
            public Constraint Null => Is.Null.Not;
            public Constraint Empty => Is.Empty.Not;
            public Constraint True => Is.True.Not;
            public Constraint False => Is.False.Not;
            public Constraint Zero => Is.Zero.Not;
            public Constraint GreaterThan(object x) => Is.GreaterThan(x).Not;
            public Constraint LessThan(object x) => Is.LessThan(x).Not;
            public Constraint InRange(object lo, object hi) => Is.InRange(lo, hi).Not;
            public Constraint SameAs(object x) => Is.SameAs(x).Not;
        }
    }

    // ---------------------------------------------------------------------------------------------- Assert
    public static class Assert
    {
        static string Msg(string message, object[] args) => string.IsNullOrEmpty(message) ? "" : (args != null && args.Length > 0 ? string.Format(message, args) : message);
        static void Fail(string expected, string actual, string message)
        {
            var m = Msg(message, null);
            throw new AssertionException((m.Length > 0 ? m + "\n" : "") + "  Expected: " + expected + "\n  But was:  " + actual);
        }

        public static void Fail() => throw new AssertionException("Assert.Fail()");
        public static void Fail(string message) => throw new AssertionException(message);
        public static void Fail(string message, params object[] args) => throw new AssertionException(Msg(message, args));
        public static void Pass() => throw new SuccessException("pass");
        public static void Ignore(string message) => throw new IgnoreException(message);
        public static void Inconclusive(string message) => throw new InconclusiveException(message);

        // ---- equality
        public static void AreEqual(double expected, double actual, double delta) => AreEqual(expected, actual, delta, null);
        public static void AreEqual(double expected, double actual, double delta, string message)
        {
            if (double.IsNaN(expected) && double.IsNaN(actual)) return;
            if (!(Math.Abs(expected - actual) <= delta)) Fail(Eq.Show(expected) + " +/- " + Eq.Show(delta), Eq.Show(actual), message);
        }
        public static void AreEqual(object expected, object actual) => AreEqual(expected, actual, (string)null);
        public static void AreEqual(object expected, object actual, string message)
        {
            if (!Eq.Equal(expected, actual, 0)) Fail(Eq.Show(expected), Eq.Show(actual), message);
        }
        public static void AreNotEqual(object expected, object actual) => AreNotEqual(expected, actual, null);
        public static void AreNotEqual(object expected, object actual, string message)
        {
            if (Eq.Equal(expected, actual, 0)) Fail("not " + Eq.Show(expected), Eq.Show(actual), message);
        }
        public static void AreSame(object expected, object actual) { if (!ReferenceEquals(expected, actual)) Fail("same instance as " + Eq.Show(expected), Eq.Show(actual), null); }
        public static void AreNotSame(object expected, object actual) { if (ReferenceEquals(expected, actual)) Fail("a different instance", Eq.Show(actual), null); }

        // ---- booleans, null
        public static void IsTrue(bool condition) => IsTrue(condition, null);
        public static void IsTrue(bool condition, string message) { if (!condition) Fail("True", "False", message); }
        public static void IsFalse(bool condition) => IsFalse(condition, null);
        public static void IsFalse(bool condition, string message) { if (condition) Fail("False", "True", message); }
        public static void True(bool condition) => IsTrue(condition, null);
        public static void True(bool condition, string message) => IsTrue(condition, message);
        public static void False(bool condition) => IsFalse(condition, null);
        public static void False(bool condition, string message) => IsFalse(condition, message);
        public static void IsNull(object value) => IsNull(value, null);
        public static void IsNull(object value, string message) { if (value != null) Fail("null", Eq.Show(value), message); }
        public static void IsNotNull(object value) => IsNotNull(value, null);
        public static void IsNotNull(object value, string message) { if (value == null) Fail("not null", "null", message); }
        public static void Null(object value) => IsNull(value, null);
        public static void NotNull(object value) => IsNotNull(value, null);
        public static void Zero(double value) { if (value != 0) Fail("0", Eq.Show(value), null); }
        public static void NotZero(double value) { if (value == 0) Fail("not 0", "0", null); }

        // ---- comparisons (numeric types coerce like NUnit)
        public static void Greater(object arg1, object arg2) => Greater(arg1, arg2, null);
        public static void Greater(object arg1, object arg2, string message) { if (!(Eq.Compare(arg1, arg2) > 0)) Fail("greater than " + Eq.Show(arg2), Eq.Show(arg1), message); }
        public static void GreaterOrEqual(object arg1, object arg2) => GreaterOrEqual(arg1, arg2, null);
        public static void GreaterOrEqual(object arg1, object arg2, string message) { if (!(Eq.Compare(arg1, arg2) >= 0)) Fail("greater than or equal to " + Eq.Show(arg2), Eq.Show(arg1), message); }
        public static void Less(object arg1, object arg2) => Less(arg1, arg2, null);
        public static void Less(object arg1, object arg2, string message) { if (!(Eq.Compare(arg1, arg2) < 0)) Fail("less than " + Eq.Show(arg2), Eq.Show(arg1), message); }
        public static void LessOrEqual(object arg1, object arg2) => LessOrEqual(arg1, arg2, null);
        public static void LessOrEqual(object arg1, object arg2, string message) { if (!(Eq.Compare(arg1, arg2) <= 0)) Fail("less than or equal to " + Eq.Show(arg2), Eq.Show(arg1), message); }

        // ---- collections
        public static void IsEmpty(IEnumerable collection) => IsEmpty(collection, null);
        public static void IsEmpty(IEnumerable collection, string message) { if (collection is string s ? s.Length != 0 : collection.GetEnumerator().MoveNext()) Fail("<empty>", Eq.Show(collection), message); }
        public static void IsNotEmpty(IEnumerable collection) => IsNotEmpty(collection, null);
        public static void IsNotEmpty(IEnumerable collection, string message) { if (collection is string s ? s.Length == 0 : !collection.GetEnumerator().MoveNext()) Fail("not <empty>", "<empty>", message); }
        public static void Contains(object expected, ICollection actual) => Contains(expected, actual, null);
        public static void Contains(object expected, ICollection actual, string message) { if (!actual.Cast<object>().Any(x => Eq.Equal(expected, x, 0))) Fail("collection containing " + Eq.Show(expected), Eq.Show(actual), message); }

        // ---- exceptions
        public static T Throws<T>(TestDelegate code) where T : Exception => (T)Throws(typeof(T), code);
        public static Exception Throws(Type expected, TestDelegate code)
        {
            Exception caught = null;
            try { code(); } catch (Exception e) { caught = e; }
            if (caught == null) { Fail(expected.Name, "no exception", null); }
            if (caught.GetType() != expected) Fail(expected.Name, caught.GetType().Name + ": " + caught.Message, null);
            return caught;
        }
        public static void DoesNotThrow(TestDelegate code) { try { code(); } catch (Exception e) { Fail("no exception", e.GetType().Name + ": " + e.Message, null); } }

        // ---- constraint model
        public static void That(bool condition) => That(condition, (string)null);
        public static void That(bool condition, string message) { if (!condition) Fail("True", "False", message); }
        public static void That(object actual, Constraint constraint) => That(actual, constraint, null);
        public static void That(object actual, Constraint constraint, string message)
        {
            if (!constraint.Matches(actual)) Fail(constraint.Describe(), Eq.Show(actual), message);
        }
    }

    public static class StringAssert
    {
        static void Check(bool ok, string expected, string actual, string message) { if (!ok) throw new AssertionException((string.IsNullOrEmpty(message) ? "" : message + "\n") + "  Expected: " + expected + "\n  But was:  " + Eq.Show(actual)); }
        public static void Contains(string expected, string actual) => Contains(expected, actual, null);
        public static void Contains(string expected, string actual, string message) => Check(actual != null && actual.Contains(expected, StringComparison.Ordinal), "String containing " + Eq.Show(expected), actual, message);
        public static void DoesNotContain(string expected, string actual) => DoesNotContain(expected, actual, null);
        public static void DoesNotContain(string expected, string actual, string message) => Check(actual == null || !actual.Contains(expected, StringComparison.Ordinal), "String not containing " + Eq.Show(expected), actual, message);
        public static void StartsWith(string expected, string actual) => Check(actual != null && actual.StartsWith(expected, StringComparison.Ordinal), "String starting with " + Eq.Show(expected), actual, null);
        public static void EndsWith(string expected, string actual) => Check(actual != null && actual.EndsWith(expected, StringComparison.Ordinal), "String ending with " + Eq.Show(expected), actual, null);
        public static void AreEqualIgnoringCase(string expected, string actual) => Check(string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase), Eq.Show(expected) + " (ignoring case)", actual, null);
    }
}
