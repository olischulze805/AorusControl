using System.IO;
using AorusControl.App.ViewModels;
using AorusControl.Core.Features.PowerProfiles;

internal static class ProfileEditorTests
{
    public static async Task RunAsync()
    {
        ProfileCatalog? stored = null;
        bool fail = false;
        var vm = new ProfileEditorViewModel(() => stored, candidate =>
        {
            if (fail) throw new IOException("Testfehler");
            stored = candidate;
        });
        await vm.Initialization;
        vm.Name = "Netz";
        await vm.SaveCommand.ExecuteAsync();
        Guid id = vm.Profiles.Single().Id;
        vm.AcProfile = id;
        vm.BatteryProfile = id;
        await vm.AssignCommand.ExecuteAsync();
        Check(stored!.Assignments == new PowerProfileAssignments(id, id), "both assignments saved");
        vm.Selected = vm.Profiles.Single();
        vm.LoadSelectedCommand.Execute(null);
        vm.Name = "Umbenannt";
        fail = true;
        await vm.SaveCommand.ExecuteAsync();
        Check(vm.Profiles.Single().Name == "Netz" && vm.Status.Contains("Testfehler"), "failed save preserves catalog");
        fail = false;
        await vm.SaveCommand.ExecuteAsync();
        Check(vm.Profiles.Single().Id == id && stored.Profiles.Single().Name == "Umbenannt", "editing keeps ID");
        vm.CoolingMode = ProfileCoolingMode.CustomCurve;
        vm.CurveText = "30:57";
        await vm.SaveCommand.ExecuteAsync();
        Check(vm.Status.Contains("15 Zeilen") && vm.Profiles.Single().CoolingMode == ProfileCoolingMode.Normal, "invalid curve rejected");
        vm.CurveText = string.Join('\n', Enumerable.Range(0, 15).Select(i => $"{30 + i * 4}:{(i == 14 ? 229 : 57 + i * 10)}"));
        await vm.SaveCommand.ExecuteAsync();
        Check(stored.Profiles.Single().Curve?.Count == 15, "custom curve saved");
        Check(vm.CurveRows.Count == 15 && vm.CurveRows[14].Value == "229", "curve loaded into fixed-size table");
        vm.CurveRows[0].Temperature = "31";
        Check(vm.HasDraftChanges, "table edit marks draft changed");
        await vm.SaveCommand.ExecuteAsync();
        Check(stored.Profiles.Single().Curve![0].Temperature == 31, "table edit persisted");
        vm.CurveRows[3].Value = "abc";
        await vm.SaveCommand.ExecuteAsync();
        Check(vm.Status.Contains("Punkt 4") && vm.HasDraftChanges, "invalid table input stays visible and is not saved");
        vm.CurveRows[3].Value = "87";
        await vm.SaveCommand.ExecuteAsync();
        vm.Selected = vm.Profiles.Single();
        await vm.DeleteCommand.ExecuteAsync();
        Check(stored.Profiles.Count == 0 && stored.Assignments == new PowerProfileAssignments(null, null), "delete clears assignments");

        bool wrote = false;
        var broken = new ProfileEditorViewModel(() => throw new InvalidDataException("Kaputte Datei"), _ => wrote = true);
        await broken.Initialization;
        broken.Name = "Nicht überschreiben";
        await broken.SaveCommand.ExecuteAsync();
        Check(!wrote && broken.Status.Contains("nicht überschrieben"), "failed load cannot replace original file");
        bool discard = false;
        var drafts = new ProfileEditorViewModel(() => null, _ => { }, _ => discard);
        await drafts.Initialization;
        Check(!drafts.HasUnsavedChanges, "empty initial draft is clean");
        drafts.Name = "Entwurf";
        drafts.NewCommand.Execute(null);
        Check(drafts.Name == "Entwurf" && drafts.HasDraftChanges, "declined discard retains draft");
        discard = true;
        drafts.NewCommand.Execute(null);
        Check(drafts.Name == "" && !drafts.HasDraftChanges, "confirmed discard resets draft");
        drafts.Name = "Profil";
        await drafts.SaveCommand.ExecuteAsync();
        Guid draftId = drafts.Profiles.Single().Id;
        drafts.AcProfile = draftId;
        drafts.Name = "Anderer Name";
        await drafts.SaveCommand.ExecuteAsync();
        Check(drafts.AcProfile == draftId && drafts.HasAssignmentChanges && !drafts.HasDraftChanges, "profile save preserves unsaved assignment");
        drafts.Name = "Noch nicht speichern";
        await drafts.AssignCommand.ExecuteAsync();
        Check(drafts.HasDraftChanges && !drafts.HasAssignmentChanges && drafts.Name == "Noch nicht speichern", "assignment save preserves unsaved profile");
        discard = false;
        await drafts.ReloadCommand.ExecuteAsync();
        Check(drafts.Name == "Noch nicht speichern", "reload protects unsaved draft");
        Console.WriteLine("PASS: profile editor save/edit/assign/delete, curves, disk failure and corrupt-file protection");
        Console.WriteLine("PASS: profile editor discard decisions and independent pending profile/assignment changes");
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int writes = 0;
        var slow = new ProfileEditorViewModel(() => null, _ =>
        {
            Interlocked.Increment(ref writes);
            entered.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Test writer not released");
        });
        await slow.Initialization;
        slow.Name = "Langsamer Datenträger";
        Task save = slow.SaveCommand.ExecuteAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check(slow.IsBusy && !slow.CanEdit && !save.IsCompleted, "save returns without blocking caller");
            await slow.SaveCommand.ExecuteAsync();
            await slow.AssignCommand.ExecuteAsync();
            slow.NewCommand.Execute(null);
            Check(writes == 1 && slow.Name == "Langsamer Datenträger" && slow.Profiles.Count == 0, "busy operations cannot overlap or publish early");
        }
        finally { release.Set(); }
        await save;
        Check(!slow.IsBusy && slow.Profiles.Count == 1, "publish after completion and unlock editor");
        Console.WriteLine("PASS: asynchronous profile I/O, busy gate, no duplicate writes and publication after completion");
    }
    private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
}
