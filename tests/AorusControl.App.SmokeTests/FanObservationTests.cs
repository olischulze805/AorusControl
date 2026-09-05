using AorusControl.Core.Features.Cooling;

/// <summary>
/// What the vendor profiles do is measured, never read: they set four status flags and leave
/// the EC's fifteen curve points untouched (see research/FAN-POWER-GPU-CONTROL.md). These
/// checks pin down that the measurements stay measurements - kept per profile, per degree,
/// and never quietly averaged into something that looks like a specification.
/// </summary>
internal static class FanObservationTests
{
    public static void Run()
    {
        var observations = new FanProfileObservations();
        Check(observations.For("Gaming").Count == 0, "an unmeasured profile shows nothing rather than a guess");

        observations.Record("Gaming", 60, 55);
        observations.Record("Gaming", 70, 70);
        observations.Record("Quiet", 60, 30);

        Check(observations.For("Gaming").Count == 2, "each degree is its own point");
        Check(observations.For("Quiet").Single().Percent == 30, "profiles never share measurements");
        Check(observations.For("Gaming")[0].TemperatureCelsius == 60, "points come back in temperature order");

        // Hysteresis is real: the same degree carries different duties depending on which way
        // the temperature was moving. The newest reading wins - averaging would smooth a real
        // behaviour into a fake one.
        observations.Record("Gaming", 60, 65);
        Check(observations.For("Gaming")[0].Percent == 65, "the newest reading for a degree wins");
        Check(observations.For("Gaming")[0].Samples == 2, "and the count says how often that degree was seen");
        Check(observations.SampleCount("Gaming") == 3, "the total counts every reading, not every degree");

        // Bad readings are dropped rather than drawn: a failed sensor read reports 0 °C, and a
        // single point at 0 would bend the whole picture down to the left.
        observations.Record("Gaming", 0, 50);
        observations.Record("Gaming", 200, 50);
        observations.Record("Gaming", 60, 101);
        Check(observations.For("Gaming").Count == 2, "impossible temperatures and duties are ignored");

        // Surviving a restart matters: the picture is built over days of normal use.
        FanProfileObservations restored = FanProfileObservations.FromJson(observations.ToJson());
        Check(restored.For("Gaming").Count == 2 && restored.For("Quiet").Count == 1, "measurements survive a round trip");
        Check(restored.For("Gaming")[0].Samples == 2, "so do the sample counts");

        // Fail-soft: these are measurements, not settings. A damaged file starts the picture
        // again instead of becoming an error the user has to deal with.
        Check(FanProfileObservations.FromJson("{ kaputt").For("Gaming").Count == 0, "a corrupt file is simply empty");
        Check(FanProfileObservations.FromJson(null).SampleCount("Gaming") == 0, "so is a missing one");

        Console.WriteLine("PASS: profile behaviour is measured per degree, kept per profile, and survives a restart");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
