namespace AorusControl.Core.Features.PowerProfiles;

public sealed class ProfileCatalog
{
    public IReadOnlyList<LaptopProfile> Profiles { get; }
    public PowerProfileAssignments Assignments { get; }

    public ProfileCatalog(IReadOnlyList<LaptopProfile> profiles, PowerProfileAssignments assignments)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(assignments);
        LaptopProfile[] copy = profiles.ToArray();
        if (copy.Length > 64 || copy.Any(p => p is null)) throw new ArgumentException("Maximal 64 gültige Profile erlaubt.");
        if (copy.Select(p => p.Id).Distinct().Count() != copy.Length) throw new ArgumentException("Doppelte Profil-ID.");
        foreach (Guid? assigned in new[] { assignments.AcProfile, assignments.BatteryProfile })
            if (assigned is not null && !copy.Any(p => p.Id == assigned)) throw new ArgumentException("Zugeordnetes Profil existiert nicht.");
        Profiles = Array.AsReadOnly(copy);
        Assignments = assignments;
    }

    public ProfileCatalog Upsert(LaptopProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new(Profiles.Where(p => p.Id != profile.Id).Append(profile).ToArray(), Assignments);
    }

    public ProfileCatalog Remove(Guid id) => new(Profiles.Where(p => p.Id != id).ToArray(),
        new(Assignments.AcProfile == id ? null : Assignments.AcProfile,
            Assignments.BatteryProfile == id ? null : Assignments.BatteryProfile));
}
