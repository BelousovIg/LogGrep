namespace LogGrep.Tests.Framework;

/// <summary>
/// The base every scenario sits on. It wires the page object to the actions and the checks, and
/// hands the test the three words it writes with.
/// </summary>
public abstract class Scenario
{
    protected Scenario()
    {
        Page = new LogGrepPage();

        var act = new TestMethods(Page);
        Given = new Given(act);
        When = new When(act);
        Then = new Then(new Verification(Page));
    }

    protected LogGrepPage Page { get; }

    protected Given Given { get; }

    protected When When { get; }

    protected Then Then { get; }
}
