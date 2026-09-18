namespace AorusControl.Core.Features.PowerProfiles;

public interface IBatterySaver
{
    bool IsOn { get; }

    /// <summary>The copy currently in force, so it can be written down and cleaned up after
    /// a crash. Empty when the saver is off.</summary>
    Guid Scheme { get; }

    /// <summary>Removes a scheme left behind by a previous run. Quiet about a scheme that is
    /// already gone - that is the normal case.</summary>
    void DiscardLeftover(Guid scheme);

    /// <summary>Turns the saver on with the given values, and returns what it actually set.</summary>
    IReadOnlyList<SaverLever> TurnOn(uint processorCap, uint brightness);

    /// <summary>Goes back to the scheme that was in force before, and removes the copy.</summary>
    void TurnOff();
}

/// <summary>
/// Switches the machine onto a power scheme of this app's own making, and back off it.
///
/// The whole design is in one sentence: the user's power plans are never written to. A copy
/// is made, the copy is changed, the copy is activated, and turning the saver off activates
/// whatever was active before and deletes the copy again.
///
/// That matters because of how the same feature failed in Gigabyte's version. It wrote into
/// the Balanced plan and kept the old values in the registry, so a crash between setting and
/// restoring left the machine capped at 50 % with nothing left that knew why. Here the worst
/// a crash can leave behind is an unused entry in Windows' plan list - and even that is
/// cleaned up at the next start, because the name is ours and recognisable.
/// </summary>
public sealed class BatterySaver : IBatterySaver
{
    private SaverBaseline? _baseline;
    private Guid _scheme;

    public bool IsOn => _baseline is not null;
    public Guid Scheme => _scheme;

    public void DiscardLeftover(Guid scheme)
    {
        if (scheme == Guid.Empty || scheme == PowerSchemeWriter.Active()) return;
        try { PowerSchemeWriter.Delete(scheme); }
        catch { /* Already gone, or not ours any more. Either way there is nothing to do. */ }
    }

    public IReadOnlyList<SaverLever> TurnOn(uint processorCap, uint brightness)
    {
        if (IsOn) TurnOff();

        Guid previous = PowerSchemeWriter.Active();
        Guid copy = PowerSchemeWriter.Duplicate(previous, BatterySaverPlan.SchemeName, BatterySaverPlan.SchemeDescription);
        try
        {
            IReadOnlyList<SaverLever> levers = BatterySaverPlan.Levers(processorCap, brightness);
            foreach (SaverLever lever in levers)
                PowerSchemeWriter.WriteOnBattery(copy, lever.Setting, lever.Percent);

            PowerSchemeWriter.Activate(copy);
            _scheme = copy;
            _baseline = new SaverBaseline(previous);
            return levers;
        }
        catch
        {
            // Nothing was activated, so nothing has to be handed back - but the copy exists
            // and would otherwise sit in the user's list for ever.
            try { PowerSchemeWriter.Delete(copy); } catch { /* Tidying up is not worth a second failure. */ }
            throw;
        }
    }

    public void TurnOff()
    {
        if (_baseline is not { } baseline) return;
        _baseline = null;

        // Order matters: a scheme cannot be deleted while it is the active one, and leaving
        // the machine on a plan that is about to vanish would be worse than leaving the plan.
        PowerSchemeWriter.Activate(baseline.PreviousScheme);
        try { PowerSchemeWriter.Delete(_scheme); }
        catch { /* The user is back on their own plan, which was the point. */ }
        _scheme = Guid.Empty;
    }
}
