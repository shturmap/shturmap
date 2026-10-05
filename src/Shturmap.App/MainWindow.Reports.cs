using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Shturmap.Session;
using Shturmap.Session.Reporting;

namespace Shturmap.App;

// Reports and crash reports: one Report dialog, reached from help, from notices that ask for a report and after a
// crash; and the one question after a crash (owner, 2026-10-03; docs/DESIGN.md §8, "Reports").
public sealed partial class MainWindow
{
    private static Reporter Reporter => App.Reporter;

    private ReportKind _reportKind = ReportKind.Problem;
    private IReadOnlyList<CrashRecord> _crashes = [];
    private string? _crashSentId;
    private DispatcherQueueTimer? _crashFade;

    /// <summary>This build can send reports.</summary>
    public bool ReportingAvailable => Reporter.Configured;

    public Visibility NotConfiguredVisibility => Reporter.Configured ? Visibility.Collapsed : Visibility.Visible;

    public Brush ReportLinkBrush => Resource(Reporter.Configured ? "AmberBrush" : "MutedBrush");

    /// <summary>The feedback button's icon: ink like "?" and the gear, muted where this build can't report.</summary>
    public Brush FeedbackIconBrush => Resource(Reporter.Configured ? "InkBrush" : "MutedBrush");

    public string FeedbackTooltip => Reporter.Configured ? "Report a problem or idea" : "Reporting isn't set up in this build";

    /// <summary>Help's pointer to the feedback button, which replaced the report link there (owner, 2026-10-03).</summary>
    public string FeedbackPointer => "Problem or idea? Use the feedback button beside the ?.";

    /// <summary>A notice that asks for a report links to the dialog, or to the diagnostics where there is none.</summary>
    public string ReportOfferText => Reporter.Configured ? "REPORT" : "COPY DIAGNOSTICS";

    public Visibility ShownIfMode(string mode, string value) => mode == value ? Visibility.Visible : Visibility.Collapsed;

    public Brush CrashModeBrush(string mode, string value) => Resource(mode == value ? "AmberBrush" : "MutedBrush");

    private bool ReportOpen => ReportOverlay.Visibility == Visibility.Visible;

    /// <summary>The session has started: its settings can be read.</summary>
    public void ReportsReady() => LoadCrashMode();

    private void LoadCrashMode() => ViewModel.CrashMode = CrashModes.Parse(_session.GetSetting(CrashModes.Setting)).ToString();

    private void OnCrashModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<CrashMode>(tag, out var mode))
            SetCrashMode(mode, "settings");
    }

    private void SetCrashMode(CrashMode mode, string how)
    {
        _session.SetSetting(CrashModes.Setting, CrashModes.Format(mode));
        ViewModel.CrashMode = mode.ToString();
        Study.Ui("crash.mode", ("mode", mode), ("how", how));
        AppLog.Info("Crash reports: " + CrashModes.Format(mode));
        // Never takes back a Send given earlier, at once: a record approved but not yet sent stays on this PC.
        if (mode == CrashMode.Never && !Reporter.Muted)
            Reporter.Keep(Reporter.Crashes.Waiting().Where(r => r.State == CrashState.Approved).ToList());
    }

    // The privacy notice, bundled with the build (PRIVACY.md as privacy.txt beside the exe).
    public static string PrivacyFile { get; } = Path.Combine(AppContext.BaseDirectory, "privacy.txt");

    private void OnPrivacyClick(object sender, RoutedEventArgs e) => OpenPrivacy();

    private void OnPrivacyLinkClick(Hyperlink sender, HyperlinkClickEventArgs args) => OpenPrivacy();

    private void OpenPrivacy()
    {
        Study.Ui("help.privacy");
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PrivacyFile) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Opening the privacy notice failed", ex);
            ShowNotice(@"Couldn't open the privacy notice; it is privacy.txt beside Shturmap.exe.");
        }
    }

    // ---- the Report dialog ----

    // The feedback button at the top right (owner, 2026-10-03: "a separate feedback/bugreport button").
    private void OnFeedbackClick(object sender, RoutedEventArgs e) => OpenReport(ReportKind.Problem, null, "button");

    private void OnNoticeReportClick(object sender, RoutedEventArgs e)
    {
        if (!Reporter.Configured)
        {
            OnCopyDiagnosticsClick(sender, e);
            return;
        }
        ViewModel.NoticeOpen = false;
        OpenReport(ReportKind.Problem, null, "notice");
    }

    /// <summary>Opens the dialog; what was typed before and not sent is still there.</summary>
    public void OpenReport(ReportKind kind, string? prefill, string how, bool showSent = false)
    {
        if (!Reporter.Configured && !SnapshotMode)
        {
            ShowNotice("Reporting isn't set up in this build: copy the diagnostics in help (?) and send them to whoever gave you Shturmap.");
            return;
        }
        if (ReportSend.Visibility == Visibility.Collapsed)
            ResetReport();
        SetReportKind(kind);
        if (prefill is not null && ReportText.Text.Trim().Length == 0)
            ReportText.Text = prefill;
        ReportStatusText.Visibility = Visibility.Collapsed;
        ReportSentView.Visibility = showSent ? Visibility.Visible : Visibility.Collapsed;
        ReportShowSentText.Text = showSent ? "HIDE WHAT'S SENT" : "SHOW WHAT'S SENT";
        RefreshReport();
        ReportOverlay.Visibility = Visibility.Visible;
        ReportText.Focus(FocusState.Programmatic);
        ReportText.SelectionStart = ReportText.Text.Length;
        Study.Ui("report.open", ("how", how));
    }

    private void CloseReport(string how)
    {
        ReportOverlay.Visibility = Visibility.Collapsed;
        Study.Ui("report.close", ("how", how));
    }

    // After a report went (or was kept to go later), the next opening starts empty.
    private void ResetReport()
    {
        ReportText.Text = "";
        ReportContact.Text = "";
        ReportText.IsEnabled = ReportContact.IsEnabled = true;
        ReportIncludeDiagnostics.IsChecked = true;
        ReportSend.Visibility = Visibility.Visible;
        ReportCancel.Content = "CANCEL";
    }

    private void SetReportKind(ReportKind kind)
    {
        _reportKind = kind;
        ReportProblem.IsChecked = kind == ReportKind.Problem;
        ReportIdea.IsChecked = kind == ReportKind.Idea;
        ReportText.PlaceholderText = kind == ReportKind.Problem
            ? "What happened, and what did you expect? (Required)"
            : "What would help, and when would you use it? (Required)";
        RefreshReport();
    }

    private void OnReportKindClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<ReportKind>(tag, out var kind))
            SetReportKind(kind);
    }

    private void OnReportInputChanged(object sender, TextChangedEventArgs e) => RefreshReport();

    private void OnReportDiagnosticsClick(object sender, RoutedEventArgs e) => RefreshReport();

    private void OnReportShowSentClick(object sender, RoutedEventArgs e)
    {
        var show = ReportSentView.Visibility != Visibility.Visible;
        ReportSentView.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ReportShowSentText.Text = show ? "HIDE WHAT'S SENT" : "SHOW WHAT'S SENT";
        RefreshReport();
        if (show)
            Study.Ui("report.preview");
    }

    // The box, the Send button and the preview follow what the player typed and chose.
    private void RefreshReport()
    {
        if (ReportText is null || ReportIncludeDiagnostics is null)
            return;
        var include = ReportIncludeDiagnostics.IsChecked == true;
        ReportDiagnosticsBox.Background = include ? Resource("AmberBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        ReportDiagnosticsCheck.Visibility = include ? Visibility.Visible : Visibility.Collapsed;
        ReportSend.IsEnabled = UserReport.Invalid(ReportText.Text, ReportContact.Text) is null;
        if (ReportSentView.Visibility == Visibility.Visible)
            ReportSentText.Text = ReportPreview(include);
    }

    // Exactly what goes out: the report as typed, who sends it, and the diagnostics as they are attached.
    private string ReportPreview(bool includeDiagnostics)
    {
        var contact = ReportContact.Text.Trim();
        return $"Kind: {(_reportKind == ReportKind.Problem ? "problem" : "idea")}{Environment.NewLine}" +
               $"Text: {(ReportText.Text.Trim().Length > 0 ? ReportText.Text.Trim() : "(not written yet)")}{Environment.NewLine}" +
               $"Contact: {(contact.Length > 0 ? contact : "(none)")}{Environment.NewLine}" +
               $"Sent by: Shturmap {Reporter.Info.Version} ({Reporter.Info.Build}), {Reporter.Info.Windows}{Environment.NewLine}{Environment.NewLine}" +
               (includeDiagnostics ? "Attached diagnostics:" + Environment.NewLine + DiagnosticsText() : "No diagnostics attached.");
    }

    private async void OnReportSendClick(object sender, RoutedEventArgs e)
    {
        if (UserReport.Invalid(ReportText.Text, ReportContact.Text) is { } invalid)
        {
            ShowReportStatus(invalid, "MutedBrush");
            return;
        }
        var report = new UserReport(UserReport.NewId(), DateTime.Now, _reportKind, ReportText.Text.Trim(), ReportContact.Text.Trim(),
            ReportIncludeDiagnostics.IsChecked == true ? DiagnosticsText() : null);
        ReportSend.IsEnabled = false;
        ShowReportStatus("Sending…", "MutedBrush");
        var result = await Task.Run(() => Reporter.SendAsync(report));
        Study.Ui("report.send", ("kind", report.Kind), ("status", result.Status), ("diagnostics", report.Diagnostics is not null));
        if (result.Status is ReportStatus.Sent or ReportStatus.Kept or ReportStatus.Refused)
        {
            // It has gone, or waits in the outbox: the dialog says so and closes on the player's word.
            ShowReportStatus(result.Message, result.Status == ReportStatus.Sent ? "AmberBrush" : "InkBrush");
            ReportText.IsEnabled = ReportContact.IsEnabled = false;
            ReportSend.Visibility = Visibility.Collapsed;
            ReportCancel.Content = "CLOSE";
        }
        else
        {
            ShowReportStatus(result.Message, "MutedBrush");
            ReportSend.IsEnabled = true;
        }
    }

    private void ShowReportStatus(string text, string brush)
    {
        ReportStatusText.Text = text;
        ReportStatusText.Foreground = Resource(brush);
        ReportStatusText.Visibility = Visibility.Visible;
    }

    private void OnReportCancelClick(object sender, RoutedEventArgs e) => CloseReport(ReportSend.Visibility == Visibility.Visible ? "cancel" : "close");

    /// <summary>Developer aid ("--send-report"): the dialog's own Send, with the given text and diagnostics.</summary>
    public async Task<string> SendReportForTestAsync(string text)
    {
        OpenReport(ReportKind.Problem, null, "test");
        ReportText.Text = text;
        ReportIncludeDiagnostics.IsChecked = true;
        RefreshReport();
        OnReportSendClick(this, new RoutedEventArgs());
        for (var i = 0; i < 60 && ReportStatusText.Text is "Sending…" or ""; i++)
            await Task.Delay(500);
        return ReportStatusText.Text;
    }

    // ---- after a crash ----

    /// <summary>The start found crashes or errors the player hasn't answered: one question, until answered.</summary>
    public void AskAboutCrashes(IReadOnlyList<CrashRecord> records)
    {
        _crashes = records;
        var fatal = records.Count(r => r.Fatal);
        ViewModel.CrashQuestion = fatal == 0
            ? (records.Count == 1 ? "Shturmap ran into an error last time." : $"Shturmap ran into {records.Count} errors since you last answered.") +
              " Send an error report? It helps to fix it."
            : (fatal == 1 ? "Shturmap closed unexpectedly last time." : $"Shturmap closed unexpectedly {fatal} times since you last answered.") +
              " Send a crash report? It helps to fix it.";
        ViewModel.CrashAsking = true;
        ViewModel.CrashSent = false;
        ViewModel.CrashDetails = "";
        Study.Ui("crash.ask", ("count", records.Count), ("fatal", fatal));
    }

    private void OnCrashSendClick(object sender, RoutedEventArgs e) => SendCrashes("send");

    private void OnCrashAlwaysClick(object sender, RoutedEventArgs e)
    {
        SetCrashMode(CrashMode.Always, "crash notice");
        SendCrashes("always");
    }

    private void OnCrashKeepClick(object sender, RoutedEventArgs e)
    {
        Reporter.Keep(_crashes);
        Study.Ui("crash.keep", ("count", _crashes.Count));
        ViewModel.CrashQuestion = "";
        ViewModel.CrashAsking = false;
        ViewModel.CrashDetails = "";
    }

    // Closed without an answer: the next start asks again.
    private void OnCrashLaterClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CrashAsking)
            Study.Ui("crash.later", ("count", _crashes.Count));
        ViewModel.CrashQuestion = "";
        ViewModel.CrashAsking = false;
        ViewModel.CrashSent = false;
        ViewModel.CrashDetails = "";
    }

    private void OnCrashDetailsClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CrashDetails = ViewModel.CrashDetails.Length > 0 ? "" : string.Join(Environment.NewLine + Environment.NewLine, _crashes.Select(ReportEnvelopes.Describe));
        if (ViewModel.CrashDetails.Length > 0)
            Study.Ui("crash.preview");
    }

    private void OnCrashNoteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CrashQuestion = "";
        ViewModel.CrashSent = false;
        OpenReport(ReportKind.Problem, $"About crash report {_crashSentId}: ", "crash");
    }

    private async void SendCrashes(string how)
    {
        ViewModel.CrashAsking = false;
        ViewModel.CrashDetails = "";
        ViewModel.CrashQuestion = "Sending…";
        var records = _crashes;
        var result = await Task.Run(() => Reporter.SendCrashesAsync(records));
        Study.Ui("crash.send", ("how", how), ("count", records.Count), ("status", result.Status));
        ViewModel.CrashQuestion = result.Message;
        ViewModel.CrashSent = result.Status == ReportStatus.Sent;
        _crashSentId = result.Id;
        // The answer goes after a while, unless the player adds a note.
        if (_crashFade is null)
        {
            _crashFade = DispatcherQueue.CreateTimer();
            _crashFade.Interval = TimeSpan.FromSeconds(30);
            _crashFade.IsRepeating = false;
            _crashFade.Tick += (_, _) =>
            {
                if (!ViewModel.CrashAsking)
                {
                    ViewModel.CrashQuestion = "";
                    ViewModel.CrashSent = false;
                }
            };
        }
        _crashFade.Stop();
        _crashFade.Start();
    }
}
