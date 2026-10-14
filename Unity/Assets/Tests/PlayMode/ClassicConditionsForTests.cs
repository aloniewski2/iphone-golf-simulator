using NUnit.Framework;

/// The automated tests measure plain golf — no twists, clear skies, nothing moving about the
/// holes — unless a test deals the round's events itself (RoundEventsPlayTests). (Outside any
/// namespace: it covers every test in the assembly.)
[SetUpFixture]
public class ClassicConditionsForTests
{
    [OneTimeSetUp] public void Plain() => GolfArcade.Course.RoundEvents.Enabled = false;
    [OneTimeTearDown] public void Back() => GolfArcade.Course.RoundEvents.Enabled = true;
}
