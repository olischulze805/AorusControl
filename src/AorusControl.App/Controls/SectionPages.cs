using System.Windows.Controls;

namespace AorusControl.App.Controls;

/// <summary>
/// Empty marker pages, one per navigation item.
///
/// WPF-UI's <c>NavigationViewItem</c> only reports a selection when it has a
/// <c>TargetPageType</c> - its OnClick returns early without one, which is why the pane
/// looked clickable but never switched sections. The app does not use Frame navigation at
/// all (all five sections live in one ContentOverlay, toggled by
/// <c>MainWindowViewModel.SelectedSection</c>), so these types exist purely to give each
/// item its own identity. They are deliberately distinct rather than one shared type:
/// NavigationView keys its navigation on the page type, so five items sharing one type
/// risks a second click being treated as "already there" and no selection being raised.
///
/// They are never displayed; nothing is ever added to them.
/// </summary>
internal sealed class DashboardPage : Page;

internal sealed class CoolingPage : Page;

internal sealed class LightingPage : Page;

internal sealed class PowerPage : Page;

internal sealed class AboutPage : Page;
