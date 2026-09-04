using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AorusControl.Core.Models;
using AorusControl.Core.Services;
using HidSharp;
using HidSharp.Reports;
using Microsoft.Win32;

[CompilerGenerated]
internal class Program
{
	private static void Main(string[] args)
	{
		string[] firmwareClasses = new string[3] { "GB_WMIACPI_Get", "GB_WMIACPI_Set", "CLEVO_GET" };
		bool readTelemetry = args.Any((string argument) => argument.Equals("--read-telemetry", StringComparison.OrdinalIgnoreCase));
		bool liveMonitor = args.Any((string argument) => argument.Equals("--monitor", StringComparison.OrdinalIgnoreCase));
		bool inspectKeyboard = args.Any((string argument) => argument.Equals("--inspect-keyboard", StringComparison.OrdinalIgnoreCase));
		bool readKeyboardState = args.Any((string argument) => argument.Equals("--read-keyboard-state", StringComparison.OrdinalIgnoreCase));
		bool queryKeyboardRgb = args.Any((string argument) => argument.Equals("--query-keyboard-rgb", StringComparison.OrdinalIgnoreCase));
		bool verifyKeyboardZoneWrite = args.Any((string argument) => argument.Equals("--verify-keyboard-zone-write", StringComparison.OrdinalIgnoreCase));
		bool setKeyboardGreen = args.Any((string argument) => argument.Equals("--set-keyboard-green", StringComparison.OrdinalIgnoreCase));
		bool monitorKeyboardBrightness = args.Any((string argument) => argument.Equals("--monitor-keyboard-brightness", StringComparison.OrdinalIgnoreCase));
		bool cycleKeyboardBrightness = args.Any((string argument) => argument.Equals("--cycle-keyboard-brightness", StringComparison.OrdinalIgnoreCase));
		bool testKeyboardBreathing = args.Any((string argument) => argument.Equals("--test-keyboard-breathing", StringComparison.OrdinalIgnoreCase));
		bool readKeyboardMatrix = args.Any((string argument) => argument.Equals("--read-keyboard-matrix", StringComparison.OrdinalIgnoreCase));
		bool probeKeyboardPictureMatrix = args.Any((string argument) => argument.Equals("--probe-keyboard-picture-matrix", StringComparison.OrdinalIgnoreCase));
		bool testKeyboardHostEffects = args.Any((string argument) => argument.Equals("--test-keyboard-host-effects", StringComparison.OrdinalIgnoreCase));
		bool interactiveHostEffectTest = args.Any((string argument) => argument.Equals("--interactive-host-effect-test", StringComparison.OrdinalIgnoreCase));
		bool testPictureMatrixWrite = args.Any((string argument) => argument.Equals("--test-picture-matrix-write", StringComparison.OrdinalIgnoreCase));
		bool isolateEffectSelection = args.Any((string argument) => argument.Equals("--isolate-effect-selection", StringComparison.OrdinalIgnoreCase));
		bool testEffectPalette = args.Any((string argument) => argument.Equals("--test-effect-palette", StringComparison.OrdinalIgnoreCase));
		bool sweepZoneBrightness = args.Any((string argument) => argument.Equals("--sweep-zone-brightness", StringComparison.OrdinalIgnoreCase));
		bool huntBrightnessSignal = args.Any((string argument) => argument.Equals("--hunt-brightness-signal", StringComparison.OrdinalIgnoreCase));
		bool monitorBrightnessEvents = args.Any((string argument) => argument.Equals("--monitor-brightness-events", StringComparison.OrdinalIgnoreCase));
		bool monitorPowerDraw = args.Any((string argument) => argument.Equals("--monitor-power-draw", StringComparison.OrdinalIgnoreCase));
		bool testBrightnessInteraction = args.Any((string argument) => argument.Equals("--test-brightness-interaction", StringComparison.OrdinalIgnoreCase));
		bool testBacklightLevel = args.Any((string argument) => argument.Equals("--test-backlight-level", StringComparison.OrdinalIgnoreCase));
		bool testKeyboardEffectsBatch1 = args.Any((string argument) => argument.Equals("--test-keyboard-effects-batch1", StringComparison.OrdinalIgnoreCase));
		bool interactiveKeyboardEffectTest = args.Any((string argument) => argument.Equals("--interactive-keyboard-effect-test", StringComparison.OrdinalIgnoreCase));
		bool setKeyboardSlowColorCycle = args.Any((string argument) => argument.Equals("--set-keyboard-slow-color-cycle", StringComparison.OrdinalIgnoreCase));
		bool setKeyboardOldDefaultPulse = args.Any((string argument) => argument.Equals("--set-keyboard-old-default-pulse", StringComparison.OrdinalIgnoreCase));
		bool inspectBattery = args.Any((string argument) => argument.Equals("--inspect-battery", StringComparison.OrdinalIgnoreCase));
		bool inspectThermalPower = args.Any((string argument) => argument.Equals("--inspect-thermal-power", StringComparison.OrdinalIgnoreCase));
		bool setFanNormal = args.Any((string argument) => argument.Equals("--set-fan-normal", StringComparison.OrdinalIgnoreCase));
		bool testFanQuiet = args.Any((string argument) => argument.Equals("--test-fan-quiet", StringComparison.OrdinalIgnoreCase));
		bool testFanGaming = args.Any((string argument) => argument.Equals("--test-fan-gaming", StringComparison.OrdinalIgnoreCase));
		bool testFanMaximum = args.Any((string argument) => argument.Equals("--test-fan-maximum", StringComparison.OrdinalIgnoreCase));
		bool testWindowsPowerModes = args.Any((string argument) => argument.Equals("--test-windows-power-modes", StringComparison.OrdinalIgnoreCase));
		bool testFanFixedScale = args.Any((string argument) => argument.Equals("--test-fan-fixed-scale", StringComparison.OrdinalIgnoreCase));
		bool testFanFixedLowScale = args.Any((string argument) => argument.Equals("--test-fan-fixed-low-scale", StringComparison.OrdinalIgnoreCase));
		bool testFanDynamic = args.Any((string argument) => argument.Equals("--test-fan-dynamic", StringComparison.OrdinalIgnoreCase));
		bool testFanCurveWrite = args.Any((string argument) => argument.Equals("--test-fan-curve-write", StringComparison.OrdinalIgnoreCase));
		int? requestedChargeLimit = ReadOptionalIntArgument("--set-charge-limit");
		bool setStandardChargeMode = args.Any((string argument) => argument.Equals("--set-standard-charge-mode", StringComparison.OrdinalIgnoreCase));
		if (requestedChargeLimit.HasValue | setStandardChargeMode)
		{
			RunBatteryChargeChange(requestedChargeLimit, setStandardChargeMode);
			return;
		}
		if (setFanNormal)
		{
			RunFanNormalChange();
			return;
		}
		if (testFanQuiet)
		{
			RunTemporaryFanProfileTest("quiet");
			return;
		}
		if (testFanGaming)
		{
			RunTemporaryFanProfileTest("gaming");
			return;
		}
		if (testFanMaximum)
		{
			RunTemporaryFanProfileTest("maximum");
			return;
		}
		if (testWindowsPowerModes)
		{
			RunWindowsPowerModeTest();
			return;
		}
		if (testFanFixedScale)
		{
			RunFanFixedScaleTest(lowRange: false);
			return;
		}
		if (testFanFixedLowScale)
		{
			RunFanFixedScaleTest(lowRange: true);
			return;
		}
		if (testFanDynamic)
		{
			RunTemporaryFanProfileTest("dynamic");
			return;
		}
		if (testFanCurveWrite)
		{
			RunFanCurveWriteTest();
			return;
		}
		if (setKeyboardSlowColorCycle)
		{
			RunKeyboardSlowColorCycle();
			return;
		}
		if (setKeyboardOldDefaultPulse)
		{
			RunKeyboardOldDefaultPulse();
			return;
		}
		if (testKeyboardBreathing)
		{
			RunKeyboardBreathingTest();
			return;
		}
		if (readKeyboardMatrix)
		{
			RunKeyboardMatrixRead();
			return;
		}
		if (probeKeyboardPictureMatrix)
		{
			RunKeyboardPictureMatrixProbe();
			return;
		}
		if (testKeyboardHostEffects)
		{
			RunKeyboardHostEffectTest();
			return;
		}
		if (interactiveHostEffectTest)
		{
			RunInteractiveHostEffectTest();
			return;
		}
		if (testPictureMatrixWrite)
		{
			RunPictureMatrixWriteTest();
			return;
		}
		if (isolateEffectSelection)
		{
			RunEffectSelectionIsolation();
			return;
		}
		if (testEffectPalette)
		{
			RunEffectPaletteTest();
			return;
		}
		if (sweepZoneBrightness)
		{
			RunZoneBrightnessSweep();
			return;
		}
		if (huntBrightnessSignal)
		{
			RunBrightnessSignalHunt();
			return;
		}
		if (monitorPowerDraw)
		{
			RunPowerDrawMonitor();
			return;
		}
		if (monitorBrightnessEvents)
		{
			RunBrightnessEventMonitor();
			return;
		}
		if (testBrightnessInteraction)
		{
			RunBrightnessInteractionTest();
			return;
		}
		if (testBacklightLevel)
		{
			RunBacklightLevelTest();
			return;
		}
		if (testKeyboardEffectsBatch1)
		{
			RunKeyboardEffectBatch();
			return;
		}
		if (interactiveKeyboardEffectTest)
		{
			RunInteractiveKeyboardEffectTest();
			return;
		}
		if (inspectBattery)
		{
			RunBatteryInspection();
			return;
		}
		if (inspectThermalPower)
		{
			RunThermalPowerInspection();
			return;
		}
		if (cycleKeyboardBrightness)
		{
			RunKeyboardBrightnessCycle();
			return;
		}
		if (monitorKeyboardBrightness)
		{
			RunKeyboardBrightnessMonitor();
			return;
		}
		if (setKeyboardGreen)
		{
			RunSetKeyboardGreen();
			return;
		}
		if (verifyKeyboardZoneWrite)
		{
			RunKeyboardZoneWriteVerification();
			return;
		}
		if (queryKeyboardRgb)
		{
			RunKeyboardRgbQuery();
			return;
		}
		if (readKeyboardState)
		{
			RunKeyboardFeatureRead();
			return;
		}
		if (inspectKeyboard)
		{
			RunKeyboardHidInspection();
			return;
		}
		if (liveMonitor)
		{
			RunLiveMonitor();
			return;
		}
		StringBuilder report = new StringBuilder();
		report.AppendLine("# AORUS read-only diagnostic report");
		report.AppendLine();
		StringBuilder stringBuilder = report;
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
		handler.AppendLiteral("- Created: ");
		handler.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
		stringBuilder2.AppendLine(ref handler);
		report.AppendLine(readTelemetry ? "- Mode: metadata plus DSDT-verified read-only telemetry whitelist" : "- Mode: metadata and read-only operating-system queries");
		report.AppendLine("- Firmware/EC write methods invoked: **no**");
		report.AppendLine();
		AddSection("Execution context");
		AddValue("Windows", Environment.OSVersion.VersionString);
		AddValue("64-bit process", Environment.Is64BitProcess ? "yes" : "no");
		AddValue("Administrator", IsAdministrator() ? "yes" : "no");
		AddValue(".NET runtime", Environment.Version.ToString());
		AddSection("Device identity (privacy-safe)");
		AppendFirstCimv("SELECT Manufacturer, Model FROM Win32_ComputerSystem", new string[2] { "Manufacturer", "Model" });
		AppendFirstCimv("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", new string[3] { "Manufacturer", "SMBIOSBIOSVersion", "ReleaseDate" });
		AppendFirstCimv("SELECT Manufacturer, Product, Version FROM Win32_BaseBoard", new string[3] { "Manufacturer", "Product", "Version" });
		AddSection("Windows ACPI WMI bridge");
		using (RegistryKey key = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\WmiAcpi"))
		{
			AddValue("WmiAcpi registry key", (key == null) ? "missing" : "present");
			AddValue("MofImagePath", key?.GetValue("MofImagePath")?.ToString() ?? "not configured");
		}
		report.AppendLine();
		report.AppendLine("### ACPI PNP0C14 devices");
		report.AppendLine();
		List<Dictionary<string, object>> acpiDevices = (from item in Query2("root\\cimv2", "SELECT Name, PNPDeviceID, Status FROM Win32_PnPEntity")
			where GetText(item, "PNPDeviceID").StartsWith("ACPI\\PNP0C14", StringComparison.OrdinalIgnoreCase)
			select item).ToList();
		if (acpiDevices.Count == 0)
		{
			report.AppendLine("- None found");
		}
		else
		{
			foreach (Dictionary<string, object> device in acpiDevices)
			{
				stringBuilder = report;
				StringBuilder stringBuilder3 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(10, 3, stringBuilder);
				handler.AppendLiteral("- `");
				handler.AppendFormatted(Escape(GetText(device, "PNPDeviceID")));
				handler.AppendLiteral("` — ");
				handler.AppendFormatted(Escape(GetText(device, "Name")));
				handler.AppendLiteral(" (");
				handler.AppendFormatted(Escape(GetText(device, "Status")));
				handler.AppendLiteral(")");
				stringBuilder3.AppendLine(ref handler);
			}
		}
		report.AppendLine();
		report.AppendLine("### Processed binary MOF resources for ACPI WMI devices");
		report.AppendLine();
		List<Dictionary<string, object>> mofResources = (from item in Query2("root\\WMI", "SELECT Name, MofProcessed FROM WMIBinaryMofResource")
			where GetText(item, "Name").Contains("PNP0C14", StringComparison.OrdinalIgnoreCase)
			select item).ToList();
		if (mofResources.Count == 0)
		{
			report.AppendLine("- None found");
		}
		else
		{
			foreach (Dictionary<string, object> resource in mofResources)
			{
				stringBuilder = report;
				StringBuilder stringBuilder4 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder);
				handler.AppendLiteral("- `");
				handler.AppendFormatted(Escape(GetText(resource, "Name")));
				handler.AppendLiteral("` — processed: ");
				handler.AppendFormatted(Escape(GetText(resource, "MofProcessed")));
				stringBuilder4.AppendLine(ref handler);
			}
		}
		AddSection("Gigabyte/Clevo WMI class metadata");
		string[] array = firmwareClasses;
		foreach (string className in array)
		{
			AppendClassMetadata("root\\WMI", className);
		}
		if (readTelemetry)
		{
			AppendWhitelistedTelemetry();
		}
		AddSection("Standard ACPI thermal zones");
		List<Dictionary<string, object>> thermalZones = Query2("root\\WMI", "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
		if (thermalZones.Count == 0)
		{
			report.AppendLine("- No readable thermal-zone data");
		}
		else
		{
			foreach (Dictionary<string, object> zone in thermalZones)
			{
				string raw = GetText(zone, "CurrentTemperature");
				string formatted = (uint.TryParse(raw, out var deciKelvin) ? $"{(double)deciKelvin / 10.0 - 273.15:F1} °C (raw {deciKelvin})" : raw);
				stringBuilder = report;
				StringBuilder stringBuilder5 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 2, stringBuilder);
				handler.AppendLiteral("- `");
				handler.AppendFormatted(Escape(GetText(zone, "InstanceName")));
				handler.AppendLiteral("`: ");
				handler.AppendFormatted(Escape(formatted));
				stringBuilder5.AppendLine(ref handler);
			}
		}
		report.AppendLine();
		report.AppendLine("## Interpretation");
		report.AppendLine();
		report.AppendLine("- A class being present means its metadata is registered; it does not prove that every method is safe on this firmware.");
		report.AppendLine(readTelemetry ? "- Only the explicit getter whitelist confirmed against this FB0F DSDT may be invoked; no setter is available in this mode." : "- This diagnostic intentionally does not invoke any class method, including methods whose names begin with `Get`.");
		report.AppendLine("- Serial numbers, UUIDs, user names, and network identifiers are intentionally excluded.");
		string text = Path.Combine(FindRepositoryRoot(), "research", "runs");
		Directory.CreateDirectory(text);
		string outputPath = Path.Combine(text, $"diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.md");
		File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		Console.WriteLine(report);
		Console.WriteLine("Report written to: " + outputPath);
		void AddSection(string title)
		{
			report.AppendLine();
			StringBuilder stringBuilder6 = report;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(3, 1, stringBuilder6);
			handler2.AppendLiteral("## ");
			handler2.AppendFormatted(title);
			stringBuilder6.AppendLine(ref handler2);
			report.AppendLine();
		}
		void AddValue(string name, string value)
		{
			StringBuilder stringBuilder6 = report;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(4, 2, stringBuilder6);
			handler2.AppendLiteral("- ");
			handler2.AppendFormatted(name);
			handler2.AppendLiteral(": ");
			handler2.AppendFormatted(Escape(value));
			stringBuilder6.AppendLine(ref handler2);
		}
		void AppendClassMetadata(string scopePath, string text2)
		{
			StringBuilder stringBuilder6 = report;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder6);
			handler2.AppendLiteral("### `");
			handler2.AppendFormatted(text2);
			handler2.AppendLiteral("`");
			stringBuilder7.AppendLine(ref handler2);
			report.AppendLine();
			try
			{
				ManagementClass managementClass = new ManagementClass(scopePath, text2, null);
				try
				{
					managementClass.Get();
					string[] methods = (from MethodData methodData in managementClass.Methods
						select methodData.Name).OrderBy<string, string>((string name) => name, StringComparer.OrdinalIgnoreCase).ToArray();
					report.AppendLine("- Status: present");
					stringBuilder6 = report;
					StringBuilder stringBuilder8 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder6);
					handler2.AppendLiteral("- Method count: ");
					handler2.AppendFormatted(methods.Length);
					stringBuilder8.AppendLine(ref handler2);
					string[] array2 = methods;
					foreach (string method in array2)
					{
						stringBuilder6 = report;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder6);
						handler2.AppendLiteral("  - `");
						handler2.AppendFormatted(method);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
					}
				}
				finally
				{
					((IDisposable)managementClass)?.Dispose();
				}
			}
			catch (ManagementException ex) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				ManagementStatus errorCode = ex.ErrorCode;
				return ((errorCode == ManagementStatus.NotFound || errorCode == ManagementStatus.InvalidClass) ? 1 : 0) != 0;
			}).Invoke())
			{
				report.AppendLine("- Status: not registered");
			}
			catch (Exception ex2)
			{
				stringBuilder6 = report;
				StringBuilder stringBuilder10 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder6);
				handler2.AppendLiteral("- Status: unavailable (");
				handler2.AppendFormatted(Escape(ex2.Message));
				handler2.AppendLiteral(")");
				stringBuilder10.AppendLine(ref handler2);
			}
			report.AppendLine();
		}
		static void AppendCommandOutput(StringBuilder target, string label, string executable, string arguments)
		{
			try
			{
				using Process process = new Process
				{
					StartInfo = new ProcessStartInfo(executable, arguments)
					{
						UseShellExecute = false,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						CreateNoWindow = true
					}
				};
				process.Start();
				string stdout = process.StandardOutput.ReadToEnd().Trim();
				string stderr = process.StandardError.ReadToEnd().Trim();
				if (!process.WaitForExit(5000))
				{
					process.Kill(entireProcessTree: true);
					StringBuilder stringBuilder6 = target;
					StringBuilder stringBuilder7 = stringBuilder6;
					StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
					handler2.AppendLiteral("- ");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral(": timed out");
					stringBuilder7.AppendLine(ref handler2);
				}
				else
				{
					StringBuilder stringBuilder6 = target;
					StringBuilder stringBuilder8 = stringBuilder6;
					StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 2, stringBuilder6);
					handler2.AppendLiteral("- ");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral(": exit `");
					handler2.AppendFormatted(process.ExitCode);
					handler2.AppendLiteral("`");
					stringBuilder8.AppendLine(ref handler2);
					target.AppendLine("```text");
					target.AppendLine(string.IsNullOrWhiteSpace(stdout) ? "(no output)" : stdout);
					if (!string.IsNullOrWhiteSpace(stderr))
					{
						target.AppendLine("stderr: " + stderr);
					}
					target.AppendLine("```");
				}
			}
			catch (Exception ex)
			{
				StringBuilder stringBuilder6 = target;
				StringBuilder stringBuilder9 = stringBuilder6;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder6);
				handler2.AppendLiteral("- ");
				handler2.AppendFormatted(label);
				handler2.AppendLiteral(": unavailable (");
				handler2.AppendFormatted(Escape(ex.Message));
				handler2.AppendLiteral(")");
				stringBuilder9.AppendLine(ref handler2);
			}
		}
		static void AppendFanState(StringBuilder target, string label, FanControlState state)
		{
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(86, 7, target);
			handler2.AppendLiteral("- ");
			handler2.AppendFormatted(label);
			handler2.AppendLiteral(": fixed `");
			handler2.AppendFormatted(state.FixedStatusRaw);
			handler2.AppendLiteral("`, step `");
			handler2.AppendFormatted(state.StepStatusRaw);
			handler2.AppendLiteral("`, ");
			handler2.AppendLiteral("auto `");
			handler2.AppendFormatted(state.AutoStatusRaw);
			handler2.AppendLiteral("`, thermal `");
			handler2.AppendFormatted(state.NvidiaThermalTargetRaw);
			handler2.AppendLiteral("`, ");
			handler2.AppendLiteral("stored fixed speed `");
			handler2.AppendFormatted(state.FixedSpeedRaw);
			handler2.AppendLiteral("`, current GPU duty `");
			handler2.AppendFormatted(state.GpuDutyRaw);
			handler2.AppendLiteral("`");
			target.AppendLine(ref handler2);
		}
		void AppendFirstCimv(string query, string[] properties)
		{
			Dictionary<string, object> item = Query2("root\\cimv2", query).FirstOrDefault();
			if (item == null)
			{
				report.AppendLine("- Query returned no instance");
			}
			else
			{
				foreach (string property in properties)
				{
					string value = GetText(item, property);
					if (property.Equals("ReleaseDate", StringComparison.OrdinalIgnoreCase))
					{
						value = FormatWmiDate(value);
					}
					AddValue(property, value);
				}
			}
		}
		static void AppendQueryRows(StringBuilder target, string scope, string query, string[] properties)
		{
			try
			{
				List<Dictionary<string, object>> rows = Query2(scope, query);
				if (rows.Count == 0)
				{
					StringBuilder stringBuilder6 = target;
					StringBuilder stringBuilder7 = stringBuilder6;
					StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
					handler2.AppendLiteral("- `");
					handler2.AppendFormatted(Escape(query));
					handler2.AppendLiteral("`: no rows");
					stringBuilder7.AppendLine(ref handler2);
					return;
				}
				foreach (Dictionary<string, object> row in rows)
				{
					target.AppendLine("- " + string.Join("; ", properties.Select((string property) => property + "=`" + Escape(GetText(row, property)) + "`")));
				}
			}
			catch (Exception ex)
			{
				StringBuilder stringBuilder6 = target;
				StringBuilder stringBuilder8 = stringBuilder6;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder6);
				handler2.AppendLiteral("- Query failed: ");
				handler2.AppendFormatted(Escape(ex.Message));
				stringBuilder8.AppendLine(ref handler2);
			}
		}
		static void AppendRegistryValue(StringBuilder target, string subKey, string valueName)
		{
			try
			{
				using RegistryKey key2 = Registry.LocalMachine.OpenSubKey(subKey, writable: false);
				object value = key2?.GetValue(valueName);
				StringBuilder stringBuilder6 = target;
				StringBuilder stringBuilder7 = stringBuilder6;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder6);
				handler2.AppendLiteral("- `");
				handler2.AppendFormatted(valueName);
				handler2.AppendLiteral("`: `");
				handler2.AppendFormatted(Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "not set"));
				handler2.AppendLiteral("`");
				stringBuilder7.AppendLine(ref handler2);
			}
			catch (Exception ex)
			{
				StringBuilder stringBuilder6 = target;
				StringBuilder stringBuilder8 = stringBuilder6;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 2, stringBuilder6);
				handler2.AppendLiteral("- `");
				handler2.AppendFormatted(valueName);
				handler2.AppendLiteral("`: unavailable (");
				handler2.AppendFormatted(Escape(ex.Message));
				handler2.AppendLiteral(")");
				stringBuilder8.AppendLine(ref handler2);
			}
		}
		void AppendWhitelistedTelemetry()
		{
			AddSection("DSDT-verified read-only telemetry");
			string model = GetFirstValue("root\\cimv2", "SELECT Model FROM Win32_ComputerSystem", "Model");
			string bios = GetFirstValue("root\\cimv2", "SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
			if (!model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) || !bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase))
			{
				StringBuilder stringBuilder6 = report;
				StringBuilder stringBuilder7 = stringBuilder6;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(88, 2, stringBuilder6);
				handler2.AppendLiteral("- Refused: telemetry whitelist is approved only for `AORUS 5 SE / FB0F`; ");
				handler2.AppendLiteral("detected `");
				handler2.AppendFormatted(Escape(model));
				handler2.AppendLiteral(" / ");
				handler2.AppendFormatted(Escape(bios));
				handler2.AppendLiteral("`.");
				stringBuilder7.AppendLine(ref handler2);
				return;
			}
			string[] approvedMethods = new string[14]
			{
				"getCpuTemp", "getGpuTemp1", "getGpuTemp2", "getRpm1", "getRpm2", "GetCPUFanDuty", "GetGPUFanDuty", "GetChargePolicy", "GetChargeStop", "GetFixedFanStatus",
				"GetFixedFanSpeed", "GetFanAdjustStatus", "GetAutoFanStatus", "GetFanSpeed"
			};
			try
			{
				ManagementClass getClass = new ManagementClass("root\\WMI", "GB_WMIACPI_Get", null);
				try
				{
					getClass.Get();
					using ManagementObjectCollection instances = getClass.GetInstances();
					ManagementObject instance = instances.Cast<ManagementObject>().FirstOrDefault();
					try
					{
						if (instance == null)
						{
							report.AppendLine("- `GB_WMIACPI_Get` is registered but has no instance.");
						}
						else
						{
							AddValue("Instance", instance.Path.Path);
							HashSet<string> availableMethods = (from MethodData method in getClass.Methods
								select method.Name).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
							string[] array2 = approvedMethods;
							foreach (string methodName in array2)
							{
								if (!availableMethods.Contains(methodName))
								{
									StringBuilder stringBuilder6 = report;
									StringBuilder stringBuilder8 = stringBuilder6;
									StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(34, 1, stringBuilder6);
									handler2.AppendLiteral("- `");
									handler2.AppendFormatted(methodName);
									handler2.AppendLiteral("`: not exposed by installed MOF");
									stringBuilder8.AppendLine(ref handler2);
								}
								else
								{
									try
									{
										InvokeMethodOptions options = new InvokeMethodOptions
										{
											Timeout = TimeSpan.FromSeconds(2L)
										};
										ManagementBaseObject output = instance.InvokeMethod(methodName, null, options);
										try
										{
											string values = string.Join(", ", from PropertyData property in output.Properties
												select property.Name + "=" + Convert.ToString(property.Value, CultureInfo.InvariantCulture));
											if ((methodName.Equals("getRpm1", StringComparison.OrdinalIgnoreCase) || methodName.Equals("getRpm2", StringComparison.OrdinalIgnoreCase)) && output["Data"] != null)
											{
												ushort rawRpm = Convert.ToUInt16(output["Data"], CultureInfo.InvariantCulture);
												ushort decodedRpm = (ushort)((rawRpm >> 8) | (rawRpm << 8));
												values += $" (byte-swapped: {decodedRpm} RPM)";
											}
											StringBuilder stringBuilder6 = report;
											StringBuilder stringBuilder9 = stringBuilder6;
											StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(6, 2, stringBuilder6);
											handler2.AppendLiteral("- `");
											handler2.AppendFormatted(methodName);
											handler2.AppendLiteral("`: ");
											handler2.AppendFormatted(Escape(values));
											stringBuilder9.AppendLine(ref handler2);
										}
										finally
										{
											((IDisposable)output)?.Dispose();
										}
									}
									catch (Exception ex)
									{
										StringBuilder stringBuilder6 = report;
										StringBuilder stringBuilder10 = stringBuilder6;
										StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 2, stringBuilder6);
										handler2.AppendLiteral("- `");
										handler2.AppendFormatted(methodName);
										handler2.AppendLiteral("`: error (");
										handler2.AppendFormatted(Escape(ex.Message));
										handler2.AppendLiteral(")");
										stringBuilder10.AppendLine(ref handler2);
									}
								}
							}
						}
					}
					finally
					{
						((IDisposable)instance)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)getClass)?.Dispose();
				}
			}
			catch (ManagementException ex2) when (((Func<bool>)delegate
			{
				// Could not convert BlockContainer to single expression
				ManagementStatus errorCode = ex2.ErrorCode;
				return ((errorCode == ManagementStatus.NotFound || errorCode == ManagementStatus.InvalidClass) ? 1 : 0) != 0;
			}).Invoke())
			{
				report.AppendLine("- `GB_WMIACPI_Get` is not registered. Install the signed MOF provider and reboot first.");
			}
			catch (Exception ex3)
			{
				StringBuilder stringBuilder6 = report;
				StringBuilder stringBuilder11 = stringBuilder6;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(25, 1, stringBuilder6);
				handler2.AppendLiteral("- Telemetry unavailable: ");
				handler2.AppendFormatted(Escape(ex3.Message));
				stringBuilder11.AppendLine(ref handler2);
			}
		}
		static byte CalculateGigabyteChecksum(ReadOnlySpan<byte> packet)
		{
			int sum = 0;
			for (int index = 1; index <= 7; index++)
			{
				sum += packet[index];
			}
			return (byte)(255 - sum);
		}
		static PowerSample CapturePowerSample(string integratedAdapterLuid)
		{
			uint discharge = 0u;
			try
			{
				ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\wmi", "SELECT DischargeRate FROM BatteryStatus");
				try
				{
					using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = searcher.Get().GetEnumerator();
					if (managementObjectEnumerator.MoveNext())
					{
						ManagementBaseObject item = managementObjectEnumerator.Current;
						ManagementBaseObject managementBaseObject = item;
						try
						{
							discharge = Convert.ToUInt32(item["DischargeRate"] ?? ((object)0u), CultureInfo.InvariantCulture);
						}
						finally
						{
							((IDisposable)managementBaseObject)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)searcher)?.Dispose();
				}
			}
			catch (Exception)
			{
			}
			double integrated = 0.0;
			double discrete = 0.0;
			try
			{
				ManagementObjectSearcher searcher2 = new ManagementObjectSearcher("root\\cimv2", "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");
				try
				{
					foreach (ManagementBaseObject item2 in searcher2.Get())
					{
						ManagementBaseObject managementBaseObject = item2;
						try
						{
							string? obj = item2["Name"]?.ToString() ?? string.Empty;
							double value = Convert.ToDouble(item2["UtilizationPercentage"] ?? ((object)0.0), CultureInfo.InvariantCulture);
							if (obj.Contains(integratedAdapterLuid, StringComparison.OrdinalIgnoreCase))
							{
								integrated += value;
							}
							else
							{
								discrete += value;
							}
						}
						finally
						{
							((IDisposable)managementBaseObject)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)searcher2)?.Dispose();
				}
			}
			catch (Exception)
			{
			}
			double cpuTotal = 0.0;
			List<(string, double)> processes = new List<(string, double)>();
			try
			{
				ManagementObjectSearcher searcher3 = new ManagementObjectSearcher("root\\cimv2", "SELECT Name, PercentProcessorTime FROM Win32_PerfFormattedData_PerfProc_Process");
				try
				{
					foreach (ManagementBaseObject item3 in searcher3.Get())
					{
						ManagementBaseObject managementBaseObject = item3;
						try
						{
							string name = item3["Name"]?.ToString() ?? string.Empty;
							double value2 = Convert.ToDouble(item3["PercentProcessorTime"] ?? ((object)0.0), CultureInfo.InvariantCulture);
							if (name.Equals("_Total", StringComparison.OrdinalIgnoreCase))
							{
								cpuTotal = value2 / (double)Math.Max(1, Environment.ProcessorCount);
							}
							else if (!name.Equals("Idle", StringComparison.OrdinalIgnoreCase) && value2 > 0.0)
							{
								processes.Add((name, value2));
							}
						}
						finally
						{
							((IDisposable)managementBaseObject)?.Dispose();
						}
					}
				}
				finally
				{
					((IDisposable)searcher3)?.Dispose();
				}
			}
			catch (Exception)
			{
			}
			string top = ((processes.Count == 0) ? "-" : string.Join(", ", from entry in processes.OrderByDescending<(string, double), double>(((string Name, double Cpu) entry) => entry.Cpu).Take(3)
				select $"{entry.Name} {entry.Cpu:F0} %"));
			return new PowerSample(DateTimeOffset.Now, discharge, cpuTotal, integrated, discrete, top);
		}
		static string DescribeOverlay(Guid value)
		{
			if (!(value == WindowsPowerOverlayController.BalancedGuid))
			{
				if (!(value == WindowsPowerOverlayController.BestEfficiencyGuid))
				{
					if (!(value == WindowsPowerOverlayController.BestPerformanceGuid))
					{
						return "unknown";
					}
					return "Best performance";
				}
				return "Best efficiency";
			}
			return "Balanced";
		}
		static string Escape(string value)
		{
			return value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);
		}
		static string FindRepositoryRoot()
		{
			string[] array2 = new string[2]
			{
				AppContext.BaseDirectory,
				Environment.CurrentDirectory
			};
			for (int i = 0; i < array2.Length; i++)
			{
				for (DirectoryInfo directory = new DirectoryInfo(array2[i]); directory != null; directory = directory.Parent)
				{
					if (File.Exists(Path.Combine(directory.FullName, "AorusControl.slnx")))
					{
						return directory.FullName;
					}
				}
			}
			return Environment.CurrentDirectory;
		}
		static string FormatManagementValues(ManagementBaseObject values)
		{
			return string.Join(", ", from PropertyData property in values.Properties
				select property.Name + "=" + Convert.ToString(property.Value, CultureInfo.InvariantCulture));
		}
		static string FormatMethodSignature(MethodData method)
		{
			string inputs = string.Join(", ", method.InParameters?.Properties.Cast<PropertyData>().Select((PropertyData property) => $"{property.Name}:{property.Type}") ?? Array.Empty<string>());
			string outputs = string.Join(", ", method.OutParameters?.Properties.Cast<PropertyData>().Select((PropertyData property) => $"{property.Name}:{property.Type}") ?? Array.Empty<string>());
			return $"in [{inputs}], out [{outputs}]";
		}
		static string FormatWmiDate(string value)
		{
			try
			{
				return ManagementDateTimeConverter.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			}
			catch
			{
				return value;
			}
		}
		static string GetFirstValue(string scopePath, string query, string property)
		{
			Dictionary<string, object> item = Query2(scopePath, query).FirstOrDefault();
			if (item != null)
			{
				return GetText(item, property);
			}
			return string.Empty;
		}
		static string GetInterfaceLabel(string devicePath)
		{
			Match match = Regex.Match(devicePath, "&mi_(?<interface>[0-9a-f]{2})(?:&col(?<collection>[0-9a-f]{2}))?", RegexOptions.IgnoreCase);
			if (!match.Success)
			{
				return "HID collection";
			}
			string label = "MI_" + match.Groups["interface"].Value.ToUpperInvariant();
			if (match.Groups["collection"].Success)
			{
				label = label + " / COL_" + match.Groups["collection"].Value.ToUpperInvariant();
			}
			return label;
		}
		static string GetText(IReadOnlyDictionary<string, object?> item, string property)
		{
			if (item.TryGetValue(property, out object value))
			{
				return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
			}
			return string.Empty;
		}
		static (byte r, byte g, byte b) HostHueToRgb(double hue)
		{
			double num2 = hue * 6.0;
			int index = (int)Math.Floor(num2) % 6;
			byte rising = (byte)Math.Round((num2 - Math.Floor(num2)) * 255.0);
			byte falling = (byte)(255 - rising);
			return index switch
			{
				0 => (r: byte.MaxValue, g: rising, b: 0), 
				1 => (r: falling, g: byte.MaxValue, b: 0), 
				2 => (r: 0, g: byte.MaxValue, b: rising), 
				3 => (r: 0, g: falling, b: byte.MaxValue), 
				4 => (r: rising, g: 0, b: byte.MaxValue), 
				_ => (r: byte.MaxValue, g: 0, b: falling), 
			};
		}
		static (byte r, byte g, byte b) HueToRgb(double hue)
		{
			double num2 = hue * 6.0;
			int index = (int)Math.Floor(num2) % 6;
			byte rising = (byte)Math.Round((num2 - Math.Floor(num2)) * 255.0);
			byte falling = (byte)(255 - rising);
			return index switch
			{
				0 => (r: byte.MaxValue, g: rising, b: 0), 
				1 => (r: falling, g: byte.MaxValue, b: 0), 
				2 => (r: 0, g: byte.MaxValue, b: rising), 
				3 => (r: 0, g: falling, b: byte.MaxValue), 
				4 => (r: rising, g: 0, b: byte.MaxValue), 
				_ => (r: byte.MaxValue, g: 0, b: falling), 
			};
		}
		static byte InvokeByteGetter(ManagementObject instance, string methodName)
		{
			InvokeMethodOptions options = new InvokeMethodOptions
			{
				Timeout = TimeSpan.FromSeconds(2L)
			};
			ManagementBaseObject output = instance.InvokeMethod(methodName, null, options);
			try
			{
				return Convert.ToByte(output["Data"] ?? throw new InvalidOperationException(methodName + " returned no Data value."), CultureInfo.InvariantCulture);
			}
			finally
			{
				((IDisposable)output)?.Dispose();
			}
		}
		static byte InvokeGetter(ManagementObject instance)
		{
			InvokeMethodOptions options = new InvokeMethodOptions
			{
				Timeout = TimeSpan.FromSeconds(2L)
			};
			ManagementBaseObject output = instance.InvokeMethod("GetKeyBoardBackLight", null, options);
			try
			{
				return Convert.ToByte(output["Data"], CultureInfo.InvariantCulture);
			}
			finally
			{
				((IDisposable)output)?.Dispose();
			}
		}
		static void InvokeSetter(ManagementObject instance, byte value)
		{
			ManagementBaseObject input = instance.GetMethodParameters("SetKeyBoardBackLight");
			try
			{
				input["Data"] = value;
				InvokeMethodOptions options = new InvokeMethodOptions
				{
					Timeout = TimeSpan.FromSeconds(2L)
				};
				ManagementBaseObject output = instance.InvokeMethod("SetKeyBoardBackLight", input, options);
				try
				{
				}
				finally
				{
					((IDisposable)output)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)input)?.Dispose();
			}
		}
		static ushort InvokeUInt16GetterUnchecked(ManagementObject instance, ManagementClass getClass, string methodName)
		{
			ManagementBaseObject input = getClass.GetMethodParameters(methodName);
			try
			{
				ManagementBaseObject output = instance.InvokeMethod(methodName, input, new InvokeMethodOptions
				{
					Timeout = TimeSpan.FromSeconds(2L)
				});
				try
				{
					return Convert.ToUInt16(output["Data"] ?? throw new InvalidOperationException(methodName + " returned no Data value."), CultureInfo.InvariantCulture);
				}
				finally
				{
					((IDisposable)output)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)input)?.Dispose();
			}
		}
		static bool IsAdministrator()
		{
			using WindowsIdentity identity = WindowsIdentity.GetCurrent();
			return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
		}
		static ManagementObject OpenInstance(string text2, string requiredMethod)
		{
			ManagementClass managementClass = new ManagementClass("root\\WMI", text2, null);
			try
			{
				managementClass.Get();
				if (managementClass.Methods.Cast<MethodData>().FirstOrDefault((MethodData candidate) => candidate.Name.Equals(requiredMethod, StringComparison.OrdinalIgnoreCase)) == null)
				{
					throw new InvalidOperationException(text2 + " exposes no method " + requiredMethod + ".");
				}
				using ManagementObjectCollection instances = managementClass.GetInstances();
				return instances.Cast<ManagementObject>().FirstOrDefault() ?? throw new InvalidOperationException(text2 + " has no live device instance.");
			}
			finally
			{
				((IDisposable)managementClass)?.Dispose();
			}
		}
		static byte[] Query(HidStream stream, byte command, byte selector)
		{
			byte[] query = new byte[9];
			query[1] = command;
			query[2] = selector;
			query[8] = CalculateGigabyteChecksum(query);
			stream.SetFeature(query);
			Thread.Sleep(65);
			byte[] response = new byte[9];
			stream.GetFeature(response);
			return response;
		}
		static List<Dictionary<string, object?>> Query2(string scopePath, string query)
		{
			List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();
			try
			{
				ManagementObjectSearcher searcher = new ManagementObjectSearcher(scopePath, query);
				try
				{
					using ManagementObjectCollection objects = searcher.Get();
					foreach (ManagementObject item in objects)
					{
						Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
						foreach (PropertyData property in item.Properties)
						{
							values[property.Name] = property.Value;
						}
						results.Add(values);
						item.Dispose();
					}
				}
				finally
				{
					((IDisposable)searcher)?.Dispose();
				}
			}
			catch (ManagementException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}
			return results;
		}
		static byte[] QueryZone(HidStream stream, byte b)
		{
			byte[] query = new byte[9];
			query[1] = 136;
			query[2] = b;
			query[8] = CalculateGigabyteChecksum(query);
			stream.SetFeature(query);
			Thread.Sleep(10);
			byte[] response = new byte[9];
			stream.GetFeature(response);
			return response;
		}
		static double Ramp(double elapsed, double periodSeconds)
		{
			return (1.0 - Math.Cos(elapsed * 2.0 * Math.PI / periodSeconds)) / 2.0;
		}
		byte[] ReadByteListArgument(string name, byte[] fallback)
		{
			for (int index = 0; index < args.Length - 1; index++)
			{
				if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					byte[] parsed = (from part in args[index + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
						select (!byte.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)) ? ((byte?)null) : new byte?(result) into value
						where value.HasValue
						select value.Value).ToArray();
					if (parsed.Length != 0)
					{
						return parsed;
					}
				}
			}
			return fallback;
		}
		int? ReadOptionalIntArgument(string name)
		{
			for (int index = 0; index < args.Length - 1; index++)
			{
				if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
				{
					return value;
				}
			}
			return null;
		}
		int ReadPositiveIntArgument(string name, int fallback)
		{
			for (int index = 0; index < args.Length - 1; index++)
			{
				if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0)
				{
					return value;
				}
			}
			return fallback;
		}
		static string ReadSingleWmiValue(string query, string property)
		{
			ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
			try
			{
				using (ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = searcher.Get().GetEnumerator())
				{
					if (managementObjectEnumerator.MoveNext())
					{
						ManagementBaseObject item = managementObjectEnumerator.Current;
						ManagementBaseObject managementBaseObject = item;
						try
						{
							return item[property]?.ToString()?.Trim() ?? string.Empty;
						}
						finally
						{
							((IDisposable)managementBaseObject)?.Dispose();
						}
					}
				}
				return string.Empty;
			}
			finally
			{
				((IDisposable)searcher)?.Dispose();
			}
		}
		void RunBacklightLevelTest()
		{
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-backlight-write", StringComparison.OrdinalIgnoreCase));
			byte[] requestedLevels = ReadByteListArgument("--levels", new byte[4] { 0, 24, 32, 50 });
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS keyboard backlight level test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Interface: `GB_WMIACPI_Get.GetKeyBoardBackLight` and `GB_WMIACPI_Set.SetKeyBoardBackLight`, WMI method ID `0xF6`");
			report2.AppendLine("- FB0F DSDT target: EC field `KBLL` at offset `0xD7`");
			report2.AppendLine("- Gates: exact model and BIOS, administrator rights, and `--confirm-backlight-write`");
			report2.AppendLine("- Battery, fan, charge, key matrix, macros, HID, BIOS, and firmware: **not touched**");
			report2.AppendLine("- Rollback: the original value is read first and rewritten plus verified in `finally`");
			report2.AppendLine();
			if (!confirmed)
			{
				Console.Error.WriteLine("Dieser Test ruft erstmals den ACPI-Setter SetKeyBoardBackLight auf.");
				Console.Error.WriteLine("Er verlangt zusaetzlich --confirm-backlight-write.");
				report2.AppendLine("- Refused before any firmware access: `--confirm-backlight-write` was not supplied.");
				WriteReport();
				Environment.ExitCode = 2;
			}
			else
			{
				Console.OutputEncoding = Encoding.UTF8;
				Console.WriteLine("AORUS 5 SE - Test der Tastatur-Hintergrundbeleuchtungsstufe");
				Console.WriteLine();
				try
				{
					string manufacturer = ReadSingleWmiValue("SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer");
					string model = ReadSingleWmiValue("SELECT Model FROM Win32_ComputerSystem", "Model");
					string bios = ReadSingleWmiValue("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
					bool deviceApproved = manufacturer.Equals("GIGABYTE", StringComparison.OrdinalIgnoreCase) && model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) && bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase);
					report2.AppendLine("## Gates");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder8 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 3, stringBuilder6);
					handler2.AppendLiteral("- Detected device: `");
					handler2.AppendFormatted(Escape(manufacturer));
					handler2.AppendLiteral("` / `");
					handler2.AppendFormatted(Escape(model));
					handler2.AppendLiteral("` / `");
					handler2.AppendFormatted(Escape(bios));
					handler2.AppendLiteral("`");
					stringBuilder8.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder6);
					handler2.AppendLiteral("- Exact approved device: **");
					handler2.AppendFormatted(deviceApproved ? "yes" : "no");
					handler2.AppendLiteral("**");
					stringBuilder9.AppendLine(ref handler2);
					if (!deviceApproved)
					{
						Console.Error.WriteLine("Nicht freigegebenes Geraet; es wurde nichts geschrieben.");
						report2.AppendLine("- Refused: device is not on the approved list.");
						WriteReport();
						Environment.ExitCode = 3;
						return;
					}
					using WindowsIdentity identity = WindowsIdentity.GetCurrent();
					bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
					handler2.AppendLiteral("- Administrator: **");
					handler2.AppendFormatted(elevated ? "yes" : "no");
					handler2.AppendLiteral("**");
					stringBuilder10.AppendLine(ref handler2);
					if (!elevated)
					{
						Console.Error.WriteLine("Administratorrechte sind erforderlich; es wurde nichts geschrieben.");
						report2.AppendLine("- Refused: administrator rights are required for ACPI writes.");
						WriteReport();
						Environment.ExitCode = 3;
						return;
					}
					ManagementObject getter = OpenInstance("GB_WMIACPI_Get", "GetKeyBoardBackLight");
					try
					{
						ManagementObject setter = OpenInstance("GB_WMIACPI_Set", "SetKeyBoardBackLight");
						try
						{
							byte original = InvokeGetter(getter);
							report2.AppendLine();
							report2.AppendLine("## Original value");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(45, 1, stringBuilder6);
							handler2.AppendLiteral("- `GetKeyBoardBackLight` before any write: `");
							handler2.AppendFormatted(original);
							handler2.AppendLiteral("`");
							stringBuilder11.AppendLine(ref handler2);
							Console.WriteLine($"Aktueller gespeicherter Wert: {original}");
							Console.WriteLine();
							Console.WriteLine("Es werden nun die Werte 0 bis 4 geschrieben. Beschreibe nach jedem Schritt,");
							Console.WriteLine("was du an der Tastaturbeleuchtung siehst. /stop beendet vorzeitig.");
							Console.WriteLine();
							report2.AppendLine();
							report2.AppendLine("## Levels");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder12 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
							handler2.AppendLiteral("- Levels written: `");
							handler2.AppendFormatted(string.Join("`, `", requestedLevels));
							handler2.AppendLiteral("`");
							stringBuilder12.AppendLine(ref handler2);
							report2.AppendLine();
							report2.AppendLine("| Written | Readback | Owner observation |");
							report2.AppendLine("|---|---|---|");
							try
							{
								byte[] array2 = requestedLevels;
								foreach (byte level in array2)
								{
									InvokeSetter(setter, level);
									Thread.Sleep(400);
									byte readback = InvokeGetter(getter);
									Console.WriteLine($"Wert {level} geschrieben, zurueckgelesen als {readback}.");
									Console.Write("  Beobachtung: ");
									string observation = Console.ReadLine()?.Trim() ?? string.Empty;
									stringBuilder6 = report2;
									StringBuilder stringBuilder13 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 3, stringBuilder6);
									handler2.AppendLiteral("| `");
									handler2.AppendFormatted(level);
									handler2.AppendLiteral("` | `");
									handler2.AppendFormatted(readback);
									handler2.AppendLiteral("` | ");
									handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung)" : observation));
									handler2.AppendLiteral(" |");
									stringBuilder13.AppendLine(ref handler2);
									if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
									{
										Console.WriteLine("Test wird beendet.");
										break;
									}
								}
							}
							finally
							{
								report2.AppendLine();
								report2.AppendLine("## Rollback");
								report2.AppendLine();
								try
								{
									InvokeSetter(setter, original);
									Thread.Sleep(400);
									byte restored = InvokeGetter(getter);
									stringBuilder6 = report2;
									StringBuilder stringBuilder14 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(61, 3, stringBuilder6);
									handler2.AppendLiteral("- Original value `");
									handler2.AppendFormatted(original);
									handler2.AppendLiteral("` rewritten, readback `");
									handler2.AppendFormatted(restored);
									handler2.AppendLiteral("`, exact match: **");
									handler2.AppendFormatted((restored == original) ? "yes" : "no");
									handler2.AppendLiteral("**");
									stringBuilder14.AppendLine(ref handler2);
									if (restored != original)
									{
										Environment.ExitCode = 6;
									}
									Console.WriteLine($"Der Ausgangswert {original} wurde wiederhergestellt.");
								}
								catch (Exception ex)
								{
									stringBuilder6 = report2;
									StringBuilder stringBuilder15 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
									handler2.AppendLiteral("- RESTORE ERROR: ");
									handler2.AppendFormatted(Escape(ex.Message));
									stringBuilder15.AppendLine(ref handler2);
									Console.Error.WriteLine("Wiederherstellungsfehler: " + ex.Message);
									Environment.ExitCode = 7;
								}
							}
						}
						finally
						{
							((IDisposable)setter)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)getter)?.Dispose();
					}
				}
				catch (Exception ex2)
				{
					Console.Error.WriteLine("Testfehler: " + ex2.Message);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder16 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex2.Message));
					stringBuilder16.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteReport();
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-backlight-level-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		void RunBatteryChargeChange(int? limitPercent, bool standardMode)
		{
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-battery-write", StringComparison.OrdinalIgnoreCase));
			StringBuilder changeReport = new StringBuilder();
			changeReport.AppendLine("# AORUS battery charge-limit change");
			changeReport.AppendLine();
			StringBuilder stringBuilder6 = changeReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = changeReport;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder6);
			handler2.AppendLiteral("- Requested state: ");
			handler2.AppendFormatted(standardMode ? "Standard mode (raw 0 + 100)" : $"Custom {limitPercent}% (raw 4 + {limitPercent})");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = changeReport;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
			handler2.AppendLiteral("- Explicit write confirmation present: ");
			handler2.AppendFormatted(confirmed ? "yes" : "no");
			stringBuilder9.AppendLine(ref handler2);
			changeReport.AppendLine();
			if (limitPercent.HasValue & standardMode)
			{
				changeReport.AppendLine("- Refused: custom limit and Standard mode cannot be requested together.");
				WriteBatteryChangeReport(changeReport);
				Environment.ExitCode = 2;
			}
			else if (!confirmed)
			{
				changeReport.AppendLine("- Refused before opening the setter: `--confirm-battery-write` is required.");
				changeReport.AppendLine("- Firmware/EC write methods invoked: **no**");
				WriteBatteryChangeReport(changeReport);
				Environment.ExitCode = 2;
			}
			else
			{
				bool flag;
				switch (limitPercent)
				{
				default:
					flag = true;
					break;
				case null:
				case 60:
				case 61:
				case 62:
				case 63:
				case 64:
				case 65:
				case 66:
				case 67:
				case 68:
				case 69:
				case 70:
				case 71:
				case 72:
				case 73:
				case 74:
				case 75:
				case 76:
				case 77:
				case 78:
				case 79:
				case 80:
				case 81:
				case 82:
				case 83:
				case 84:
				case 85:
				case 86:
				case 87:
				case 88:
				case 89:
				case 90:
				case 91:
				case 92:
				case 93:
				case 94:
				case 95:
				case 96:
				case 97:
				case 98:
				case 99:
				case 100:
					flag = false;
					break;
				}
				if (flag)
				{
					changeReport.AppendLine("- Refused before opening the setter: custom limit must be 60–100%.");
					changeReport.AppendLine("- Firmware/EC write methods invoked: **no**");
					WriteBatteryChangeReport(changeReport);
					Environment.ExitCode = 2;
				}
				else
				{
					try
					{
						using IAorusBatteryChargeController controller = new GigabyteWmiBatteryChargeController();
						DeviceCompatibility compatibility = controller.CheckCompatibility();
						changeReport.AppendLine("## Compatibility gate");
						changeReport.AppendLine();
						stringBuilder6 = changeReport;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
						handler2.AppendLiteral("- Manufacturer: `");
						handler2.AppendFormatted(Escape(compatibility.Manufacturer));
						handler2.AppendLiteral("`");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = changeReport;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
						handler2.AppendLiteral("- Model: `");
						handler2.AppendFormatted(Escape(compatibility.Model));
						handler2.AppendLiteral("`");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = changeReport;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
						handler2.AppendLiteral("- BIOS: `");
						handler2.AppendFormatted(Escape(compatibility.BiosVersion));
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
						stringBuilder6 = changeReport;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
						handler2.AppendLiteral("- Result: ");
						handler2.AppendFormatted(compatibility.IsSupported ? "exact allowlist match" : Escape(compatibility.Message));
						stringBuilder13.AppendLine(ref handler2);
						changeReport.AppendLine();
						BatteryChargeChangeResult result = (standardMode ? controller.SetStandardModeAsync().GetAwaiter().GetResult() : controller.SetCustomLimitAsync(limitPercent.Value).GetAwaiter().GetResult());
						changeReport.AppendLine("## Verified result");
						changeReport.AppendLine();
						stringBuilder6 = changeReport;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 2, stringBuilder6);
						handler2.AppendLiteral("- Original firmware pair: `");
						handler2.AppendFormatted(result.OriginalState.PolicyRaw);
						handler2.AppendLiteral(" + ");
						handler2.AppendFormatted(result.OriginalState.StoredStopPercent);
						handler2.AppendLiteral("`");
						stringBuilder14.AppendLine(ref handler2);
						stringBuilder6 = changeReport;
						StringBuilder stringBuilder15 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 2, stringBuilder6);
						handler2.AppendLiteral("- Verified firmware pair: `");
						handler2.AppendFormatted(result.VerifiedState.PolicyRaw);
						handler2.AppendLiteral(" + ");
						handler2.AppendFormatted(result.VerifiedState.StoredStopPercent);
						handler2.AppendLiteral("`");
						stringBuilder15.AppendLine(ref handler2);
						changeReport.AppendLine("- Write order: policy first, threshold second");
						changeReport.AppendLine("- Readback: exact match");
						changeReport.AppendLine("- Result: success");
					}
					catch (Exception ex)
					{
						changeReport.AppendLine("## Result");
						changeReport.AppendLine();
						stringBuilder6 = changeReport;
						StringBuilder stringBuilder16 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
						handler2.AppendLiteral("- Failed: ");
						handler2.AppendFormatted(Escape(ex.Message));
						stringBuilder16.AppendLine(ref handler2);
						changeReport.AppendLine("- See the error above for the automatic rollback result.");
						Environment.ExitCode = 5;
					}
					WriteBatteryChangeReport(changeReport);
				}
			}
		}
		static void RunBatteryInspection()
		{
			StringBuilder batteryReport = new StringBuilder();
			batteryReport.AppendLine("# AORUS battery charge-limit inspection");
			batteryReport.AppendLine();
			StringBuilder stringBuilder6 = batteryReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			batteryReport.AppendLine("- Mode: read-only");
			batteryReport.AppendLine("- Firmware/EC write methods invoked: **no**");
			batteryReport.AppendLine();
			string model = GetFirstValue("root\\cimv2", "SELECT Model FROM Win32_ComputerSystem", "Model");
			string bios = GetFirstValue("root\\cimv2", "SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
			batteryReport.AppendLine("## Compatibility gate");
			batteryReport.AppendLine();
			stringBuilder6 = batteryReport;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Model: `");
			handler2.AppendFormatted(Escape(model));
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = batteryReport;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
			handler2.AppendLiteral("- BIOS: `");
			handler2.AppendFormatted(Escape(bios));
			handler2.AppendLiteral("`");
			stringBuilder9.AppendLine(ref handler2);
			stringBuilder6 = batteryReport;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
			handler2.AppendLiteral("- Administrator: ");
			handler2.AppendFormatted(IsAdministrator() ? "yes" : "no");
			stringBuilder10.AppendLine(ref handler2);
			if (!model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) || !bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase))
			{
				batteryReport.AppendLine("- Result: refused; this inspection is allowlisted only for `AORUS 5 SE / FB0F`.");
				WriteBatteryInspectionReport(batteryReport);
				Environment.ExitCode = 2;
			}
			else
			{
				batteryReport.AppendLine("- Result: exact model/BIOS match");
				batteryReport.AppendLine();
				batteryReport.AppendLine("## Windows battery state");
				batteryReport.AppendLine();
				List<Dictionary<string, object>> batteries = Query2("root\\cimv2", "SELECT Name, BatteryStatus, EstimatedChargeRemaining, DesignVoltage FROM Win32_Battery");
				if (batteries.Count == 0)
				{
					batteryReport.AppendLine("- No `Win32_Battery` instance returned.");
				}
				else
				{
					foreach (Dictionary<string, object> battery in batteries)
					{
						stringBuilder6 = batteryReport;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
						handler2.AppendLiteral("- Name: `");
						handler2.AppendFormatted(Escape(GetText(battery, "Name")));
						handler2.AppendLiteral("`");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = batteryReport;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder6);
						handler2.AppendLiteral("- BatteryStatus: `");
						handler2.AppendFormatted(Escape(GetText(battery, "BatteryStatus")));
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
						stringBuilder6 = batteryReport;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
						handler2.AppendLiteral("- EstimatedChargeRemaining: `");
						handler2.AppendFormatted(Escape(GetText(battery, "EstimatedChargeRemaining")));
						handler2.AppendLiteral("%`");
						stringBuilder13.AppendLine(ref handler2);
						stringBuilder6 = batteryReport;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder6);
						handler2.AppendLiteral("- DesignVoltage: `");
						handler2.AppendFormatted(Escape(GetText(battery, "DesignVoltage")));
						handler2.AppendLiteral(" mV`");
						stringBuilder14.AppendLine(ref handler2);
					}
				}
				batteryReport.AppendLine();
				batteryReport.AppendLine("## Firmware charge state");
				batteryReport.AppendLine();
				if (!IsAdministrator())
				{
					batteryReport.AppendLine("- Firmware read refused: Windows requires administrator rights for this ACPI-WMI invocation.");
					WriteBatteryInspectionReport(batteryReport);
					Environment.ExitCode = 1;
				}
				else
				{
					try
					{
						ManagementClass getClass = new ManagementClass("root\\WMI", "GB_WMIACPI_Get", null);
						try
						{
							getClass.Get();
							HashSet<string> availableMethods = (from MethodData method in getClass.Methods
								select method.Name).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
							string[] missingMethods = new string[2] { "GetChargePolicy", "GetChargeStop" }.Where((string method) => !availableMethods.Contains(method)).ToArray();
							if (missingMethods.Length != 0)
							{
								stringBuilder6 = batteryReport;
								StringBuilder stringBuilder15 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(33, 1, stringBuilder6);
								handler2.AppendLiteral("- Refused: missing getter(s): `");
								handler2.AppendFormatted(string.Join("`, `", missingMethods));
								handler2.AppendLiteral("`.");
								stringBuilder15.AppendLine(ref handler2);
								WriteBatteryInspectionReport(batteryReport);
								Environment.ExitCode = 3;
								return;
							}
							using ManagementObjectCollection instances = getClass.GetInstances();
							ManagementObject instance = instances.Cast<ManagementObject>().FirstOrDefault();
							try
							{
								if (instance == null)
								{
									batteryReport.AppendLine("- Refused: `GB_WMIACPI_Get` has no live instance.");
									WriteBatteryInspectionReport(batteryReport);
									Environment.ExitCode = 3;
									return;
								}
								byte policy = InvokeByteGetter(instance, "GetChargePolicy");
								byte stop = InvokeByteGetter(instance, "GetChargeStop");
								string policyMeaning = policy switch
								{
									0 => "Standard/BIOS mode; stored stop byte is not an active custom limit", 
									4 => "Custom charge limit enabled", 
									_ => "unknown raw policy; do not write", 
								};
								stringBuilder6 = batteryReport;
								StringBuilder stringBuilder16 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(26, 2, stringBuilder6);
								handler2.AppendLiteral("- `GetChargePolicy`: `");
								handler2.AppendFormatted(policy);
								handler2.AppendLiteral("` — ");
								handler2.AppendFormatted(policyMeaning);
								stringBuilder16.AppendLine(ref handler2);
								stringBuilder6 = batteryReport;
								StringBuilder stringBuilder17 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
								handler2.AppendLiteral("- `GetChargeStop`: `");
								handler2.AppendFormatted(stop);
								handler2.AppendLiteral("`");
								stringBuilder17.AppendLine(ref handler2);
								stringBuilder6 = batteryReport;
								StringBuilder stringBuilder18 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
								handler2.AppendLiteral("- Effective interpretation: ");
								handler2.AppendFormatted((policy == 4) ? $"custom limit {stop}%" : policyMeaning);
								stringBuilder18.AppendLine(ref handler2);
							}
							finally
							{
								((IDisposable)instance)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)getClass)?.Dispose();
						}
					}
					catch (Exception ex)
					{
						stringBuilder6 = batteryReport;
						StringBuilder stringBuilder19 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder6);
						handler2.AppendLiteral("- Read failed: ");
						handler2.AppendFormatted(Escape(ex.Message));
						stringBuilder19.AppendLine(ref handler2);
						Environment.ExitCode = 4;
					}
					WriteBatteryInspectionReport(batteryReport);
				}
			}
		}
		void RunBrightnessEventMonitor()
		{
			int durationSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 45), 10, 300);
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Monitor der Fn+Space-Helligkeitsereignisse");
			Console.WriteLine();
			Console.WriteLine($"Laufzeit {durationSeconds} Sekunden. Schalte in dieser Zeit mit Fn+Space");
			Console.WriteLine("mehrfach durch ALLE Stufen, inklusive der hellsten.");
			Console.WriteLine("Jedes Ereignis wird sofort angezeigt.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS Fn+Space brightness event monitor");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(36, 2, stringBuilder6);
			handler2.AppendLiteral("- Exact target device: `VID ");
			handler2.AppendFormatted(4164, "X4");
			handler2.AppendLiteral(" / PID ");
			handler2.AppendFormatted(31297, "X4");
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(49, 2, stringBuilder6);
			handler2.AppendLiteral("- Listened collection: `");
			handler2.AppendFormatted("MI_02 / COL_04");
			handler2.AppendLiteral("`, input report length `");
			handler2.AppendFormatted(4);
			handler2.AppendLiteral("`");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- Mode: **read-only**. No setter, no output report, no feature report, no WMI, no EC access");
			stringBuilder6 = report2;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(72, 1, stringBuilder6);
			handler2.AppendLiteral("- Privacy gate: the collection must not declare keyboard usage page `0x");
			handler2.AppendFormatted(7, "X4");
			handler2.AppendLiteral("`");
			stringBuilder10.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder11 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder6);
			handler2.AppendLiteral("- Duration: `");
			handler2.AppendFormatted(durationSeconds);
			handler2.AppendLiteral("` s");
			stringBuilder11.AppendLine(ref handler2);
			report2.AppendLine("- Known so far: byte 0 is report ID `0x04`, byte 1 is constant `0x01`, byte 2 carries the step; observed `0`, `24`, `32`");
			report2.AppendLine();
			HidDevice target = null;
			foreach (HidDevice candidate in DeviceList.Local.GetHidDevices(4164, 31297))
			{
				if (GetInterfaceLabel(candidate.DevicePath).Equals("MI_02 / COL_04", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						ReportDescriptor descriptor = candidate.GetReportDescriptor();
						if (!descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Reports).SelectMany((Report deviceReport) => deviceReport.DataItems).SelectMany((DataItem dataItem) => dataItem.Usages.GetAllValues())
							.Any((uint usage) => usage >> 16 == 7) && !descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Usages.GetAllValues()).Any((uint usage) => usage >> 16 == 7 || usage == 65542) && !candidate.DevicePath.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxInputReportLength() == 4)
						{
							goto IL_03c7;
						}
					}
					catch (Exception)
					{
					}
				}
				continue;
				IL_03c7:
				target = candidate;
				break;
			}
			if (target == null)
			{
				Console.Error.WriteLine("Die Collection MI_02 / COL_04 wurde nicht gefunden oder ist nicht zugelassen.");
				report2.AppendLine("- `MI_02 / COL_04` was not found, declares keyboard usages, or has an unexpected report length; nothing was opened.");
				WriteReport();
				Environment.ExitCode = 4;
			}
			else
			{
				List<(TimeSpan, byte[])> events = new List<(TimeSpan, byte[])>();
				try
				{
					using HidStream stream = target.Open();
					stream.ReadTimeout = 250;
					Stopwatch clock = Stopwatch.StartNew();
					byte[] buffer = new byte[4];
					while (clock.Elapsed.TotalSeconds < (double)durationSeconds)
					{
						int received;
						try
						{
							received = stream.Read(buffer, 0, buffer.Length);
						}
						catch (TimeoutException)
						{
							continue;
						}
						if (received > 0)
						{
							byte[] payload = buffer.AsSpan(0, received).ToArray();
							events.Add((clock.Elapsed, payload));
							string level = ((received > 2) ? payload[2].ToString(CultureInfo.InvariantCulture) : "?");
							Console.WriteLine($"  {clock.Elapsed.TotalSeconds,6:F1} s  {Convert.ToHexString(payload)}  Stufe {level}");
						}
					}
					clock.Stop();
				}
				catch (Exception ex3)
				{
					Console.Error.WriteLine("Monitorfehler: " + ex3.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
					handler2.AppendLiteral("- Monitor error: ");
					handler2.AppendFormatted(Escape(ex3.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				report2.AppendLine("## Events");
				report2.AppendLine();
				if (events.Count == 0)
				{
					report2.AppendLine("- No input report was received during the monitoring window.");
				}
				else
				{
					report2.AppendLine("| At | Raw report | Byte 1 | Byte 2 (step) | Byte 3 |");
					report2.AppendLine("|---|---|---|---|---|");
					foreach (var item4 in events)
					{
						TimeSpan at = item4.Item1;
						byte[] payload2 = item4.Item2;
						string b1 = ((payload2.Length > 1) ? $"`0x{payload2[1]:X2}`" : "-");
						string b2 = ((payload2.Length > 2) ? $"`{payload2[2]}` / `0x{payload2[2]:X2}`" : "-");
						string b3 = ((payload2.Length > 3) ? $"`0x{payload2[3]:X2}`" : "-");
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(22, 5, stringBuilder6);
						handler2.AppendLiteral("| `");
						handler2.AppendFormatted(at.TotalSeconds, "F1");
						handler2.AppendLiteral("` s | `");
						handler2.AppendFormatted(Convert.ToHexString(payload2));
						handler2.AppendLiteral("` | ");
						handler2.AppendFormatted(b1);
						handler2.AppendLiteral(" | ");
						handler2.AppendFormatted(b2);
						handler2.AppendLiteral(" | ");
						handler2.AppendFormatted(b3);
						handler2.AppendLiteral(" |");
						stringBuilder13.AppendLine(ref handler2);
					}
					byte[] distinctSteps = (from value in (from item in events
							where item.Payload.Length > 2
							select item.Payload[2]).Distinct()
						orderby value
						select value).ToArray();
					byte[] distinctTypes = (from value in (from item in events
							where item.Payload.Length > 1
							select item.Payload[1]).Distinct()
						orderby value
						select value).ToArray();
					report2.AppendLine();
					report2.AppendLine("## Summary");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
					handler2.AppendLiteral("- Events captured: `");
					handler2.AppendFormatted(events.Count);
					handler2.AppendLiteral("`");
					stringBuilder14.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder15 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder6);
					handler2.AppendLiteral("- Distinct byte-1 values: ");
					handler2.AppendFormatted(string.Join(", ", distinctTypes.Select((byte value) => $"`0x{value:X2}`")));
					stringBuilder15.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder16 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
					handler2.AppendLiteral("- Distinct steps in byte 2: ");
					handler2.AppendFormatted(string.Join(", ", distinctSteps.Select((byte value) => $"`{value}`")));
					stringBuilder16.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder17 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(30, 1, stringBuilder6);
					handler2.AppendLiteral("- Number of distinct steps: `");
					handler2.AppendFormatted(distinctSteps.Length);
					handler2.AppendLiteral("`");
					stringBuilder17.AppendLine(ref handler2);
					if (distinctSteps.Length >= 4)
					{
						report2.AppendLine("- All four physical steps are now covered, so the value table is complete.");
					}
					else
					{
						report2.AppendLine("- Fewer than four distinct steps were seen, so the table is still incomplete. Missing steps need another run in which every level is cycled.");
					}
				}
				WriteReport();
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-brightness-events-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine();
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		void RunBrightnessInteractionTest()
		{
			byte[] zoneValues = ReadByteListArgument("--zone-values", new byte[4] { 0, 24, 32, 50 });
			byte[] expectedSteps = new byte[4] { 0, 24, 32, 50 };
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Zusammenspiel von Hardware-Stufe und Zonen-Helligkeitsbyte");
			Console.WriteLine();
			Console.WriteLine("Fuer jede der vier Fn+Space-Stufen wird das Zonen-Helligkeitsbyte");
			Console.WriteLine("durchgeschaltet: " + string.Join(", ", zoneValues) + ". Die Farbe bleibt weiss.");
			Console.WriteLine("Die aktive Hardware-Stufe wird live mitgelesen, nicht geraten.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS brightness interaction matrix");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(36, 2, stringBuilder6);
			handler2.AppendLiteral("- Exact target device: `VID ");
			handler2.AppendFormatted(4164, "X4");
			handler2.AppendLiteral(" / PID ");
			handler2.AppendFormatted(31297, "X4");
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, plus read-only input listening");
			report2.AppendLine("- Hardware step read live from `MI_02 / COL_04`, report ID `0x04`, byte 2");
			report2.AppendLine("- Global effect command, picture matrix, WMI, and EC: **not used**");
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(79, 1, stringBuilder6);
			handler2.AppendLiteral("- Privacy gate: collections declaring keyboard usage page `0x");
			handler2.AppendFormatted(7, "X4");
			handler2.AppendLiteral("` are never opened");
			stringBuilder9.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(37, 1, stringBuilder6);
			handler2.AppendLiteral("- Zone brightness values per step: `");
			handler2.AppendFormatted(string.Join("`, `", zoneValues));
			handler2.AppendLiteral("`");
			stringBuilder10.AppendLine(ref handler2);
			report2.AppendLine("- Purpose: determine whether the zone brightness byte behaves differently depending on the active hardware step");
			report2.AppendLine();
			HidDevice rgbDevice = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice hidDevice) => GetInterfaceLabel(hidDevice.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && hidDevice.GetMaxFeatureReportLength() == 9);
			HidDevice stepDevice = null;
			foreach (HidDevice candidate in DeviceList.Local.GetHidDevices(4164, 31297))
			{
				if (GetInterfaceLabel(candidate.DevicePath).Equals("MI_02 / COL_04", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						if (!candidate.GetReportDescriptor().DeviceItems.SelectMany((DeviceItem item) => item.Reports).SelectMany((Report deviceReport) => deviceReport.DataItems).SelectMany((DataItem dataItem) => dataItem.Usages.GetAllValues())
							.Any((uint usage) => usage >> 16 == 7) && !candidate.DevicePath.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxInputReportLength() == 4)
						{
							stepDevice = candidate;
						}
					}
					catch (Exception)
					{
					}
					break;
				}
			}
			if (rgbDevice == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; nothing was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			int observedStep = -1;
			Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
			List<(string, byte, byte, string)> rows = new List<(string, byte, byte, string)>();
			CancellationTokenSource stepCancellation = new CancellationTokenSource();
			try
			{
				Thread stepListener = null;
				HidStream stepStream = null;
				if (stepDevice != null)
				{
					try
					{
						stepStream = stepDevice.Open();
						stepStream.ReadTimeout = 250;
						HidStream capturedStream = stepStream;
						stepListener = new Thread((ThreadStart)delegate
						{
							byte[] array2 = new byte[4];
							while (!stepCancellation.IsCancellationRequested)
							{
								try
								{
									if (capturedStream.Read(array2, 0, array2.Length) > 2)
									{
										Volatile.Write(ref observedStep, array2[2]);
									}
								}
								catch (TimeoutException)
								{
								}
								catch (Exception)
								{
									break;
								}
							}
						})
						{
							IsBackground = true,
							Name = "aorus-step-listener"
						};
						stepListener.Start();
						report2.AppendLine("- `MI_02 / COL_04` opened for live step reading: **yes**");
					}
					catch (Exception ex2)
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(74, 2, stringBuilder6);
						handler2.AppendLiteral("- `");
						handler2.AppendFormatted("MI_02 / COL_04");
						handler2.AppendLiteral("` could not be opened (");
						handler2.AppendFormatted(Escape(ex2.Message));
						handler2.AppendLiteral("); steps fall back to the owner's own statement.");
						stringBuilder11.AppendLine(ref handler2);
					}
				}
				else
				{
					report2.AppendLine("- `MI_02 / COL_04` was not found or is not approved; steps fall back to the owner's own statement.");
				}
				byte[] value;
				try
				{
					using HidStream rgbStream = rgbDevice.Open();
					report2.AppendLine();
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone(rgbStream, zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
					}
					for (int stepIndex = 0; stepIndex < expectedSteps.Length; stepIndex++)
					{
						byte wanted = expectedSteps[stepIndex];
						string wantedName = wanted switch
						{
							0 => "aus", 
							24 => "niedrig", 
							32 => "mittel", 
							_ => "hell", 
						};
						Console.WriteLine();
						Console.WriteLine($"--- Hardware-Stufe {stepIndex + 1} von {expectedSteps.Length}: {wantedName} (erwartet {wanted}) ---");
						Console.WriteLine("  Schalte mit Fn+Space auf '" + wantedName + "' und druecke dann Enter.");
						Console.Write("  Enter: ");
						Console.ReadLine();
						int measured = Volatile.Read(in observedStep);
						string stepLabel = ((measured >= 0) ? $"{measured} (gemessen)" : $"{wanted} (angenommen, nicht gemessen)");
						Console.WriteLine("  Aktive Stufe: " + stepLabel);
						value = zoneValues;
						foreach (byte zoneValue in value)
						{
							for (byte zone3 = 1; zone3 <= 3; zone3++)
							{
								WriteZone(rgbStream, zone3, byte.MaxValue, byte.MaxValue, byte.MaxValue, zoneValue);
								Thread.Sleep(65);
							}
							byte[] readback = QueryZone(rgbStream, 1);
							Console.WriteLine($"  Zonen-Byte {zoneValue} gesetzt, gespeichert {readback[6]}.");
							Console.Write("    Beobachtung: ");
							string observation = Console.ReadLine()?.Trim() ?? string.Empty;
							rows.Add((stepLabel, zoneValue, readback[6], (observation.Length == 0) ? "(keine Beschreibung)" : observation));
							if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
							{
								Console.WriteLine("Test wird beendet.");
								return;
							}
						}
					}
				}
				catch (Exception ex3)
				{
					Console.Error.WriteLine("Testfehler: " + ex3.Message);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder13 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex3.Message));
					stringBuilder13.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					stepCancellation.Cancel();
					stepListener?.Join(1500);
					stepStream?.Dispose();
					report2.AppendLine();
					report2.AppendLine("## Matrix");
					report2.AppendLine();
					if (rows.Count == 0)
					{
						report2.AppendLine("- No row was recorded.");
					}
					else
					{
						report2.AppendLine("| Hardware step | Zone byte | Stored | Owner observation |");
						report2.AppendLine("|---|---|---|---|");
						foreach (var item5 in rows)
						{
							string step = item5.Item1;
							byte zoneValue2 = item5.Item2;
							byte stored = item5.Item3;
							string observation2 = item5.Item4;
							stringBuilder6 = report2;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 4, stringBuilder6);
							handler2.AppendLiteral("| `");
							handler2.AppendFormatted(step);
							handler2.AppendLiteral("` | `");
							handler2.AppendFormatted(zoneValue2);
							handler2.AppendLiteral("` | `");
							handler2.AppendFormatted(stored);
							handler2.AppendLiteral("` | ");
							handler2.AppendFormatted(Escape(observation2));
							handler2.AppendLiteral(" |");
							stringBuilder14.AppendLine(ref handler2);
						}
					}
					try
					{
						using HidStream restoreStream = rgbDevice.Open();
						report2.AppendLine();
						report2.AppendLine("## Restoration");
						report2.AppendLine();
						foreach (KeyValuePair<byte, byte[]> item6 in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
						{
							item6.Deconstruct(out var key2, out value);
							byte zone4 = key2;
							byte[] original = value;
							WriteZone(restoreStream, zone4, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone(restoreStream, zone4);
							bool exact = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder15.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
					}
					catch (Exception ex4)
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder16 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
						handler2.AppendLiteral("- RESTORE ERROR: ");
						handler2.AppendFormatted(Escape(ex4.Message));
						stringBuilder16.AppendLine(ref handler2);
						Console.Error.WriteLine("Wiederherstellungsfehler: " + ex4.Message);
						Environment.ExitCode = 7;
					}
					WriteReport();
				}
			}
			finally
			{
				if (stepCancellation != null)
				{
					((IDisposable)stepCancellation).Dispose();
				}
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-brightness-interaction-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine();
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		static void RunBrightnessSignalHunt()
		{
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Suche nach einem Host-Signal der Fn+Space-Stufen");
			Console.WriteLine();
			Console.WriteLine("Ablauf pro Runde: Du druckst Fn+Space, wartest kurz, und druckst dann Enter.");
			Console.WriteLine("Waehrend der Wartezeit wird auf allen Nicht-Tastatur-Collections mitgehoert.");
			Console.WriteLine("Nach Enter wird der komplette lesbare Zustand abgefragt.");
			Console.WriteLine("Am Ende werden alle Runden gegeneinander verglichen.");
			Console.WriteLine();
			Console.WriteLine("Es wird ausschliesslich gelesen. Collections mit Tastatur-Usages werden");
			Console.WriteLine("uebersprungen, es koennen also keine Tastenanschlaege erfasst werden.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS Fn+Space brightness signal hunt");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(36, 2, stringBuilder6);
			handler2.AppendLiteral("- Exact target device: `VID ");
			handler2.AppendFormatted(4164, "X4");
			handler2.AppendLiteral(" / PID ");
			handler2.AppendFormatted(31297, "X4");
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Mode: **read-only**. No setter, no output report, no WMI, no EC write");
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(112, 1, stringBuilder6);
			handler2.AppendLiteral("- Privacy gate: every collection declaring keyboard usage page `0x");
			handler2.AppendFormatted(7, "X4");
			handler2.AppendLiteral("` is skipped, so keystrokes cannot be captured");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) written: **no**");
			stringBuilder6 = report2;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(109, 1, stringBuilder6);
			handler2.AppendLiteral("- New ground 1: official getter `0x88` queried for selectors `0` to `");
			handler2.AppendFormatted((byte)15);
			handler2.AppendLiteral("`; only `0` to `3` were ever used before");
			stringBuilder10.AppendLine(ref handler2);
			report2.AppendLine("- New ground 2: input listening on the small `MI_02` collections, including `COL_03` and `COL_04`, which declare no usages");
			report2.AppendLine();
			List<(string, HidDevice)> listenDevices = new List<(string, HidDevice)>();
			HidDevice rgbDevice = null;
			foreach (HidDevice candidate in DeviceList.Local.GetHidDevices(4164, 31297))
			{
				string label = GetInterfaceLabel(candidate.DevicePath);
				bool declaresKeyboard;
				int inputLength;
				try
				{
					ReportDescriptor descriptor = candidate.GetReportDescriptor();
					bool num2 = descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Reports).SelectMany((Report deviceReport) => deviceReport.DataItems).SelectMany((DataItem dataItem) => dataItem.Usages.GetAllValues())
						.Any((uint usage) => usage >> 16 == 7);
					bool keyUsageOnCollection = descriptor.DeviceItems.SelectMany((DeviceItem item) => item.Usages.GetAllValues()).Any((uint usage) => usage >> 16 == 7 || usage == 65542);
					bool keyboardDevicePath = candidate.DevicePath.EndsWith("\\kbd", StringComparison.OrdinalIgnoreCase);
					declaresKeyboard = num2 | keyUsageOnCollection | keyboardDevicePath;
					inputLength = candidate.GetMaxInputReportLength();
				}
				catch (Exception ex)
				{
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 2, stringBuilder6);
					handler2.AppendLiteral("- `");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral("`: descriptor unreadable, skipped (");
					handler2.AppendFormatted(Escape(ex.Message));
					handler2.AppendLiteral(")");
					stringBuilder11.AppendLine(ref handler2);
					continue;
				}
				if (GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9)
				{
					rgbDevice = candidate;
				}
				if (declaresKeyboard)
				{
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
					handler2.AppendLiteral("- `");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral("`: skipped, declares keyboard usages");
					stringBuilder12.AppendLine(ref handler2);
				}
				else if (inputLength <= 0)
				{
					stringBuilder6 = report2;
					StringBuilder stringBuilder13 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
					handler2.AppendLiteral("- `");
					handler2.AppendFormatted(label);
					handler2.AppendLiteral("`: skipped, no input reports");
					stringBuilder13.AppendLine(ref handler2);
				}
				else
				{
					listenDevices.Add((label, candidate));
				}
			}
			report2.AppendLine();
			stringBuilder6 = report2;
			StringBuilder stringBuilder14 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder6);
			handler2.AppendLiteral("- Collections listened to: `");
			handler2.AppendFormatted(listenDevices.Count);
			handler2.AppendLiteral("`");
			stringBuilder14.AppendLine(ref handler2);
			foreach (var item7 in listenDevices)
			{
				string label2 = item7.Item1;
				HidDevice device2 = item7.Item2;
				stringBuilder6 = report2;
				StringBuilder stringBuilder15 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(30, 2, stringBuilder6);
				handler2.AppendLiteral("  - `");
				handler2.AppendFormatted(label2);
				handler2.AppendLiteral("`, input report length `");
				handler2.AppendFormatted(device2.GetMaxInputReportLength());
				handler2.AppendLiteral("`");
				stringBuilder15.AppendLine(ref handler2);
			}
			if (rgbDevice == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine();
				report2.AppendLine("- Exact approved RGB feature collection was not found; no state dump possible.");
				WriteReport();
				Environment.ExitCode = 4;
			}
			else
			{
				List<(string, Dictionary<string, string>)> rounds = new List<(string, Dictionary<string, string>)>();
				ConcurrentBag<string> captured = new ConcurrentBag<string>();
				try
				{
					using HidStream rgbStream = rgbDevice.Open();
					for (int round = 1; round <= 5; round++)
					{
						Console.WriteLine($"Runde {round} von 5.");
						Console.WriteLine("  Druecke jetzt Fn+Space, warte einen Moment, dann Enter.");
						Console.Write("  Beschreibe die aktuelle Helligkeit: ");
						CancellationTokenSource cancellation = new CancellationTokenSource();
						try
						{
							List<Thread> listeners = new List<Thread>();
							List<HidStream> streams = new List<HidStream>();
							foreach (var item8 in listenDevices)
							{
								var (label3, device3) = item8;
								HidStream stream;
								try
								{
									stream = device3.Open();
								}
								catch (Exception ex2)
								{
									captured.Add($"Round {round}: `{label3}` could not be opened ({Escape(ex2.Message)})");
									continue;
								}
								stream.ReadTimeout = 250;
								streams.Add(stream);
								Thread listener = new Thread((ThreadStart)delegate
								{
									byte[] array3 = new byte[device3.GetMaxInputReportLength()];
									while (!cancellation.IsCancellationRequested)
									{
										try
										{
											int num4 = stream.Read(array3, 0, array3.Length);
											if (num4 > 0)
											{
												captured.Add($"Round {round}, `{label3}`: `{Convert.ToHexString(array3.AsSpan(0, num4))}`");
											}
										}
										catch (TimeoutException)
										{
										}
										catch (Exception)
										{
											break;
										}
									}
								})
								{
									IsBackground = true,
									Name = "aorus-listen-" + label3
								};
								listener.Start();
								listeners.Add(listener);
							}
							string description = Console.ReadLine()?.Trim() ?? string.Empty;
							cancellation.Cancel();
							foreach (Thread item9 in listeners)
							{
								item9.Join(1500);
							}
							foreach (HidStream item10 in streams)
							{
								item10.Dispose();
							}
							Dictionary<string, string> state = new Dictionary<string, string>(StringComparer.Ordinal);
							for (byte selector = 0; selector <= 15; selector++)
							{
								state[$"0x88 sel {selector,2}"] = Convert.ToHexString(Query(rgbStream, 136, selector));
							}
							state["0x80 firmware"] = Convert.ToHexString(Query(rgbStream, 128, 0));
							rounds.Add(((description.Length == 0) ? $"Runde {round}" : description, state));
							Console.WriteLine();
							if (description.Equals("/stop", StringComparison.OrdinalIgnoreCase))
							{
								break;
							}
						}
						finally
						{
							if (cancellation != null)
							{
								((IDisposable)cancellation).Dispose();
							}
						}
					}
				}
				catch (Exception ex3)
				{
					Console.Error.WriteLine("Testfehler: " + ex3.Message);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder16 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex3.Message));
					stringBuilder16.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				report2.AppendLine();
				report2.AppendLine("## Captured input reports");
				report2.AppendLine();
				string[] capturedLines = captured.OrderBy<string, string>((string result) => result, StringComparer.Ordinal).ToArray();
				if (capturedLines.Length == 0)
				{
					report2.AppendLine("- **No input report at all** was emitted by any listened collection.");
				}
				else
				{
					string[] array2 = capturedLines;
					foreach (string line in array2)
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder17 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
						handler2.AppendLiteral("- ");
						handler2.AppendFormatted(line);
						stringBuilder17.AppendLine(ref handler2);
					}
				}
				report2.AppendLine();
				report2.AppendLine("## State per round");
				report2.AppendLine();
				if (rounds.Count > 0)
				{
					report2.AppendLine("| Query | " + string.Join(" | ", rounds.Select<(string, Dictionary<string, string>), string>(((string Label, Dictionary<string, string> State) item) => Escape(item.Label))) + " |");
					report2.AppendLine(string.Concat("|---", string.Concat(Enumerable.Repeat("|---", rounds.Count)), "|"));
					List<string> changing = new List<string>();
					foreach (string key2 in rounds[0].Item2.Keys)
					{
						string[] values = rounds.Select<(string, Dictionary<string, string>), string>(((string Label, Dictionary<string, string> State) item) => (!item.State.TryGetValue(key2, out var value)) ? "-" : value).ToArray();
						bool differs = values.Distinct<string>(StringComparer.Ordinal).Count() > 1;
						if (differs)
						{
							changing.Add(key2);
						}
						report2.AppendLine($"| `{key2}`{(differs ? " **changed**" : string.Empty)} | " + string.Join(" | ", values.Select((string value) => "`" + value + "`")) + " |");
					}
					report2.AppendLine();
					report2.AppendLine("## Verdict");
					report2.AppendLine();
					if (changing.Count == 0)
					{
						report2.AppendLine("- **No queried value changed across the rounds.** Neither the extended selector range of the official getter nor the firmware query carries the Fn+Space step.");
					}
					else
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder18 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(145, 1, stringBuilder6);
						handler2.AppendLiteral("- Values that changed across rounds: ");
						handler2.AppendFormatted(string.Join(", ", changing.Select((string text2) => "`" + text2 + "`")));
						handler2.AppendLiteral(". These are the first host-readable trace of the physical brightness step and deserve a dedicated follow-up.");
						stringBuilder18.AppendLine(ref handler2);
					}
					if (capturedLines.Length == 0)
					{
						report2.AppendLine("- No collection outside the keyboard interfaces reported anything, which further supports full in-controller handling of the chord.");
					}
				}
				else
				{
					report2.AppendLine("- No round completed.");
				}
				WriteReport();
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-brightness-signal-hunt-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		static void RunEffectPaletteTest()
		{
			(byte, string, byte, string, string)[] steps = new(byte, string, byte, string, string)[5]
			{
				(1, "Static", 1, "Red", "Wird die Tastatur rot?"),
				(1, "Static", 4, "Blue", "Wird sie blau?"),
				(2, "Breathing", 1, "Red", "Pulsiert sie rot, oder steht sie nur rot?"),
				(3, "Wave", 8, "Random", "Bewegt sich etwas zwischen den Zonen?"),
				(8, "Neon", 8, "Random", "Wechseln die Farben von selbst?")
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Effekt- und Palettentest");
			Console.WriteLine();
			Console.WriteLine("Der vorige Lauf sendete in jedem Schritt Farbbyte 0. Das ist in Gigabytes");
			Console.WriteLine("Enum die Farbe Schwarz, deshalb wurde die Tastatur dunkel. Dieser Test");
			Console.WriteLine("verwendet kraeftige Farben, die sich nicht mit dem Vorzustand verwechseln");
			Console.WriteLine("lassen. Vor jedem Schritt werden alle Zonen auf Weiss gesetzt und geprueft.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS effect and palette test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, global effect `0x08` selector 0");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine("- Baseline before every step: all three zones written to `#FFFFFF` at brightness `50` and verified");
			report2.AppendLine("- Correction under test: the previous isolation run sent palette byte `0` = `FusionLightColor.Black` in every step, which makes its blackout uninformative about the effect engine");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Steps");
					report2.AppendLine();
					for (int index = 0; index < steps.Length; index++)
					{
						(byte, string, byte, string, string) tuple = steps[index];
						byte effect = tuple.Item1;
						string effectName = tuple.Item2;
						byte palette = tuple.Item3;
						string paletteName = tuple.Item4;
						string question = tuple.Item5;
						bool baselineVerified = true;
						for (byte zone3 = 1; zone3 <= 3; zone3++)
						{
							WriteZone2(zone3, byte.MaxValue, byte.MaxValue, byte.MaxValue, 50);
							Thread.Sleep(65);
							byte[] readback = QueryZone2(zone3);
							if (readback[3] != byte.MaxValue || readback[4] != byte.MaxValue || readback[5] != byte.MaxValue || readback[6] != 50)
							{
								baselineVerified = false;
							}
						}
						Console.WriteLine($"{index + 1}/{steps.Length}: Effekt {effect} ({effectName}), Farbe {palette} ({paletteName})");
						Console.WriteLine(baselineVerified ? "  Ausgangslage: alle drei Zonen weiss und geprueft." : "  ACHTUNG: die weisse Ausgangslage konnte nicht verifiziert werden.");
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = effect;
						request[4] = 5;
						request[5] = 50;
						request[6] = palette;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(2000);
						byte[] globalReadback = QueryZone2(0);
						Console.WriteLine("  " + question);
						Console.Write("  Beobachtung: ");
						string observation = Console.ReadLine()?.Trim() ?? string.Empty;
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(37, 5, stringBuilder6);
						handler2.AppendLiteral("### ");
						handler2.AppendFormatted(index + 1);
						handler2.AppendLiteral(". Effect `");
						handler2.AppendFormatted(effect);
						handler2.AppendLiteral("` (");
						handler2.AppendFormatted(effectName);
						handler2.AppendLiteral(") with palette `");
						handler2.AppendFormatted(palette);
						handler2.AppendLiteral("` (");
						handler2.AppendFormatted(paletteName);
						handler2.AppendLiteral(")");
						stringBuilder9.AppendLine(ref handler2);
						report2.AppendLine();
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
						handler2.AppendLiteral("- White baseline verified: **");
						handler2.AppendFormatted(baselineVerified ? "yes" : "no");
						handler2.AppendLiteral("**");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
						handler2.AppendLiteral("- Request: `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Global readback: `");
						handler2.AppendFormatted(Convert.ToHexString(globalReadback));
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Owner observation: ");
						handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung eingegeben)" : observation));
						stringBuilder13.AppendLine(ref handler2);
						report2.AppendLine();
						if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
						{
							Console.WriteLine("Test wird beendet.");
							break;
						}
						Console.WriteLine();
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder14.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					foreach (var (zone4, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
					{
						try
						{
							WriteZone2(zone4, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone4);
							bool exact = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder15.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(" restore failed: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder16.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
					Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte selector)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(65);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-effect-palette-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
			}
		}
		static void RunEffectSelectionIsolation()
		{
			(byte, string, string)[] steps = new(byte, string, string)[5]
			{
				(51, "Custom 1", "Wird die Tastatur dunkel, so wie beim letzten Test?"),
				(1, "Static", "Bleibt sie unveraendert, oder aendert sich etwas?"),
				(52, "Custom 2", "Wird sie wieder dunkel? Custom 2 ist ebenfalls leer."),
				(2, "Breathing", "Passiert etwas, oder bleibt alles unveraendert?"),
				(8, "Neon", "Passiert etwas, oder bleibt alles unveraendert?")
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Isolation der globalen Effektauswahl");
			Console.WriteLine();
			Console.WriteLine("Vor jedem Schritt werden alle drei Zonen auf Weiss gesetzt und geprueft,");
			Console.WriteLine("damit du eine eindeutige Ausgangslage siehst. Danach wird genau ein");
			Console.WriteLine("Effektpaket gesendet. Es wird KEIN Picture-Matrix-Kommando verwendet.");
			Console.WriteLine("Beschreibe nach jedem Schritt, was du siehst, und druecke Enter.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS global effect selection isolation");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88`, global effect `0x08` selector 0");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine("- Baseline before every step: all three zones written to `#FFFFFF` at brightness `50` and verified");
			report2.AppendLine("- Purpose: separate the blackout cause — effect selection `51` versus the failed picture-matrix output reports");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Steps");
					report2.AppendLine();
					for (int index = 0; index < steps.Length; index++)
					{
						(byte, string, string) tuple = steps[index];
						byte effect = tuple.Item1;
						string name = tuple.Item2;
						string question = tuple.Item3;
						bool baselineVerified = true;
						for (byte zone3 = 1; zone3 <= 3; zone3++)
						{
							WriteZone2(zone3, byte.MaxValue, byte.MaxValue, byte.MaxValue, 50);
							Thread.Sleep(65);
							byte[] readback = QueryZone2(zone3);
							if (readback[3] != byte.MaxValue || readback[4] != byte.MaxValue || readback[5] != byte.MaxValue || readback[6] != 50)
							{
								baselineVerified = false;
							}
						}
						Console.WriteLine($"{index + 1}/{steps.Length}: Effekt {effect} ({name})");
						Console.WriteLine(baselineVerified ? "  Ausgangslage: alle drei Zonen weiss und geprueft. Die Tastatur sollte jetzt hell leuchten." : "  ACHTUNG: die weisse Ausgangslage konnte nicht verifiziert werden.");
						Console.WriteLine("  Sende Effektpaket ...");
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = effect;
						request[4] = 5;
						request[5] = 50;
						request[6] = 0;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(1500);
						byte[] globalReadback = QueryZone2(0);
						byte[] zoneAfter = QueryZone2(1);
						Console.WriteLine("  " + question);
						Console.Write("  Beobachtung: ");
						string observation = Console.ReadLine()?.Trim() ?? string.Empty;
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 3, stringBuilder6);
						handler2.AppendLiteral("### ");
						handler2.AppendFormatted(index + 1);
						handler2.AppendLiteral(". Effect `");
						handler2.AppendFormatted(effect);
						handler2.AppendLiteral("` (");
						handler2.AppendFormatted(name);
						handler2.AppendLiteral(")");
						stringBuilder9.AppendLine(ref handler2);
						report2.AppendLine();
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
						handler2.AppendLiteral("- White baseline verified: **");
						handler2.AppendFormatted(baselineVerified ? "yes" : "no");
						handler2.AppendLiteral("**");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
						handler2.AppendLiteral("- Request: `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Global readback: `");
						handler2.AppendFormatted(Convert.ToHexString(globalReadback));
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(54, 4, stringBuilder6);
						handler2.AppendLiteral("- Zone 1 readback after the effect: `#");
						handler2.AppendFormatted(zoneAfter[3], "X2");
						handler2.AppendFormatted(zoneAfter[4], "X2");
						handler2.AppendFormatted(zoneAfter[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(zoneAfter[6]);
						handler2.AppendLiteral("`");
						stringBuilder13.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Owner observation: ");
						handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung eingegeben)" : observation));
						stringBuilder14.AppendLine(ref handler2);
						report2.AppendLine();
						if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
						{
							Console.WriteLine("Test wird beendet.");
							break;
						}
						Console.WriteLine();
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder15 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder15.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					foreach (var (zone4, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
					{
						try
						{
							WriteZone2(zone4, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone4);
							bool exact = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder16.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder17 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(" restore failed: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder17.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
					Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte selector)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(65);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-effect-isolation-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
			}
		}
		void RunFanCurveWriteTest()
		{
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-fan-curve-write", StringComparison.OrdinalIgnoreCase));
			StringBuilder test = new StringBuilder();
			test.AppendLine("# AORUS conservative fan-curve write test");
			test.AppendLine();
			StringBuilder stringBuilder6 = test;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			test.AppendLine("- Change: point 1 value 68 to 80; no temperature lowered");
			stringBuilder6 = test;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(45, 1, stringBuilder6);
			handler2.AppendLiteral("- Explicit curve-write confirmation present: ");
			handler2.AppendFormatted(confirmed ? "yes" : "no");
			stringBuilder8.AppendLine(ref handler2);
			test.AppendLine("- Mandatory restore: all 15 original points plus original mode");
			test.AppendLine();
			if (!confirmed)
			{
				test.AppendLine("- Refused before opening the setter: `--confirm-fan-curve-write` is required.");
				test.AppendLine("- Firmware/EC write methods invoked: **no**");
				WriteCurveTestReport(test);
				Environment.ExitCode = 2;
				return;
			}
			using IAorusFanController controller = new GigabyteWmiFanController();
			FanControlState original = null;
			bool curveWriteStarted = false;
			try
			{
				original = controller.ReadAsync().GetAwaiter().GetResult();
				if (original.FixedStatusRaw != 0 || original.StepStatusRaw != 0 || original.AutoStatusRaw != 0 || original.NvidiaThermalTargetRaw != 0)
				{
					throw new AorusFanControlException("Der Test startet nur aus dem verifizierten Normalzustand.");
				}
				if (original.Curve[1] != new FanCurvePoint(1, 50, 68) || original.Curve[2].Value < 80)
				{
					throw new AorusFanControlException("Die erwartete Originalkurve liegt nicht mehr vor.");
				}
				FanCurvePoint[] modified = original.Curve.ToArray();
				modified[1] = new FanCurvePoint(1, 50, 80);
				curveWriteStarted = true;
				FanProfileChangeResult curveResult = controller.SetCurveAsync(modified).GetAwaiter().GetResult();
				test.AppendLine("## Curve readback");
				test.AppendLine();
				stringBuilder6 = test;
				StringBuilder stringBuilder9 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
				handler2.AppendLiteral("- Original point 1: (");
				handler2.AppendFormatted(curveResult.OriginalState.Curve[1].Temperature);
				handler2.AppendLiteral(", ");
				handler2.AppendFormatted(curveResult.OriginalState.Curve[1].Value);
				handler2.AppendLiteral(")");
				stringBuilder9.AppendLine(ref handler2);
				stringBuilder6 = test;
				StringBuilder stringBuilder10 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
				handler2.AppendLiteral("- Modified point 1: (");
				handler2.AppendFormatted(curveResult.VerifiedState.Curve[1].Temperature);
				handler2.AppendLiteral(", ");
				handler2.AppendFormatted(curveResult.VerifiedState.Curve[1].Value);
				handler2.AppendLiteral(")");
				stringBuilder10.AppendLine(ref handler2);
				stringBuilder6 = test;
				StringBuilder stringBuilder11 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder6);
				handler2.AppendLiteral("- Other 14 points unchanged: ");
				handler2.AppendFormatted(curveResult.OriginalState.Curve.Where((FanCurvePoint _, int i) => i != 1).SequenceEqual(curveResult.VerifiedState.Curve.Where((FanCurvePoint _, int i) => i != 1)) ? "yes" : "no");
				stringBuilder11.AppendLine(ref handler2);
				FanProfileChangeResult dynamic = controller.SetDynamicAsync().GetAwaiter().GetResult();
				test.AppendLine();
				test.AppendLine("## Dynamic result with modified point");
				test.AppendLine();
				AppendFanState(test, "Dynamic", dynamic.VerifiedState);
				using IAorusTelemetryReader telemetry = new GigabyteWmiTelemetryReader();
				for (int sample = 1; sample <= 3; sample++)
				{
					Thread.Sleep(TimeSpan.FromSeconds(2L));
					TelemetrySnapshot value = telemetry.ReadAsync().GetAwaiter().GetResult();
					stringBuilder6 = test;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(61, 7, stringBuilder6);
					handler2.AppendLiteral("- Sample ");
					handler2.AppendFormatted(sample);
					handler2.AppendLiteral(": CPU ");
					handler2.AppendFormatted(value.CpuTemperatureCelsius);
					handler2.AppendLiteral(" °C, GPU ");
					handler2.AppendFormatted(value.GpuTemperatureCelsius);
					handler2.AppendLiteral(" °C, ");
					handler2.AppendLiteral("CPU ");
					handler2.AppendFormatted(value.CpuFanRpm);
					handler2.AppendLiteral(" RPM / raw ");
					handler2.AppendFormatted(value.CpuFanDutyPercent);
					handler2.AppendLiteral(", ");
					handler2.AppendLiteral("GPU ");
					handler2.AppendFormatted(value.GpuFanRpm);
					handler2.AppendLiteral(" RPM / raw ");
					handler2.AppendFormatted(value.GpuFanDutyPercent);
					stringBuilder12.AppendLine(ref handler2);
					if (value.CpuTemperatureCelsius > 65 || value.GpuTemperatureCelsius > 65)
					{
						throw new AorusFanControlException("Temperature guard triggered.");
					}
				}
			}
			catch (Exception ex)
			{
				stringBuilder6 = test;
				StringBuilder stringBuilder13 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder6);
				handler2.AppendLiteral("- Test failed: ");
				handler2.AppendFormatted(Escape(ex.Message));
				stringBuilder13.AppendLine(ref handler2);
				Environment.ExitCode = 5;
			}
			finally
			{
				if (curveWriteStarted && (object)original != null)
				{
					test.AppendLine();
					test.AppendLine("## Restore");
					test.AppendLine();
					try
					{
						FanProfileChangeResult restored = controller.RestoreAsync(original).GetAwaiter().GetResult();
						AppendFanState(test, "Verified original", restored.VerifiedState);
						stringBuilder6 = test;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(43, 1, stringBuilder6);
						handler2.AppendLiteral("- All 15 original points restored exactly: ");
						handler2.AppendFormatted(restored.VerifiedState.Curve.SequenceEqual(original.Curve));
						stringBuilder14.AppendLine(ref handler2);
					}
					catch (Exception ex2)
					{
						stringBuilder6 = test;
						StringBuilder stringBuilder15 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
						handler2.AppendLiteral("- CRITICAL: restore failed: ");
						handler2.AppendFormatted(Escape(ex2.Message));
						stringBuilder15.AppendLine(ref handler2);
						Environment.ExitCode = 6;
					}
				}
			}
			WriteCurveTestReport(test);
		}
		void RunFanFixedScaleTest(bool lowRange)
		{
			byte[] targets = ((!lowRange) ? new byte[3] { 160, 194, 229 } : new byte[5] { 57, 68, 91, 114, 137 });
			string rangeName = (lowRange ? "low" : "high");
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-fan-write", StringComparison.OrdinalIgnoreCase));
			StringBuilder test = new StringBuilder();
			test.AppendLine("# AORUS fixed fan raw-scale test");
			test.AppendLine();
			StringBuilder stringBuilder6 = test;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = test;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 2, stringBuilder6);
			handler2.AppendLiteral("- Targets: ");
			handler2.AppendFormatted(string.Join(", ", targets));
			handler2.AppendLiteral(" (");
			handler2.AppendFormatted(rangeName);
			handler2.AppendLiteral(" range)");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = test;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
			handler2.AppendLiteral("- Explicit write confirmation present: ");
			handler2.AppendFormatted(confirmed ? "yes" : "no");
			stringBuilder9.AppendLine(ref handler2);
			test.AppendLine("- Mandatory exact restore of the original persistent state");
			test.AppendLine();
			if (!confirmed)
			{
				test.AppendLine("- Refused before opening the setter: `--confirm-fan-write` is required.");
				test.AppendLine("- Firmware/EC write methods invoked: **no**");
				WriteFixedScaleReport(test);
				Environment.ExitCode = 2;
				return;
			}
			using IAorusFanController controller = new GigabyteWmiFanController();
			FanControlState original = null;
			bool aWriteSucceeded = false;
			try
			{
				original = controller.ReadAsync().GetAwaiter().GetResult();
				if (original.FixedStatusRaw != 0 || original.StepStatusRaw != 0 || original.AutoStatusRaw != 0 || original.NvidiaThermalTargetRaw != 0)
				{
					throw new AorusFanControlException("Der Test startet nur aus dem verifizierten Normalzustand.");
				}
				using IAorusTelemetryReader telemetry = new GigabyteWmiTelemetryReader();
				byte[] array2 = targets;
				foreach (byte rawValue in array2)
				{
					FanProfileChangeResult selected = controller.SetFixedAsync(rawValue).GetAwaiter().GetResult();
					aWriteSucceeded = true;
					stringBuilder6 = test;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(7, 1, stringBuilder6);
					handler2.AppendLiteral("## Raw ");
					handler2.AppendFormatted(rawValue);
					stringBuilder10.AppendLine(ref handler2);
					test.AppendLine();
					AppendFanState(test, "Verified fixed", selected.VerifiedState);
					int sampleCount = (lowRange ? 2 : 3);
					for (int sample = 1; sample <= sampleCount; sample++)
					{
						Thread.Sleep(TimeSpan.FromSeconds(2L));
						TelemetrySnapshot value = telemetry.ReadAsync().GetAwaiter().GetResult();
						stringBuilder6 = test;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(61, 7, stringBuilder6);
						handler2.AppendLiteral("- Sample ");
						handler2.AppendFormatted(sample);
						handler2.AppendLiteral(": CPU ");
						handler2.AppendFormatted(value.CpuTemperatureCelsius);
						handler2.AppendLiteral(" °C, GPU ");
						handler2.AppendFormatted(value.GpuTemperatureCelsius);
						handler2.AppendLiteral(" °C, ");
						handler2.AppendLiteral("CPU ");
						handler2.AppendFormatted(value.CpuFanRpm);
						handler2.AppendLiteral(" RPM / raw ");
						handler2.AppendFormatted(value.CpuFanDutyPercent);
						handler2.AppendLiteral(", ");
						handler2.AppendLiteral("GPU ");
						handler2.AppendFormatted(value.GpuFanRpm);
						handler2.AppendLiteral(" RPM / raw ");
						handler2.AppendFormatted(value.GpuFanDutyPercent);
						stringBuilder11.AppendLine(ref handler2);
						if (value.CpuTemperatureCelsius > 65 || value.GpuTemperatureCelsius > 65)
						{
							throw new AorusFanControlException($"Temperature guard triggered at CPU {value.CpuTemperatureCelsius} °C / GPU {value.GpuTemperatureCelsius} °C.");
						}
					}
					test.AppendLine();
				}
			}
			catch (Exception ex)
			{
				stringBuilder6 = test;
				StringBuilder stringBuilder12 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder6);
				handler2.AppendLiteral("- Test failed: ");
				handler2.AppendFormatted(Escape(ex.Message));
				stringBuilder12.AppendLine(ref handler2);
				Environment.ExitCode = 5;
			}
			finally
			{
				if (aWriteSucceeded && (object)original != null)
				{
					test.AppendLine("## Restore");
					test.AppendLine();
					try
					{
						FanProfileChangeResult restored = controller.RestoreAsync(original).GetAwaiter().GetResult();
						AppendFanState(test, "Verified original", restored.VerifiedState);
						test.AppendLine("- Result: exact persistent original restored");
					}
					catch (Exception ex2)
					{
						stringBuilder6 = test;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
						handler2.AppendLiteral("- CRITICAL: restore failed: ");
						handler2.AppendFormatted(Escape(ex2.Message));
						stringBuilder13.AppendLine(ref handler2);
						Environment.ExitCode = 6;
					}
				}
			}
			WriteFixedScaleReport(test);
		}
		void RunFanNormalChange()
		{
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-fan-write", StringComparison.OrdinalIgnoreCase));
			StringBuilder change = new StringBuilder();
			change.AppendLine("# AORUS fan Normal-profile change");
			change.AppendLine();
			StringBuilder stringBuilder6 = change;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			change.AppendLine("- Requested profile: Normal / firmware curve");
			stringBuilder6 = change;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
			handler2.AppendLiteral("- Explicit write confirmation present: ");
			handler2.AppendFormatted(confirmed ? "yes" : "no");
			stringBuilder8.AppendLine(ref handler2);
			change.AppendLine();
			if (!confirmed)
			{
				change.AppendLine("- Refused before opening the setter: `--confirm-fan-write` is required.");
				change.AppendLine("- Firmware/EC write methods invoked: **no**");
				WriteFanChangeReport(change);
				Environment.ExitCode = 2;
			}
			else
			{
				try
				{
					using IAorusFanController controller = new GigabyteWmiFanController();
					DeviceCompatibility compatibility = controller.CheckCompatibility();
					change.AppendLine("## Compatibility gate");
					change.AppendLine();
					stringBuilder6 = change;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
					handler2.AppendLiteral("- Manufacturer: `");
					handler2.AppendFormatted(Escape(compatibility.Manufacturer));
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					stringBuilder6 = change;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
					handler2.AppendLiteral("- Model: `");
					handler2.AppendFormatted(Escape(compatibility.Model));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = change;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
					handler2.AppendLiteral("- BIOS: `");
					handler2.AppendFormatted(Escape(compatibility.BiosVersion));
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
					stringBuilder6 = change;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
					handler2.AppendLiteral("- Result: ");
					handler2.AppendFormatted(compatibility.IsSupported ? "exact allowlist match" : Escape(compatibility.Message));
					stringBuilder12.AppendLine(ref handler2);
					change.AppendLine();
					FanProfileChangeResult result = controller.SetNormalAsync().GetAwaiter().GetResult();
					change.AppendLine("## Verified result");
					change.AppendLine();
					AppendFanState(change, "Original", result.OriginalState);
					AppendFanState(change, "Verified", result.VerifiedState);
					stringBuilder6 = change;
					StringBuilder stringBuilder13 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 1, stringBuilder6);
					handler2.AppendLiteral("- Curve preserved exactly: ");
					handler2.AppendFormatted(result.OriginalState.Curve.SequenceEqual(result.VerifiedState.Curve) ? "yes" : "no");
					stringBuilder13.AppendLine(ref handler2);
					change.AppendLine("- Setter order: Fixed off, Step off, Auto off, NVIDIA thermal target 0");
					change.AppendLine("- Result: success");
				}
				catch (Exception ex)
				{
					change.AppendLine("## Result");
					change.AppendLine();
					stringBuilder6 = change;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
					handler2.AppendLiteral("- Failed: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder14.AppendLine(ref handler2);
					change.AppendLine("- If a write had started, the controller attempted and verified rollback before returning this error.");
					Environment.ExitCode = 5;
				}
				WriteFanChangeReport(change);
			}
		}
		static void RunInteractiveHostEffectTest()
		{
			(string, string, Func<double, (byte, byte, byte)[]>)[] effects = new(string, string, Func<double, (byte, byte, byte)[]>)[10]
			{
				("Static", "Sollte ruhig und unveraenderlich in einer Farbe leuchten.", (double _) => Uniform(0, byte.MaxValue, 0)),
				("Breathing", "Sollte langsam heller und dunkler werden, ohne Farbwechsel.", (double elapsed) => Uniform(Scale(0, Ramp(elapsed, 3.0)), Scale(byte.MaxValue, Ramp(elapsed, 3.0)), Scale(0, Ramp(elapsed, 3.0)))),
				("Pulse", "Sollte deutlich schneller und harter blinken als Breathing.", (double elapsed) => (!(elapsed % 0.7 < 0.35)) ? Uniform(10, 2, 0) : Uniform(byte.MaxValue, 40, 0)),
				("Colour cycle", "Alle drei Zonen sollten gemeinsam durch das ganze Farbspektrum wandern.", delegate(double elapsed)
				{
					(byte, byte, byte) tuple2 = HostHueToRgb(elapsed / 6.0 % 1.0);
					return new(byte, byte, byte)[3] { tuple2, tuple2, tuple2 };
				}),
				("Rainbow marquee", "Die drei Zonen sollten unterschiedliche Farben zeigen, die nach rechts wandern.", (double elapsed) => new(byte, byte, byte)[3]
				{
					HostHueToRgb((elapsed / 5.0 + 0.0) % 1.0),
					HostHueToRgb((elapsed / 5.0 + 0.33) % 1.0),
					HostHueToRgb((elapsed / 5.0 + 0.66) % 1.0)
				}),
				("Wave", "Eine helle Zone sollte weich von links nach rechts laufen, Rest gedimmt.", (double elapsed) => Travelling(elapsed, 0.5, (r: 0, g: byte.MaxValue, b: 120), (r: 0, g: 30, b: 15), pingPong: false)),
				("Marquee", "Wie Wave, aber schneller und mit hartem Wechsel.", (double elapsed) => Travelling(elapsed, 0.18, (r: byte.MaxValue, g: byte.MaxValue, b: byte.MaxValue), (r: 0, g: 0, b: 0), pingPong: false)),
				("Rotate", "Die helle Zone sollte hin und zurueck pendeln, nicht nur in eine Richtung.", (double elapsed) => Travelling(elapsed, 0.4, (r: 120, g: 0, b: byte.MaxValue), (r: 12, g: 0, b: 25), pingPong: true)),
				("Raindrop", "Einzelne Zonen sollten unregelmaessig kurz aufblitzen.", delegate(double elapsed)
				{
					(byte, byte, byte)[] obj = new(byte, byte, byte)[3]
					{
						(0, 10, 30),
						(0, 10, 30),
						(0, 10, 30)
					};
					int num3 = (int)(Math.Abs(Math.Sin((double)(int)(elapsed / 0.25) * 12.9898) * 43758.5453) % 3.0);
					obj[num3] = (120, 200, byte.MaxValue);
					return obj;
				}),
				("Fade sweep", "Die Zonen sollten nacheinander aufleuchten und langsam ausklingen.", delegate(double elapsed)
				{
					(byte, byte, byte)[] array3 = new(byte, byte, byte)[3];
					for (int i = 0; i < 3; i++)
					{
						double num3 = (elapsed / 1.2 - (double)i * 0.33) % 1.0;
						if (num3 < 0.0)
						{
							num3++;
						}
						double factor = Math.Max(0.0, 1.0 - num3 * 1.6);
						array3[i] = (Scale(byte.MaxValue, factor), Scale(120, factor), Scale(0, factor));
					}
					return array3;
				})
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - interaktiver Test host-gerenderter RGB-Effekte");
			Console.WriteLine("Jeder Effekt laeuft dauerhaft, bis du Enter drueckst.");
			Console.WriteLine("Enter = naechster Effekt | Text = Beobachtung speichern und weiter | /stop = beenden");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS interactive host-rendered RGB-effect test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88` only");
			report2.AppendLine("- Global effect command `0x08` selector 0 used: **no**");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(38, 1, stringBuilder6);
			handler2.AppendLiteral("- Frame interval per zone write: `");
			handler2.AppendFormatted(5);
			handler2.AppendLiteral(" ms`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Animation is rendered in the RGB values; the brightness byte stays at `50`, because raw brightness is a proven off/on gate on this firmware.");
			report2.AppendLine("- Advancement: owner-controlled; no fixed timeout");
			report2.AppendLine("- Restore policy: capture all zones; restore and verify in `finally`");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Effects and owner observations");
					report2.AppendLine();
					for (int index = 0; index < effects.Length; index++)
					{
						string name;
						string hint;
						Func<double, (byte r, byte g, byte b)[]> frameAt;
						(name, hint, frameAt) = effects[index];
						Console.WriteLine($"{index + 1}/{effects.Length}: {name}");
						Console.WriteLine("  " + hint);
						Console.Write("  Beobachtung: ");
						CancellationTokenSource cancellation = new CancellationTokenSource();
						try
						{
							long renderedFrames = 0L;
							Stopwatch clock = Stopwatch.StartNew();
							Thread thread = new Thread((ThreadStart)delegate
							{
								while (!cancellation.IsCancellationRequested)
								{
									(byte, byte, byte)[] array3 = frameAt(clock.Elapsed.TotalSeconds);
									byte b2 = 1;
									while (b2 <= 3 && !cancellation.IsCancellationRequested)
									{
										var (red, green, blue) = array3[b2 - 1];
										WriteZone2(b2, red, green, blue, 50);
										Thread.Sleep(5);
										b2++;
									}
									Interlocked.Increment(ref renderedFrames);
								}
							});
							thread.IsBackground = true;
							thread.Name = $"aorus-host-effect-{index}";
							thread.Start();
							string observation = Console.ReadLine() ?? string.Empty;
							cancellation.Cancel();
							thread.Join();
							clock.Stop();
							bool num2 = observation.Trim().Equals("/stop", StringComparison.OrdinalIgnoreCase);
							string recorded = ((num2 || observation.Trim().Length == 0) ? "(keine Beschreibung eingegeben)" : observation.Trim());
							double achieved = (double)renderedFrames / Math.Max(0.001, clock.Elapsed.TotalSeconds);
							stringBuilder6 = report2;
							StringBuilder stringBuilder10 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(8, 2, stringBuilder6);
							handler2.AppendLiteral("### ");
							handler2.AppendFormatted(index + 1);
							handler2.AppendLiteral(". `");
							handler2.AppendFormatted(name);
							handler2.AppendLiteral("`");
							stringBuilder10.AppendLine(ref handler2);
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder6);
							handler2.AppendLiteral("- Expected appearance: ");
							handler2.AppendFormatted(Escape(hint));
							stringBuilder11.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder12 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(49, 3, stringBuilder6);
							handler2.AppendLiteral("- Ran for `");
							handler2.AppendFormatted(clock.Elapsed.TotalSeconds, "F1");
							handler2.AppendLiteral("` s, `");
							handler2.AppendFormatted(renderedFrames);
							handler2.AppendLiteral("` three-zone frames, `");
							handler2.AppendFormatted(achieved, "F1");
							handler2.AppendLiteral("` frames/s");
							stringBuilder12.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
							handler2.AppendLiteral("- Owner observation: ");
							handler2.AppendFormatted(Escape(recorded));
							stringBuilder13.AppendLine(ref handler2);
							report2.AppendLine();
							if (num2)
							{
								Console.WriteLine("Test wird beendet und die vorherigen Zonen werden wiederhergestellt.");
								break;
							}
							Console.WriteLine();
						}
						finally
						{
							if (cancellation != null)
							{
								((IDisposable)cancellation).Dispose();
							}
						}
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder14.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					if (originalZones.Count == 3)
					{
						try
						{
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								WriteZone2(zone3, original[3], original[4], original[5], original[6]);
								Thread.Sleep(65);
								byte[] restored = QueryZone2(zone3);
								bool verified = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
								stringBuilder6 = report2;
								StringBuilder stringBuilder15 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder15.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
							Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder16.AppendLine(ref handler2);
							Console.Error.WriteLine("Wiederherstellungsfehler: " + ex2.Message);
							Environment.ExitCode = 7;
						}
					}
					else
					{
						report2.AppendLine("- Restore unavailable because not all three original zones were captured.");
						Environment.ExitCode = 7;
					}
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte b2)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = b2;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(10);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-host-effect-interactive-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request = new byte[9];
				request[1] = 8;
				request[2] = b2;
				request[3] = red;
				request[4] = green;
				request[5] = blue;
				request[6] = brightness;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
			}
		}
		static void RunInteractiveKeyboardEffectTest()
		{
			(byte, string, byte, string)[] effects = new(byte, string, byte, string)[10]
			{
				(1, "Static", 2, "Sollte dauerhaft grün leuchten."),
				(2, "Breathing", 2, "Sollte grün pulsieren/atmen."),
				(3, "Wave", 8, "Achte auf eine wandernde Welle zwischen den drei Zonen."),
				(4, "Fade-on-keypress", 2, "Drücke mehrere Tasten und achte auf ein Nachleuchten."),
				(5, "Marquee", 8, "Achte auf ein laufendes Farbmuster."),
				(6, "Ripple", 2, "Drücke mehrere Tasten und achte auf eine auslaufende Reaktion."),
				(8, "Neon", 8, "Achte auf automatisch wechselnde Farben."),
				(10, "Raindrop", 8, "Achte auf einzelne zufällige Lichtimpulse."),
				(12, "Hedge", 8, "Achte auf ein gerichtetes, wanderndes Muster."),
				(13, "Rotate", 8, "Achte auf eine rotierende/umlaufende Bewegung.")
			};
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE – interaktiver RGB-Effekttest");
			Console.WriteLine("Enter = nächster Effekt | Text = Beobachtung speichern und weiter | /stop = beenden");
			Console.WriteLine();
			StringBuilder interactiveReport = new StringBuilder();
			interactiveReport.AppendLine("# AORUS interactive visible RGB-effect test");
			interactiveReport.AppendLine();
			StringBuilder stringBuilder6 = interactiveReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			interactiveReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			interactiveReport.AppendLine("- Tested list: Gigabyte's ten compact ITE UI effects");
			interactiveReport.AppendLine("- Shared parameters: raw speed `5`, brightness `50`, direction `1`");
			interactiveReport.AppendLine("- Advancement: user-controlled; no fixed timeout");
			interactiveReport.AppendLine("- Restore policy: capture all zones; restore and verify in `finally`");
			interactiveReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die freigegebene RGB-Schnittstelle wurde nicht gefunden.");
				interactiveReport.AppendLine("- Exact approved RGB interface was not found; no packet was sent.");
				WriteInteractiveKeyboardEffectReport(interactiveReport);
				Environment.ExitCode = 2;
				return;
			}
			Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
			HidStream stream = device2.Open();
			try
			{
				try
				{
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						originalZones.Add(zone2, Query3(136, zone2, 65));
					}
					interactiveReport.AppendLine("## Effect observations");
					interactiveReport.AppendLine();
					for (int index = 0; index < effects.Length; index++)
					{
						(byte, string, byte, string) tuple = effects[index];
						byte id = tuple.Item1;
						string name = tuple.Item2;
						byte palette = tuple.Item3;
						string item = tuple.Item4;
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = id;
						request[4] = 5;
						request[5] = 50;
						request[6] = palette;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(500);
						byte[] readback = Query3(136, 0, 500);
						Console.WriteLine($"[{index + 1}/{effects.Length}] {name} (ID {id})");
						Console.WriteLine(item);
						Console.Write("Beobachtung oder Enter: ");
						string observation = Console.ReadLine();
						int num2;
						object obj;
						if (observation != null)
						{
							num2 = (observation.Trim().Equals("/stop", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
							if (num2 == 0)
							{
								obj = (string.IsNullOrWhiteSpace(observation) ? "No note entered." : observation.Trim());
								goto IL_03db;
							}
						}
						else
						{
							num2 = 1;
						}
						obj = "Test stopped by user before an observation was recorded.";
						goto IL_03db;
						IL_03db:
						string recordedObservation = (string)obj;
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 3, stringBuilder6);
						handler2.AppendLiteral("### ");
						handler2.AppendFormatted(index + 1);
						handler2.AppendLiteral(". `");
						handler2.AppendFormatted(name);
						handler2.AppendLiteral("` (ID `");
						handler2.AppendFormatted(id);
						handler2.AppendLiteral("`)");
						stringBuilder8.AppendLine(ref handler2);
						interactiveReport.AppendLine();
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
						handler2.AppendLiteral("- Request: `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Global readback: `");
						handler2.AppendFormatted(Convert.ToHexString(readback));
						handler2.AppendLiteral("`");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = interactiveReport;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
						handler2.AppendLiteral("- User observation: ");
						handler2.AppendFormatted(Escape(recordedObservation));
						stringBuilder11.AppendLine(ref handler2);
						interactiveReport.AppendLine();
						if (num2 != 0)
						{
							Console.WriteLine("Test wird beendet und die vorherigen Zonen werden wiederhergestellt.");
							break;
						}
						Console.WriteLine();
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					stringBuilder6 = interactiveReport;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					interactiveReport.AppendLine("## Restoration");
					interactiveReport.AppendLine();
					if (originalZones.Count == 3)
					{
						try
						{
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> keyValuePair2) => keyValuePair2.Key))
							{
								SetZone(zone3, original[3], original[4], original[5], original[6]);
								byte[] restored = Query3(136, zone3, 65);
								bool verified = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
								stringBuilder6 = interactiveReport;
								StringBuilder stringBuilder13 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder13.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
							Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
						}
						catch (Exception ex2)
						{
							stringBuilder6 = interactiveReport;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder14.AppendLine(ref handler2);
							Console.Error.WriteLine("Wiederherstellungsfehler: " + ex2.Message);
							Environment.ExitCode = 7;
						}
					}
					else
					{
						interactiveReport.AppendLine("- Restore unavailable because not all three original zones were captured.");
						Environment.ExitCode = 7;
					}
				}
				WriteInteractiveKeyboardEffectReport(interactiveReport);
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void SetZone(byte b2, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
			}
		}
		static void RunKeyboardBreathingTest()
		{
			StringBuilder effectReport = new StringBuilder();
			effectReport.AppendLine("# AORUS guarded Breathing-effect test");
			effectReport.AppendLine();
			StringBuilder stringBuilder6 = effectReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			effectReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			effectReport.AppendLine("- Temporary request: Breathing `2`, raw speed `5` (official UI 50/100), brightness `50`, Green `2`, direction `1`");
			effectReport.AppendLine("- Visible hold: 10 seconds");
			effectReport.AppendLine("- Restore policy: capture all three zones first; restore and verify them in `finally`");
			effectReport.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			effectReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				effectReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteKeyboardEffectTestReport(effectReport);
				return;
			}
			Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
			HidStream stream = device2.Open();
			try
			{
				try
				{
					effectReport.AppendLine("## Captured zone state");
					effectReport.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] original = Query3(136, zone2, 65);
						originalZones.Add(zone2, original);
						stringBuilder6 = effectReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(original[3], "X2");
						handler2.AppendFormatted(original[4], "X2");
						handler2.AppendFormatted(original[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(original[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					byte[] request = new byte[9];
					request[1] = 8;
					request[2] = 0;
					request[3] = 2;
					request[4] = 5;
					request[5] = 50;
					request[6] = 2;
					request[7] = 1;
					request[8] = CalculateGigabyteChecksum(request);
					stream.SetFeature(request);
					Thread.Sleep(500);
					byte[] globalReadback = Query3(136, 0, 500);
					effectReport.AppendLine();
					effectReport.AppendLine("## Effect request and readback");
					effectReport.AppendLine();
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
					handler2.AppendLiteral("- Request: `");
					handler2.AppendFormatted(Convert.ToHexString(request));
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
					handler2.AppendLiteral("- Global readback: `");
					handler2.AppendFormatted(Convert.ToHexString(globalReadback));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(78, 5, stringBuilder6);
					handler2.AppendLiteral("- Decoded readback: effect `");
					handler2.AppendFormatted(globalReadback[3]);
					handler2.AppendLiteral("`, speed `");
					handler2.AppendFormatted(globalReadback[4]);
					handler2.AppendLiteral("`, ");
					handler2.AppendLiteral("brightness `");
					handler2.AppendFormatted(globalReadback[5]);
					handler2.AppendLiteral("`, color `");
					handler2.AppendFormatted(globalReadback[6]);
					handler2.AppendLiteral("`, direction `");
					handler2.AppendFormatted(globalReadback[7]);
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
					effectReport.AppendLine("- The effect was then left visible for ten seconds for direct observation.");
					Thread.Sleep(10000);
				}
				catch (Exception ex)
				{
					effectReport.AppendLine();
					stringBuilder6 = effectReport;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					if (originalZones.Count == 3)
					{
						effectReport.AppendLine();
						effectReport.AppendLine("## Restoration");
						effectReport.AppendLine();
						try
						{
							foreach (var (zone3, original2) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								SetZone(zone3, original2[3], original2[4], original2[5], original2[6]);
								byte[] restored = Query3(136, zone3, 65);
								bool verified = restored[3] == original2[3] && restored[4] == original2[4] && restored[5] == original2[5] && restored[6] == original2[6];
								stringBuilder6 = effectReport;
								StringBuilder stringBuilder13 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder13.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = effectReport;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder14.AppendLine(ref handler2);
							Environment.ExitCode = 7;
						}
					}
				}
				WriteKeyboardEffectTestReport(effectReport);
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void SetZone(byte b2, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
			}
		}
		static void RunKeyboardBrightnessCycle()
		{
			byte[] brightnessSteps = new byte[5] { 0, 1, 25, 49, 50 };
			StringBuilder cycleReport = new StringBuilder();
			cycleReport.AppendLine("# AORUS keyboard software-brightness cycle");
			cycleReport.AppendLine();
			StringBuilder stringBuilder6 = cycleReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			cycleReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			cycleReport.AppendLine("- Boundary steps: raw `0`, `1`, `25`, `49`, `50` (approximately 0%, 2%, 50%, 98%, 100%)");
			cycleReport.AppendLine("- Hold time: five seconds per step");
			cycleReport.AppendLine("- Final requested state: raw `50` / on");
			cycleReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				cycleReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteBrightnessCycleReport(cycleReport);
			}
			else
			{
				try
				{
					HidStream stream = device2.Open();
					try
					{
						byte[][] zoneStates = new byte[3][];
						for (byte zone2 = 1; zone2 <= 3; zone2++)
						{
							zoneStates[zone2 - 1] = QueryZone2(zone2);
						}
						stringBuilder6 = cycleReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(54, 9, stringBuilder6);
						handler2.AppendLiteral("- Colors preserved: zone 1 `#");
						handler2.AppendFormatted(zoneStates[0][3], "X2");
						handler2.AppendFormatted(zoneStates[0][4], "X2");
						handler2.AppendFormatted(zoneStates[0][5], "X2");
						handler2.AppendLiteral("`, ");
						handler2.AppendLiteral("zone 2 `#");
						handler2.AppendFormatted(zoneStates[1][3], "X2");
						handler2.AppendFormatted(zoneStates[1][4], "X2");
						handler2.AppendFormatted(zoneStates[1][5], "X2");
						handler2.AppendLiteral("`, ");
						handler2.AppendLiteral("zone 3 `#");
						handler2.AppendFormatted(zoneStates[2][3], "X2");
						handler2.AppendFormatted(zoneStates[2][4], "X2");
						handler2.AppendFormatted(zoneStates[2][5], "X2");
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
						cycleReport.AppendLine();
						byte[] array2 = brightnessSteps;
						foreach (byte brightness in array2)
						{
							for (byte zone3 = 1; zone3 <= 3; zone3++)
							{
								byte[] saved = zoneStates[zone3 - 1];
								SetZone(zone3, saved[3], saved[4], saved[5], brightness);
							}
							bool verified = true;
							List<string> readbacks = new List<string>();
							for (byte zone4 = 1; zone4 <= 3; zone4++)
							{
								byte[] readback = QueryZone2(zone4);
								verified &= readback[6] == brightness;
								readbacks.Add($"Z{zone4}={readback[6]}");
							}
							stringBuilder6 = cycleReport;
							StringBuilder stringBuilder9 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 3, stringBuilder6);
							handler2.AppendLiteral("- Raw candidate ");
							handler2.AppendFormatted(brightness);
							handler2.AppendLiteral(": ");
							handler2.AppendFormatted(string.Join(", ", readbacks));
							handler2.AppendLiteral("; stored value verified **");
							handler2.AppendFormatted(verified ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder9.AppendLine(ref handler2);
							Thread.Sleep(5000);
						}
						byte[] array3 = QueryZone2(1);
						byte[] finalZone2 = QueryZone2(2);
						byte[] finalZone3 = QueryZone2(3);
						bool finalVerified = array3[6] == 50 && finalZone2[6] == 50 && finalZone3[6] == 50;
						stringBuilder6 = cycleReport;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(47, 1, stringBuilder6);
						handler2.AppendLiteral("- Final on-state (`50`) readback verified: **");
						handler2.AppendFormatted(finalVerified ? "yes" : "no");
						handler2.AppendLiteral("**");
						stringBuilder10.AppendLine(ref handler2);
					}
					finally
					{
						if (stream != null)
						{
							((IDisposable)stream).Dispose();
						}
					}
					byte[] QueryZone2(byte b)
					{
						byte[] request = new byte[9];
						request[1] = 136;
						request[2] = b;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(65);
						byte[] response = new byte[9];
						stream.GetFeature(response);
						return response;
					}
					void SetZone(byte b, byte red, byte green, byte blue, byte b2)
					{
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = b;
						request[3] = red;
						request[4] = green;
						request[5] = blue;
						request[6] = b2;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(65);
					}
				}
				catch (Exception ex)
				{
					stringBuilder6 = cycleReport;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder6);
					handler2.AppendLiteral("- Cycle failed: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder11.AppendLine(ref handler2);
				}
				WriteBrightnessCycleReport(cycleReport);
			}
		}
		void RunKeyboardBrightnessMonitor()
		{
			int durationSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 25), 5, 120);
			StringBuilder monitorReport = new StringBuilder();
			monitorReport.AppendLine("# AORUS Fn+Space brightness-channel monitor");
			monitorReport.AppendLine();
			StringBuilder stringBuilder6 = monitorReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stringBuilder6 = monitorReport;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
			handler2.AppendLiteral("- Duration: ");
			handler2.AppendFormatted(durationSeconds);
			handler2.AppendLiteral(" seconds");
			stringBuilder8.AppendLine(ref handler2);
			monitorReport.AppendLine("- State-changing command sent: **no**");
			monitorReport.AppendLine("- Standard keyboard interfaces captured: **no**");
			monitorReport.AppendLine("- Vendor interfaces observed: `MI_01` and `MI_03` only");
			monitorReport.AppendLine();
			ConcurrentQueue<string> events = new ConcurrentQueue<string>();
			CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
			try
			{
				Task[] readers = (from hidDevice in DeviceList.Local.GetHidDevices(4164, 31297).Where(delegate(HidDevice hidDevice)
					{
						string text3 = GetInterfaceLabel(hidDevice.DevicePath);
						return (text3.Equals("MI_01", StringComparison.OrdinalIgnoreCase) || text3.Equals("MI_03", StringComparison.OrdinalIgnoreCase)) && hidDevice.GetMaxInputReportLength() == 65;
					}).ToArray()
					select Task.Run(delegate
					{
						string value2 = GetInterfaceLabel(hidDevice.DevicePath);
						try
						{
							using HidStream hidStream = hidDevice.Open();
							hidStream.ReadTimeout = 150;
							byte[] array2 = new byte[hidDevice.GetMaxInputReportLength()];
							string value3 = null;
							while (!cancellation.IsCancellationRequested)
							{
								try
								{
									int num2 = hidStream.Read(array2, 0, array2.Length);
									if (num2 > 0)
									{
										string text3 = Convert.ToHexString(array2.AsSpan(0, num2));
										if (!text3.Equals(value3, StringComparison.Ordinal))
										{
											events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} HID {value2} `{text3}`");
											value3 = text3;
										}
									}
								}
								catch (TimeoutException)
								{
								}
							}
						}
						catch (Exception ex5)
						{
							events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} HID {value2} unavailable: {ex5.Message}");
						}
					}, cancellation.Token)).ToArray();
				ManagementObject wmiInstance = null;
				try
				{
					ManagementClass getClass = new ManagementClass("root\\WMI", "GB_WMIACPI_Get", null);
					try
					{
						getClass.Get();
						using ManagementObjectCollection instances = getClass.GetInstances();
						wmiInstance = instances.Cast<ManagementObject>().FirstOrDefault();
					}
					finally
					{
						((IDisposable)getClass)?.Dispose();
					}
				}
				catch (Exception ex)
				{
					events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} WMI unavailable: {ex.Message}");
				}
				string previousWmi = null;
				while (!cancellation.IsCancellationRequested)
				{
					if (wmiInstance != null)
					{
						try
						{
							InvokeMethodOptions options = new InvokeMethodOptions
							{
								Timeout = TimeSpan.FromSeconds(2L)
							};
							ManagementBaseObject output = wmiInstance.InvokeMethod("GetKeyBoardBackLight", null, options);
							try
							{
								string value = Convert.ToString(output["Data"], CultureInfo.InvariantCulture) ?? string.Empty;
								if (!value.Equals(previousWmi, StringComparison.Ordinal))
								{
									events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} EC KBLL `{value}`");
									previousWmi = value;
								}
							}
							finally
							{
								((IDisposable)output)?.Dispose();
							}
						}
						catch (Exception ex2)
						{
							events.Enqueue($"{DateTimeOffset.Now:HH:mm:ss.fff} WMI read failed: {ex2.Message}");
							wmiInstance.Dispose();
							wmiInstance = null;
						}
					}
					Thread.Sleep(250);
				}
				try
				{
					Task.WaitAll(readers, TimeSpan.FromSeconds(2L));
				}
				catch (AggregateException)
				{
				}
				finally
				{
					wmiInstance?.Dispose();
				}
				monitorReport.AppendLine("## Observed changes");
				monitorReport.AppendLine();
				if (events.IsEmpty)
				{
					monitorReport.AppendLine("- No EC or vendor-HID changes observed.");
				}
				else
				{
					string observedEvent;
					while (events.TryDequeue(out observedEvent))
					{
						stringBuilder6 = monitorReport;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
						handler2.AppendLiteral("- ");
						handler2.AppendFormatted(Escape(observedEvent));
						stringBuilder9.AppendLine(ref handler2);
					}
				}
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-brightness-monitor-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, monitorReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(monitorReport);
				Console.WriteLine("Report written to: " + outputPath2);
			}
			finally
			{
				if (cancellation != null)
				{
					((IDisposable)cancellation).Dispose();
				}
			}
		}
		static void RunKeyboardEffectBatch()
		{
			(byte, string, byte, string)[] effects = new(byte, string, byte, string)[3]
			{
				(2, "Breathing", 2, "The whole keyboard should pulse green."),
				(3, "Wave", 8, "A moving/rainbow wave should cross the three zones."),
				(4, "Fade-on-keypress", 2, "Press several keys; their zone should react/fade if supported.")
			};
			StringBuilder batchReport = new StringBuilder();
			batchReport.AppendLine("# AORUS visible RGB-effect test — batch 1");
			batchReport.AppendLine();
			StringBuilder stringBuilder6 = batchReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			batchReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			batchReport.AppendLine("- Sequence: Breathing → Wave → Fade-on-keypress");
			batchReport.AppendLine("- Hold per effect: 8 seconds");
			batchReport.AppendLine("- Shared parameters: raw speed `5`, brightness `50`, direction `1`");
			batchReport.AppendLine("- Restore policy: capture all zones; restore and verify in `finally`");
			batchReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				batchReport.AppendLine("- Exact approved RGB interface was not found; no packet was sent.");
				WriteKeyboardEffectBatchReport(batchReport);
				return;
			}
			Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
			HidStream stream = device2.Open();
			try
			{
				try
				{
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						originalZones.Add(zone2, Query3(136, zone2, 65));
					}
					batchReport.AppendLine("## Requests");
					batchReport.AppendLine();
					(byte, string, byte, string)[] array2 = effects;
					for (int num2 = 0; num2 < array2.Length; num2++)
					{
						(byte, string, byte, string) tuple = array2[num2];
						byte id = tuple.Item1;
						string name = tuple.Item2;
						byte palette = tuple.Item3;
						string observation = tuple.Item4;
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = 0;
						request[3] = id;
						request[4] = 5;
						request[5] = 50;
						request[6] = palette;
						request[7] = 1;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(500);
						byte[] readback = Query3(136, 0, 500);
						stringBuilder6 = batchReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(66, 5, stringBuilder6);
						handler2.AppendLiteral("- `");
						handler2.AppendFormatted(name);
						handler2.AppendLiteral("` ID `");
						handler2.AppendFormatted(id);
						handler2.AppendLiteral("`: request `");
						handler2.AppendFormatted(Convert.ToHexString(request));
						handler2.AppendLiteral("`, ");
						handler2.AppendLiteral("global readback `");
						handler2.AppendFormatted(Convert.ToHexString(readback));
						handler2.AppendLiteral("`. Expected observation: ");
						handler2.AppendFormatted(observation);
						stringBuilder8.AppendLine(ref handler2);
						Console.WriteLine("Now visible for 8 seconds: " + name);
						Thread.Sleep(8000);
					}
				}
				catch (Exception ex)
				{
					stringBuilder6 = batchReport;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder9.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					batchReport.AppendLine();
					batchReport.AppendLine("## Restoration");
					batchReport.AppendLine();
					if (originalZones.Count == 3)
					{
						try
						{
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								SetZone(zone3, original[3], original[4], original[5], original[6]);
								byte[] restored = Query3(136, zone3, 65);
								bool verified = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
								stringBuilder6 = batchReport;
								StringBuilder stringBuilder10 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 6, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone3);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(restored[3], "X2");
								handler2.AppendFormatted(restored[4], "X2");
								handler2.AppendFormatted(restored[5], "X2");
								handler2.AppendLiteral("`, ");
								handler2.AppendLiteral("brightness `");
								handler2.AppendFormatted(restored[6]);
								handler2.AppendLiteral("`, verified **");
								handler2.AppendFormatted(verified ? "yes" : "no");
								handler2.AppendLiteral("**");
								stringBuilder10.AppendLine(ref handler2);
								if (!verified)
								{
									Environment.ExitCode = 6;
								}
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = batchReport;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder11.AppendLine(ref handler2);
							Environment.ExitCode = 7;
						}
					}
					else
					{
						batchReport.AppendLine("- Restore unavailable because not all three original zones were captured.");
						Environment.ExitCode = 7;
					}
				}
				batchReport.AppendLine();
				batchReport.AppendLine("## Observation status");
				batchReport.AppendLine();
				batchReport.AppendLine("- Visible behavior requires the user's physical observation; HID global readback is known to return zeros on this firmware.");
				WriteKeyboardEffectBatchReport(batchReport);
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void SetZone(byte b2, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request2 = new byte[9];
				request2[1] = 8;
				request2[2] = b2;
				request2[3] = red;
				request2[4] = green;
				request2[5] = blue;
				request2[6] = brightness;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
			}
		}
		static void RunKeyboardFeatureRead()
		{
			string[] approvedCollections = new string[2] { "MI_02 / COL_07", "MI_03" };
			StringBuilder stateReport = new StringBuilder();
			stateReport.AppendLine("# AORUS keyboard read-only feature report");
			stateReport.AppendLine();
			StringBuilder stringBuilder6 = stateReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			stateReport.AppendLine("- Target: `VID 1044 / PID 7A41`");
			stateReport.AppendLine("- Operation: USB HID `GET_REPORT (Feature)` only");
			stateReport.AppendLine("- Output report sent: **no**");
			stateReport.AppendLine("- Feature report set: **no**");
			stateReport.AppendLine();
			HidDevice[] devices = (from hidDevice in DeviceList.Local.GetHidDevices(4164, 31297)
				where approvedCollections.Contains<string>(GetInterfaceLabel(hidDevice.DevicePath), StringComparer.OrdinalIgnoreCase)
				select hidDevice).OrderBy<HidDevice, string>((HidDevice hidDevice) => GetInterfaceLabel(hidDevice.DevicePath), StringComparer.OrdinalIgnoreCase).ToArray();
			HidDevice[] array2 = devices;
			foreach (HidDevice device2 in array2)
			{
				string interfaceLabel = GetInterfaceLabel(device2.DevicePath);
				int reportLength = device2.GetMaxFeatureReportLength();
				byte reportId = (byte)(interfaceLabel.Equals("MI_02 / COL_07", StringComparison.OrdinalIgnoreCase) ? 90 : 0);
				stringBuilder6 = stateReport;
				StringBuilder stringBuilder8 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(5, 1, stringBuilder6);
				handler2.AppendLiteral("## `");
				handler2.AppendFormatted(interfaceLabel);
				handler2.AppendLiteral("`");
				stringBuilder8.AppendLine(ref handler2);
				stateReport.AppendLine();
				if (reportLength <= 0)
				{
					stateReport.AppendLine("- No feature report exposed.");
					stateReport.AppendLine();
				}
				else
				{
					try
					{
						byte[] buffer = new byte[reportLength];
						buffer[0] = reportId;
						using HidStream stream = device2.Open();
						stream.GetFeature(buffer);
						stringBuilder6 = stateReport;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
						handler2.AppendLiteral("- Report ID: `0x");
						handler2.AppendFormatted(reportId, "X2");
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
						stringBuilder6 = stateReport;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(41, 1, stringBuilder6);
						handler2.AppendLiteral("- Length: ");
						handler2.AppendFormatted(buffer.Length);
						handler2.AppendLiteral(" bytes including report ID byte");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = stateReport;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder6);
						handler2.AppendLiteral("- Raw bytes: `");
						handler2.AppendFormatted(Convert.ToHexString(buffer));
						handler2.AppendLiteral("`");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = stateReport;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder6);
						handler2.AppendLiteral("- Payload bytes: `");
						handler2.AppendFormatted(Convert.ToHexString(buffer.AsSpan(1)));
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
					}
					catch (Exception ex)
					{
						stringBuilder6 = stateReport;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder6);
						handler2.AppendLiteral("- Read failed: ");
						handler2.AppendFormatted(Escape(ex.Message));
						stringBuilder13.AppendLine(ref handler2);
					}
					stateReport.AppendLine();
				}
			}
			if (devices.Length == 0)
			{
				stateReport.AppendLine("- No approved feature-report collection found.");
				stateReport.AppendLine();
			}
			stateReport.AppendLine("## Interpretation");
			stateReport.AppendLine();
			stateReport.AppendLine("- Returned bytes are retained as uninterpreted state until their meaning is confirmed by independent observations.");
			stateReport.AppendLine("- Reading a feature report does not reveal whether each byte is a color, mode, brightness, version, or capability flag.");
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-feature-read-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, stateReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(stateReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		static void RunKeyboardHidInspection()
		{
			StringBuilder keyboardReport = new StringBuilder();
			keyboardReport.AppendLine("# AORUS keyboard read-only HID inventory");
			keyboardReport.AppendLine();
			StringBuilder stringBuilder6 = keyboardReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			keyboardReport.AppendLine("- Target: `VID 1044 / PID 7A41`");
			keyboardReport.AppendLine("- HID communication stream opened: **no**");
			keyboardReport.AppendLine("- Input/feature report requested: **no**");
			keyboardReport.AppendLine("- Output report sent: **no**");
			keyboardReport.AppendLine();
			HidDevice[] devices = DeviceList.Local.GetHidDevices(4164, 31297).OrderBy<HidDevice, string>((HidDevice hidDevice) => GetInterfaceLabel(hidDevice.DevicePath), StringComparer.OrdinalIgnoreCase).ToArray();
			stringBuilder6 = keyboardReport;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
			handler2.AppendLiteral("## HID collections (");
			handler2.AppendFormatted(devices.Length);
			handler2.AppendLiteral(")");
			stringBuilder8.AppendLine(ref handler2);
			keyboardReport.AppendLine();
			if (devices.Length == 0)
			{
				keyboardReport.AppendLine("- No matching HID collection found.");
			}
			HidDevice[] array2 = devices;
			foreach (HidDevice device2 in array2)
			{
				string interfaceLabel = GetInterfaceLabel(device2.DevicePath);
				stringBuilder6 = keyboardReport;
				StringBuilder stringBuilder9 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(6, 1, stringBuilder6);
				handler2.AppendLiteral("### `");
				handler2.AppendFormatted(interfaceLabel);
				handler2.AppendLiteral("`");
				stringBuilder9.AppendLine(ref handler2);
				keyboardReport.AppendLine();
				stringBuilder6 = keyboardReport;
				StringBuilder stringBuilder10 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 1, stringBuilder6);
				handler2.AppendLiteral("- Manufacturer: ");
				handler2.AppendFormatted(Escape(TryRead(() => device2.GetManufacturer())));
				stringBuilder10.AppendLine(ref handler2);
				stringBuilder6 = keyboardReport;
				StringBuilder stringBuilder11 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
				handler2.AppendLiteral("- Product: ");
				handler2.AppendFormatted(Escape(TryRead(() => device2.GetProductName())));
				stringBuilder11.AppendLine(ref handler2);
				stringBuilder6 = keyboardReport;
				StringBuilder stringBuilder12 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(30, 1, stringBuilder6);
				handler2.AppendLiteral("- Maximum input report: ");
				handler2.AppendFormatted(device2.GetMaxInputReportLength());
				handler2.AppendLiteral(" bytes");
				stringBuilder12.AppendLine(ref handler2);
				stringBuilder6 = keyboardReport;
				StringBuilder stringBuilder13 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 1, stringBuilder6);
				handler2.AppendLiteral("- Maximum output report: ");
				handler2.AppendFormatted(device2.GetMaxOutputReportLength());
				handler2.AppendLiteral(" bytes");
				stringBuilder13.AppendLine(ref handler2);
				stringBuilder6 = keyboardReport;
				StringBuilder stringBuilder14 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(32, 1, stringBuilder6);
				handler2.AppendLiteral("- Maximum feature report: ");
				handler2.AppendFormatted(device2.GetMaxFeatureReportLength());
				handler2.AppendLiteral(" bytes");
				stringBuilder14.AppendLine(ref handler2);
				keyboardReport.AppendLine("- Device path intentionally omitted (may contain a device-unique identifier).");
				try
				{
					ReportDescriptor descriptor = device2.GetReportDescriptor();
					stringBuilder6 = keyboardReport;
					StringBuilder stringBuilder15 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder6);
					handler2.AppendLiteral("- Uses report IDs: ");
					handler2.AppendFormatted(descriptor.ReportsUseID ? "yes" : "no");
					stringBuilder15.AppendLine(ref handler2);
					foreach (Report hidReport in from item in descriptor.Reports
						orderby item.ReportType, item.ReportID
						select item)
					{
						string usages = string.Join(", ", from usage in hidReport.GetAllUsages().Distinct()
							orderby usage
							select $"0x{usage:X8}");
						stringBuilder6 = keyboardReport;
						StringBuilder stringBuilder16 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(37, 4, stringBuilder6);
						handler2.AppendLiteral("  - ");
						handler2.AppendFormatted(hidReport.ReportType);
						handler2.AppendLiteral(": ID `0x");
						handler2.AppendFormatted(hidReport.ReportID, "X2");
						handler2.AppendLiteral("`, ");
						handler2.AppendLiteral("length ");
						handler2.AppendFormatted(hidReport.Length);
						handler2.AppendLiteral(" bytes, usages ");
						handler2.AppendFormatted(Escape(string.IsNullOrEmpty(usages) ? "none" : usages));
						stringBuilder16.AppendLine(ref handler2);
					}
				}
				catch (Exception ex)
				{
					stringBuilder6 = keyboardReport;
					StringBuilder stringBuilder17 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(35, 1, stringBuilder6);
					handler2.AppendLiteral("- Parsed descriptor: unavailable (");
					handler2.AppendFormatted(Escape(ex.Message));
					handler2.AppendLiteral(")");
					stringBuilder17.AppendLine(ref handler2);
				}
				keyboardReport.AppendLine();
			}
			keyboardReport.AppendLine("## Interpretation");
			keyboardReport.AppendLine();
			keyboardReport.AppendLine("- Enumeration reads Windows HID metadata only; no communication stream was opened and no report reached the keyboard.");
			keyboardReport.AppendLine("- A vendor-defined collection with a large output or feature report is the likely RGB-control channel.");
			keyboardReport.AppendLine("- Report lengths alone do not reveal packet contents or authorize sending a report.");
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-hid-inventory-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, keyboardReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(keyboardReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		void RunKeyboardHostEffectTest()
		{
			int effectSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 8), 3, 30);
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS host-rendered keyboard effect test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3, zone getter `0x88` only");
			report2.AppendLine("- Global effect command `0x08` selector 0 used: **no**");
			report2.AppendLine("- Picture-matrix commands `0x12` / `0x92` used: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder6);
			handler2.AppendLiteral("- Seconds per effect: `");
			handler2.AppendFormatted(effectSeconds);
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Zone brightness byte held at `50` throughout; animation is rendered in the RGB values, because raw brightness is a proven off/on gate on this firmware.");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice hidDevice) => GetInterfaceLabel(hidDevice.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && hidDevice.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			int writeInterval;
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				writeInterval = 65;
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Minimum reliable write interval");
					report2.AppendLine();
					report2.AppendLine("| Interval | Verified writes | Result |");
					report2.AppendLine("|---|---|---|");
					int[] array2 = new int[6] { 65, 40, 25, 15, 10, 5 };
					foreach (int candidate in array2)
					{
						int verified = 0;
						for (int attempt = 0; attempt < 6; attempt++)
						{
							byte value = (byte)(40 + attempt * 30);
							WriteZone2(1, value, 0, value, 50);
							Thread.Sleep(candidate);
							byte[] readback = QueryZone2(1);
							if (readback[3] == value && readback[5] == value)
							{
								verified++;
							}
						}
						bool reliable = verified == 6;
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 3, stringBuilder6);
						handler2.AppendLiteral("| `");
						handler2.AppendFormatted(candidate);
						handler2.AppendLiteral(" ms` | `");
						handler2.AppendFormatted(verified);
						handler2.AppendLiteral(" / 6` | ");
						handler2.AppendFormatted(reliable ? "reliable" : "unreliable");
						handler2.AppendLiteral(" |");
						stringBuilder10.AppendLine(ref handler2);
						if (!reliable)
						{
							break;
						}
						writeInterval = candidate;
					}
					int zoneFramesPerSecond = 1000 / (writeInterval * 3);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(67, 1, stringBuilder6);
					handler2.AppendLiteral("- Fastest interval verified on every attempt: `");
					handler2.AppendFormatted(writeInterval);
					handler2.AppendLiteral(" ms` per zone write.");
					stringBuilder11.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(58, 1, stringBuilder6);
					handler2.AppendLiteral("- Resulting full three-zone frame rate: about `");
					handler2.AppendFormatted(zoneFramesPerSecond);
					handler2.AppendLiteral("` frames/s.");
					stringBuilder12.AppendLine(ref handler2);
					Console.WriteLine();
					Console.WriteLine($"Schnellstes verifiziertes Intervall: {writeInterval} ms pro Zone (~{zoneFramesPerSecond} Bilder/s).");
					Console.WriteLine("Bitte jetzt auf die Tastatur schauen.");
					Console.WriteLine();
					report2.AppendLine();
					report2.AppendLine("## Rendered effects");
					report2.AppendLine();
					RenderEffect("Breathing", "all three zones share one hue whose RGB values follow a sine ramp", delegate(double elapsed)
					{
						byte b2 = (byte)Math.Round((1.0 - Math.Cos(elapsed * 2.0 * Math.PI / 3.0)) / 2.0 * 255.0);
						return new(byte, byte, byte)[3]
						{
							(b2, 0, b2),
							(b2, 0, b2),
							(b2, 0, b2)
						};
					});
					RenderEffect("Colour cycle", "one hue rotating through the full spectrum on all three zones", delegate(double elapsed)
					{
						(byte, byte, byte) tuple = HueToRgb(elapsed / 6.0 % 1.0);
						return new(byte, byte, byte)[3] { tuple, tuple, tuple };
					});
					RenderEffect("Wave", "a bright zone travelling left to right over a dim base", delegate(double elapsed)
					{
						int num3 = (int)(elapsed / 0.35) % 3;
						(byte, byte, byte)[] array4 = new(byte, byte, byte)[3];
						for (int i = 0; i < 3; i++)
						{
							array4[i] = ((byte, byte, byte))((i == num3) ? (0, 255, 120) : (0, 25, 12));
						}
						return array4;
					});
				}
				catch (Exception ex)
				{
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder13 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder13.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine();
					report2.AppendLine("## Restore of the original zone state");
					report2.AppendLine();
					foreach (var (zone3, state2) in originalZones)
					{
						try
						{
							WriteZone2(zone3, state2[3], state2[4], state2[5], state2[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone3);
							bool exact = restored[3] == state2[3] && restored[4] == state2[4] && restored[5] == state2[5] && restored[6] == state2[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone3);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder14.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(26, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone3);
							handler2.AppendLiteral(": restore failed — ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder15.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte b2)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = b2;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(10);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void RenderEffect(string name, string description, Func<double, (byte r, byte g, byte b)[]> frameAt)
			{
				Console.WriteLine("Effekt laeuft: " + name);
				Stopwatch clock = Stopwatch.StartNew();
				int frames = 0;
				while (clock.Elapsed.TotalSeconds < (double)effectSeconds)
				{
					(byte, byte, byte)[] frame = frameAt(clock.Elapsed.TotalSeconds);
					for (byte zone4 = 1; zone4 <= 3; zone4++)
					{
						var (r, g, b2) = frame[zone4 - 1];
						WriteZone2(zone4, r, g, b2, 50);
						Thread.Sleep(writeInterval);
					}
					frames++;
				}
				clock.Stop();
				double achieved = (double)frames / clock.Elapsed.TotalSeconds;
				StringBuilder stringBuilder16 = report2;
				StringBuilder.AppendInterpolatedStringHandler handler3 = new StringBuilder.AppendInterpolatedStringHandler(60, 5, stringBuilder16);
				handler3.AppendLiteral("- **");
				handler3.AppendFormatted(name);
				handler3.AppendLiteral("**: ");
				handler3.AppendFormatted(description);
				handler3.AppendLiteral(". Frames rendered: `");
				handler3.AppendFormatted(frames);
				handler3.AppendLiteral("` in `");
				handler3.AppendFormatted(clock.Elapsed.TotalSeconds, "F1");
				handler3.AppendLiteral("` s, achieved `");
				handler3.AppendFormatted(achieved, "F1");
				handler3.AppendLiteral("` frames/s.");
				stringBuilder16.AppendLine(ref handler3);
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-host-effects-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b2, byte red, byte green, byte blue, byte brightness = 50)
			{
				byte[] request = new byte[9];
				request[1] = 8;
				request[2] = b2;
				request[3] = red;
				request[4] = green;
				request[5] = blue;
				request[6] = brightness;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
			}
		}
		static void RunKeyboardMatrixRead()
		{
			StringBuilder matrixReport = new StringBuilder();
			matrixReport.AppendLine("# AORUS keyboard key-matrix read");
			matrixReport.AppendLine();
			StringBuilder stringBuilder6 = matrixReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			matrixReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input report`");
			matrixReport.AppendLine("- Official Gigabyte device class: `ITE / ZoneRgb / 3a4041`");
			matrixReport.AppendLine("- Known query command: `0x8D`");
			matrixReport.AppendLine("- Expected transfer: eight 65-byte input reports carrying 512 matrix bytes");
			matrixReport.AppendLine("- Matrix, macros, RGB, firmware, BIOS, and EC written: **no**");
			matrixReport.AppendLine("- Serial number recorded: **no**");
			matrixReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9 && candidate.GetMaxInputReportLength() == 65);
			if (device2 == null)
			{
				matrixReport.AppendLine("- Exact approved matrix interface was not found; no command was sent.");
				WriteKeyboardMatrixReport(matrixReport);
			}
			else
			{
				try
				{
					using HidStream stream = device2.Open();
					stream.ReadTimeout = 2500;
					byte[] request = new byte[9];
					request[1] = 141;
					request[8] = CalculateGigabyteChecksum(request);
					stream.SetFeature(request);
					stream.GetFeature(new byte[9] { 0, 141, 0, 0, 8, 0, 0, 0, 0 });
					byte[] matrix = new byte[512];
					List<string> transferHashes = new List<string>();
					for (int block = 0; block < 8; block++)
					{
						byte[] input = new byte[65];
						int received = stream.Read(input, 0, input.Length);
						if (received != 65)
						{
							throw new InvalidOperationException($"Matrix block {block + 1} returned {received} bytes instead of {65}.");
						}
						input.AsSpan(1, 64).CopyTo(matrix.AsSpan(block * 64, 64));
						transferHashes.Add(Convert.ToHexString(SHA256.HashData(input.AsSpan(1, 64))));
					}
					string matrixHash = Convert.ToHexString(SHA256.HashData(matrix));
					int activeRecords = Enumerable.Range(0, 128).Count((int index) => ((ReadOnlySpan<byte>)matrix.AsSpan(index * 4, 4)).IndexOfAnyExcept((byte)0) >= 0);
					int distinctRecords = (from index in Enumerable.Range(0, 128)
						select Convert.ToHexString(matrix.AsSpan(index * 4, 4))).Distinct<string>(StringComparer.Ordinal).Count();
					bool matchesDefault = matrixHash.Equals("92431FE3FAE62A5777FC124D73F090F00877BA7DAFA3080F496CB313F72EC78A", StringComparison.OrdinalIgnoreCase);
					matrixReport.AppendLine("## Transfer result");
					matrixReport.AppendLine();
					matrixReport.AppendLine("- Blocks received: `8 / 8`");
					stringBuilder6 = matrixReport;
					StringBuilder stringBuilder8 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 1, stringBuilder6);
					handler2.AppendLiteral("- Matrix bytes received: `");
					handler2.AppendFormatted(matrix.Length);
					handler2.AppendLiteral("`");
					stringBuilder8.AppendLine(ref handler2);
					stringBuilder6 = matrixReport;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
					handler2.AppendLiteral("- Matrix SHA-256: `");
					handler2.AppendFormatted(matrixHash);
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					matrixReport.AppendLine("- Signed-module default SHA-256: `92431FE3FAE62A5777FC124D73F090F00877BA7DAFA3080F496CB313F72EC78A`");
					stringBuilder6 = matrixReport;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(34, 1, stringBuilder6);
					handler2.AppendLiteral("- Exact default-matrix match: **");
					handler2.AppendFormatted(matchesDefault ? "yes" : "no");
					handler2.AppendLiteral("**");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = matrixReport;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
					handler2.AppendLiteral("- Non-empty four-byte records: `");
					handler2.AppendFormatted(activeRecords);
					handler2.AppendLiteral(" / 128`");
					stringBuilder11.AppendLine(ref handler2);
					stringBuilder6 = matrixReport;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(47, 1, stringBuilder6);
					handler2.AppendLiteral("- Distinct four-byte records including zero: `");
					handler2.AppendFormatted(distinctRecords);
					handler2.AppendLiteral("`");
					stringBuilder12.AppendLine(ref handler2);
					matrixReport.AppendLine();
					matrixReport.AppendLine("### Per-block payload hashes");
					matrixReport.AppendLine();
					for (int block2 = 0; block2 < transferHashes.Count; block2++)
					{
						stringBuilder6 = matrixReport;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(12, 2, stringBuilder6);
						handler2.AppendLiteral("- Block ");
						handler2.AppendFormatted(block2 + 1);
						handler2.AppendLiteral(": `");
						handler2.AppendFormatted(transferHashes[block2]);
						handler2.AppendLiteral("`");
						stringBuilder13.AppendLine(ref handler2);
					}
					matrixReport.AppendLine();
					matrixReport.AppendLine("## Raw 512-byte matrix");
					matrixReport.AppendLine();
					matrixReport.AppendLine("```text");
					for (int offset = 0; offset < matrix.Length; offset += 16)
					{
						stringBuilder6 = matrixReport;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder6);
						handler2.AppendFormatted(offset, "X3");
						handler2.AppendLiteral(": ");
						handler2.AppendFormatted(Convert.ToHexString(matrix.AsSpan(offset, 16)));
						stringBuilder14.AppendLine(ref handler2);
					}
					matrixReport.AppendLine("```");
					matrixReport.AppendLine();
					matrixReport.AppendLine("## Interpretation boundary");
					matrixReport.AppendLine();
					matrixReport.AppendLine("- The controller stores 128 four-byte slots; the signed software maps these slots to the model-specific keyboard layout.");
					matrixReport.AppendLine("- A default-matrix match proves factory assignments are present, not that every shared macro feature is enabled in the UI.");
					matrixReport.AppendLine("- Macro records were not requested by this diagnostic.");
				}
				catch (Exception ex)
				{
					matrixReport.AppendLine("## Read failure");
					matrixReport.AppendLine();
					stringBuilder6 = matrixReport;
					StringBuilder stringBuilder15 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
					handler2.AppendLiteral("- ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder15.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteKeyboardMatrixReport(matrixReport);
			}
		}
		static void RunKeyboardOldDefaultPulse()
		{
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS exact old-default Pulse request");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Source: signed `GBT_Keyboard 23.03.10.01` defaults and `IteKeyBoard.SetLightEffect`");
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Request: Pulse/Breathing `2`, speed `5`, brightness `50`, Orange palette `5`, direction `1`");
			report2.AppendLine("- Expected bytes: `0008000205320501B8`");
			report2.AppendLine("- Persistence: deliberately left active for visual observation; static zone values were not overwritten");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				try
				{
					report2.AppendLine("## Static zone state before request");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] zoneState = Query3(136, zone2, 65);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(zoneState[3], "X2");
						handler2.AppendFormatted(zoneState[4], "X2");
						handler2.AppendFormatted(zoneState[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(zoneState[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					byte[] request = new byte[9] { 0, 8, 0, 2, 5, 50, 5, 1, 0 };
					request[8] = CalculateGigabyteChecksum(request);
					if (Convert.ToHexString(request) != "0008000205320501B8")
					{
						throw new InvalidOperationException("Generated packet did not match the decompiled old Gigabyte packet.");
					}
					stream.SetFeature(request);
					Thread.Sleep(750);
					byte[] readback = Query3(136, 0, 500);
					report2.AppendLine();
					report2.AppendLine("## Request and immediate global readback");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
					handler2.AppendLiteral("- Request sent: `");
					handler2.AppendFormatted(Convert.ToHexString(request));
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Readback: `");
					handler2.AppendFormatted(Convert.ToHexString(readback));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(69, 5, stringBuilder6);
					handler2.AppendLiteral("- Decoded: effect `");
					handler2.AppendFormatted(readback[3]);
					handler2.AppendLiteral("`, speed `");
					handler2.AppendFormatted(readback[4]);
					handler2.AppendLiteral("`, brightness `");
					handler2.AppendFormatted(readback[5]);
					handler2.AppendLiteral("`, color `");
					handler2.AppendFormatted(readback[6]);
					handler2.AppendLiteral("`, direction `");
					handler2.AppendFormatted(readback[7]);
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
				}
				catch (Exception ex)
				{
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-old-default-pulse-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		void RunKeyboardPictureMatrixProbe()
		{
			int requestedSlot = Math.Clamp(ReadPositiveIntArgument("--slot", 1) - 1, 0, 4);
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS keyboard picture-matrix probe");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input report`");
			report2.AppendLine("- Official Gigabyte device class: `ITE / ZoneRgb / 3a4041`");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(57, 1, stringBuilder6);
			handler2.AppendLiteral("- Official query command: `0x");
			handler2.AppendFormatted((byte)146, "X2");
			handler2.AppendLiteral("` (`LoadPictureMatrixValue`)");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(53, 3, stringBuilder6);
			handler2.AppendLiteral("- Requested custom slot: `");
			handler2.AppendFormatted(requestedSlot);
			handler2.AppendLiteral("` (effect enum `");
			handler2.AppendFormatted(51 + requestedSlot);
			handler2.AppendLiteral("`, Custom ");
			handler2.AppendFormatted(requestedSlot + 1);
			handler2.AppendLiteral(")");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- Setter `0x12` implemented: **no**");
			report2.AppendLine("- Picture matrix, key matrix, macros, RGB zones, firmware, BIOS, and EC written: **no**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Serial number recorded: **no**");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9 && candidate.GetMaxInputReportLength() == 65);
			if (device2 == null)
			{
				report2.AppendLine("- Exact approved interface was not found; no command was sent.");
				WriteReport();
				Environment.ExitCode = 4;
			}
			else
			{
				try
				{
					using HidStream stream = device2.Open();
					stream.ReadTimeout = 2500;
					byte[] request = new byte[9];
					request[1] = 146;
					request[2] = 0;
					request[3] = (byte)requestedSlot;
					request[8] = CalculateGigabyteChecksum(request);
					stream.SetFeature(request);
					byte[] handshake = new byte[9];
					stream.GetFeature(handshake);
					report2.AppendLine("## Handshake");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
					handler2.AppendLiteral("- Request: `");
					handler2.AppendFormatted(Convert.ToHexString(request));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(50, 1, stringBuilder6);
					handler2.AppendLiteral("- Feature response (read into a zeroed buffer): `");
					handler2.AppendFormatted(Convert.ToHexString(handshake));
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
					report2.AppendLine();
					byte[] payload = new byte[512];
					int blocksReceived = 0;
					string transferError = null;
					List<string> blockHashes = new List<string>();
					for (int block = 0; block < 8; block++)
					{
						byte[] input = new byte[65];
						int received;
						try
						{
							received = stream.Read(input, 0, input.Length);
						}
						catch (TimeoutException)
						{
							transferError = $"Block {block + 1} timed out after 2500 ms.";
							break;
						}
						if (received != 65)
						{
							transferError = $"Block {block + 1} returned {received} bytes instead of {65}.";
							break;
						}
						input.AsSpan(1, 64).CopyTo(payload.AsSpan(block * 64, 64));
						blockHashes.Add(Convert.ToHexString(SHA256.HashData(input.AsSpan(1, 64))));
						blocksReceived++;
						Thread.Sleep(25);
					}
					report2.AppendLine("## Transfer result");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
					handler2.AppendLiteral("- Blocks received: `");
					handler2.AppendFormatted(blocksReceived);
					handler2.AppendLiteral(" / ");
					handler2.AppendFormatted(8);
					handler2.AppendLiteral("`");
					stringBuilder12.AppendLine(ref handler2);
					if (transferError != null)
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
						handler2.AppendLiteral("- Transfer stopped: ");
						handler2.AppendFormatted(Escape(transferError));
						stringBuilder13.AppendLine(ref handler2);
					}
					if (blocksReceived > 0)
					{
						int validBytes = blocksReceived * 64;
						Span<byte> valid = payload.AsSpan(0, validBytes);
						int nonZero = validBytes - ((ReadOnlySpan<byte>)valid).Count((byte)0);
						int distinct = valid.ToArray().Distinct().Count();
						stringBuilder6 = report2;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
						handler2.AppendLiteral("- Payload bytes received: `");
						handler2.AppendFormatted(validBytes);
						handler2.AppendLiteral("`");
						stringBuilder14.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder15 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
						handler2.AppendLiteral("- Payload SHA-256: `");
						handler2.AppendFormatted(Convert.ToHexString(SHA256.HashData(valid)));
						handler2.AppendLiteral("`");
						stringBuilder15.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder16 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(23, 2, stringBuilder6);
						handler2.AppendLiteral("- Non-zero bytes: `");
						handler2.AppendFormatted(nonZero);
						handler2.AppendLiteral(" / ");
						handler2.AppendFormatted(validBytes);
						handler2.AppendLiteral("`");
						stringBuilder16.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder17 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(26, 1, stringBuilder6);
						handler2.AppendLiteral("- Distinct byte values: `");
						handler2.AppendFormatted(distinct);
						handler2.AppendLiteral("`");
						stringBuilder17.AppendLine(ref handler2);
						report2.AppendLine();
						report2.AppendLine("### Per-block payload hashes");
						report2.AppendLine();
						for (int block2 = 0; block2 < blockHashes.Count; block2++)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder18 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(12, 2, stringBuilder6);
							handler2.AppendLiteral("- Block ");
							handler2.AppendFormatted(block2 + 1);
							handler2.AppendLiteral(": `");
							handler2.AppendFormatted(blockHashes[block2]);
							handler2.AppendLiteral("`");
							stringBuilder18.AppendLine(ref handler2);
						}
						report2.AppendLine();
						stringBuilder6 = report2;
						StringBuilder stringBuilder19 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder6);
						handler2.AppendLiteral("## Raw ");
						handler2.AppendFormatted(validBytes);
						handler2.AppendLiteral("-byte payload");
						stringBuilder19.AppendLine(ref handler2);
						report2.AppendLine();
						report2.AppendLine("```text");
						for (int offset = 0; offset < validBytes; offset += 16)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder20 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 2, stringBuilder6);
							handler2.AppendFormatted(offset, "X3");
							handler2.AppendLiteral(": ");
							handler2.AppendFormatted(Convert.ToHexString(valid.Slice(offset, 16)));
							stringBuilder20.AppendLine(ref handler2);
						}
						report2.AppendLine("```");
					}
					report2.AppendLine();
					report2.AppendLine("## Interpretation boundary");
					report2.AppendLine();
					report2.AppendLine("- A timeout on block 1 means firmware 19.0.4 does not answer `0x92` on this device; the picture-matrix path would then be closed for `7A41`.");
					report2.AppendLine("- An all-zero payload proves the command is answered but the slot is empty; it does not prove the slot is writable.");
					report2.AppendLine("- Structured non-zero data would indicate a usable second lighting layer and justify a separate guarded write design.");
					report2.AppendLine("- Gigabyte's signed module uses 512 of the declared 960 `PictureMatrix` bytes; only those 512 are requested here.");
				}
				catch (Exception ex2)
				{
					report2.AppendLine("## Probe failure");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder21 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
					handler2.AppendLiteral("- ");
					handler2.AppendFormatted(Escape(ex2.Message));
					stringBuilder21.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteReport();
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-picture-matrix-probe-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		static void RunKeyboardRgbQuery()
		{
			StringBuilder rgbReport = new StringBuilder();
			rgbReport.AppendLine("# AORUS keyboard RGB query");
			rgbReport.AppendLine();
			StringBuilder stringBuilder6 = rgbReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			rgbReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			rgbReport.AppendLine("- Official Gigabyte query commands sent with `SET_FEATURE`: **yes (`0x80` firmware, `0x88` lighting)**");
			rgbReport.AppendLine("- State-changing Gigabyte command sent in this mode: **no**");
			rgbReport.AppendLine("- Output report sent: **no**");
			rgbReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				rgbReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteKeyboardRgbReport(rgbReport);
			}
			else
			{
				try
				{
					HidStream stream = device2.Open();
					try
					{
						byte[] firmware = Query3(128, 0, 10);
						string firmwarePart = firmware[3].ToString(CultureInfo.InvariantCulture);
						string firmwareVersion = ((firmwarePart.Length == 1) ? $"{firmware[2]}.0.{firmwarePart}" : $"{firmware[2]}.{firmwarePart[0]}.{firmwarePart[1]}");
						rgbReport.AppendLine("## Keyboard firmware");
						rgbReport.AppendLine();
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
						handler2.AppendLiteral("- Raw response: `");
						handler2.AppendFormatted(Convert.ToHexString(firmware));
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(32, 1, stringBuilder6);
						handler2.AppendLiteral("- Gigabyte-formatted version: `");
						handler2.AppendFormatted(firmwareVersion);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
						rgbReport.AppendLine();
						byte[] effect = Query3(136, 0, 500);
						rgbReport.AppendLine("## Global lighting state");
						rgbReport.AppendLine();
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
						handler2.AppendLiteral("- Raw response: `");
						handler2.AppendFormatted(Convert.ToHexString(effect));
						handler2.AppendLiteral("`");
						stringBuilder10.AppendLine(ref handler2);
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
						handler2.AppendLiteral("- Effect code: `");
						handler2.AppendFormatted(effect[3]);
						handler2.AppendLiteral("` (`0x");
						handler2.AppendFormatted(effect[3], "X2");
						handler2.AppendLiteral("`)");
						stringBuilder11.AppendLine(ref handler2);
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder12 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
						handler2.AppendLiteral("- Speed: `");
						handler2.AppendFormatted(effect[4]);
						handler2.AppendLiteral("`");
						stringBuilder12.AppendLine(ref handler2);
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(102, 2, stringBuilder6);
						handler2.AppendLiteral("- Nominal brightness byte: `");
						handler2.AppendFormatted(effect[5]);
						handler2.AppendLiteral("` (Gigabyte UI scale label: ");
						handler2.AppendFormatted(Math.Min(effect[5] * 2, 100));
						handler2.AppendLiteral("%; not proven as visible PWM on this firmware)");
						stringBuilder13.AppendLine(ref handler2);
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(23, 2, stringBuilder6);
						handler2.AppendLiteral("- Color code: `");
						handler2.AppendFormatted(effect[6]);
						handler2.AppendLiteral("` (`0x");
						handler2.AppendFormatted(effect[6], "X2");
						handler2.AppendLiteral("`)");
						stringBuilder14.AppendLine(ref handler2);
						stringBuilder6 = rgbReport;
						StringBuilder stringBuilder15 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 2, stringBuilder6);
						handler2.AppendLiteral("- Direction code: `");
						handler2.AppendFormatted(effect[7]);
						handler2.AppendLiteral("` (`0x");
						handler2.AppendFormatted(effect[7], "X2");
						handler2.AppendLiteral("`)");
						stringBuilder15.AppendLine(ref handler2);
						rgbReport.AppendLine();
						rgbReport.AppendLine("## Three RGB zones");
						rgbReport.AppendLine();
						for (byte zone2 = 1; zone2 <= 3; zone2++)
						{
							byte[] zoneState = Query3(136, zone2, 65);
							stringBuilder6 = rgbReport;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(9, 1, stringBuilder6);
							handler2.AppendLiteral("### Zone ");
							handler2.AppendFormatted(zone2);
							stringBuilder16.AppendLine(ref handler2);
							rgbReport.AppendLine();
							stringBuilder6 = rgbReport;
							StringBuilder stringBuilder17 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
							handler2.AppendLiteral("- Raw response: `");
							handler2.AppendFormatted(Convert.ToHexString(zoneState));
							handler2.AppendLiteral("`");
							stringBuilder17.AppendLine(ref handler2);
							stringBuilder6 = rgbReport;
							StringBuilder stringBuilder18 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 3, stringBuilder6);
							handler2.AppendLiteral("- RGB: `(");
							handler2.AppendFormatted(zoneState[3]);
							handler2.AppendLiteral(", ");
							handler2.AppendFormatted(zoneState[4]);
							handler2.AppendLiteral(", ");
							handler2.AppendFormatted(zoneState[5]);
							handler2.AppendLiteral(")`");
							stringBuilder18.AppendLine(ref handler2);
							stringBuilder6 = rgbReport;
							StringBuilder stringBuilder19 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 3, stringBuilder6);
							handler2.AppendLiteral("- Hex color: `#");
							handler2.AppendFormatted(zoneState[3], "X2");
							handler2.AppendFormatted(zoneState[4], "X2");
							handler2.AppendFormatted(zoneState[5], "X2");
							handler2.AppendLiteral("`");
							stringBuilder19.AppendLine(ref handler2);
							stringBuilder6 = rgbReport;
							StringBuilder stringBuilder20 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(91, 1, stringBuilder6);
							handler2.AppendLiteral("- Nominal brightness byte: `");
							handler2.AppendFormatted(zoneState[6]);
							handler2.AppendLiteral("` (`50`=on and tested values below `50`=off on firmware 19.0.4)");
							stringBuilder20.AppendLine(ref handler2);
							rgbReport.AppendLine();
						}
					}
					finally
					{
						if (stream != null)
						{
							((IDisposable)stream).Dispose();
						}
					}
					byte[] Query3(byte command, byte selector, int delayMilliseconds)
					{
						byte[] request = new byte[9];
						request[1] = command;
						request[2] = selector;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(delayMilliseconds);
						byte[] response = new byte[9];
						stream.GetFeature(response);
						return response;
					}
				}
				catch (Exception ex)
				{
					rgbReport.AppendLine("## Query failure");
					rgbReport.AppendLine();
					stringBuilder6 = rgbReport;
					StringBuilder stringBuilder21 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
					handler2.AppendLiteral("- ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder21.AppendLine(ref handler2);
				}
				rgbReport.AppendLine("## Interpretation boundary");
				rgbReport.AppendLine();
				rgbReport.AppendLine("- Byte meanings come from Gigabyte's signed `GBT_Keyboard 25.07.25.01` implementation for this exact USB identity.");
				rgbReport.AppendLine("- Official enum mappings are documented in `research/KEYBOARD-CAPABILITIES.md`; the all-zero global response is outside the defined effect enum and is therefore reported without guessing.");
				WriteKeyboardRgbReport(rgbReport);
			}
		}
		static void RunKeyboardSlowColorCycle()
		{
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS slow full-color-cycle request");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Combined request: Neon/Cycle effect `8`, slowest raw speed `9`, brightness `50`, Random palette `8`, direction `1`");
			report2.AppendLine("- Persistence: effect request deliberately left active for visual observation; static zone values were not overwritten");
			report2.AppendLine("- Key matrix, macros, firmware, BIOS, EC, and battery modified: **no**");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				try
				{
					report2.AppendLine("## Static zone state before request");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] zoneState = Query3(136, zone2, 65);
						stringBuilder6 = report2;
						StringBuilder stringBuilder8 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(zoneState[3], "X2");
						handler2.AppendFormatted(zoneState[4], "X2");
						handler2.AppendFormatted(zoneState[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(zoneState[6]);
						handler2.AppendLiteral("`");
						stringBuilder8.AppendLine(ref handler2);
					}
					byte[] request = new byte[9];
					request[1] = 8;
					request[2] = 0;
					request[3] = 8;
					request[4] = 9;
					request[5] = 50;
					request[6] = 8;
					request[7] = 1;
					request[8] = CalculateGigabyteChecksum(request);
					stream.SetFeature(request);
					Thread.Sleep(750);
					byte[] readback = Query3(136, 0, 500);
					report2.AppendLine();
					report2.AppendLine("## Request and immediate global readback");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
					handler2.AppendLiteral("- Request: `");
					handler2.AppendFormatted(Convert.ToHexString(request));
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Readback: `");
					handler2.AppendFormatted(Convert.ToHexString(readback));
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(69, 5, stringBuilder6);
					handler2.AppendLiteral("- Decoded: effect `");
					handler2.AppendFormatted(readback[3]);
					handler2.AppendLiteral("`, speed `");
					handler2.AppendFormatted(readback[4]);
					handler2.AppendLiteral("`, brightness `");
					handler2.AppendFormatted(readback[5]);
					handler2.AppendLiteral("`, color `");
					handler2.AppendFormatted(readback[6]);
					handler2.AppendLiteral("`, direction `");
					handler2.AppendFormatted(readback[7]);
					handler2.AppendLiteral("`");
					stringBuilder11.AppendLine(ref handler2);
				}
				catch (Exception ex)
				{
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] Query3(byte command, byte selector, int delayMilliseconds)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(delayMilliseconds);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-slow-color-cycle-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		static void RunKeyboardZoneWriteVerification()
		{
			StringBuilder testReport = new StringBuilder();
			testReport.AppendLine("# AORUS guarded RGB zone write verification");
			testReport.AppendLine();
			StringBuilder stringBuilder6 = testReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			testReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			testReport.AppendLine("- Scope: zone 1 only; temporary color; original state restored in `finally`");
			testReport.AppendLine("- Key matrix, macros, effects, firmware, BIOS, and EC modified: **no**");
			testReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				testReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteZoneTestReport(testReport);
				return;
			}
			byte[] original = null;
			HidStream stream = device2.Open();
			try
			{
				try
				{
					original = QueryZone2(1);
					byte testRed = original[5];
					byte testGreen = original[4];
					byte testBlue = original[3];
					if (testRed == original[3] && testGreen == original[4] && testBlue == original[5])
					{
						testRed = (byte)(original[3] + 64);
					}
					stringBuilder6 = testReport;
					StringBuilder stringBuilder8 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(30, 4, stringBuilder6);
					handler2.AppendLiteral("- Original: `#");
					handler2.AppendFormatted(original[3], "X2");
					handler2.AppendFormatted(original[4], "X2");
					handler2.AppendFormatted(original[5], "X2");
					handler2.AppendLiteral("`, brightness `");
					handler2.AppendFormatted(original[6]);
					handler2.AppendLiteral("`");
					stringBuilder8.AppendLine(ref handler2);
					stringBuilder6 = testReport;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(36, 4, stringBuilder6);
					handler2.AppendLiteral("- Temporary test: `#");
					handler2.AppendFormatted(testRed, "X2");
					handler2.AppendFormatted(testGreen, "X2");
					handler2.AppendFormatted(testBlue, "X2");
					handler2.AppendLiteral("`, brightness `");
					handler2.AppendFormatted(original[6]);
					handler2.AppendLiteral("`");
					stringBuilder9.AppendLine(ref handler2);
					SetZone(1, testRed, testGreen, testBlue, original[6]);
					Thread.Sleep(350);
					byte[] observed = QueryZone2(1);
					bool applied = observed[3] == testRed && observed[4] == testGreen && observed[5] == testBlue && observed[6] == original[6];
					stringBuilder6 = testReport;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 4, stringBuilder6);
					handler2.AppendLiteral("- Readback during test: `#");
					handler2.AppendFormatted(observed[3], "X2");
					handler2.AppendFormatted(observed[4], "X2");
					handler2.AppendFormatted(observed[5], "X2");
					handler2.AppendLiteral("`, brightness `");
					handler2.AppendFormatted(observed[6]);
					handler2.AppendLiteral("`");
					stringBuilder10.AppendLine(ref handler2);
					stringBuilder6 = testReport;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(32, 1, stringBuilder6);
					handler2.AppendLiteral("- Temporary write verified: **");
					handler2.AppendFormatted(applied ? "yes" : "no");
					handler2.AppendLiteral("**");
					stringBuilder11.AppendLine(ref handler2);
				}
				catch (Exception ex)
				{
					stringBuilder6 = testReport;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder12.AppendLine(ref handler2);
				}
				finally
				{
					if (original != null)
					{
						try
						{
							SetZone(1, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(1);
							bool restoreVerified = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = testReport;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(36, 4, stringBuilder6);
							handler2.AppendLiteral("- Final readback: `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`");
							stringBuilder13.AppendLine(ref handler2);
							stringBuilder6 = testReport;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(44, 1, stringBuilder6);
							handler2.AppendLiteral("- Original state restored and verified: **");
							handler2.AppendFormatted(restoreVerified ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder14.AppendLine(ref handler2);
						}
						catch (Exception ex2)
						{
							stringBuilder6 = testReport;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
							handler2.AppendLiteral("- RESTORE ERROR: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder15.AppendLine(ref handler2);
						}
					}
				}
				WriteZoneTestReport(testReport);
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte b)
			{
				byte[] request = new byte[9];
				request[1] = 136;
				request[2] = b;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
				Thread.Sleep(65);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void SetZone(byte b, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request = new byte[9];
				request[1] = 8;
				request[2] = b;
				request[3] = red;
				request[4] = green;
				request[5] = blue;
				request[6] = brightness;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
				Thread.Sleep(65);
			}
		}
		void RunLiveMonitor()
		{
			int intervalMilliseconds = ReadPositiveIntArgument("--interval-ms", 2000);
			int sampleLimit = ReadPositiveIntArgument("--samples", int.MaxValue);
			bool plainOutput = args.Any((string argument) => argument.Equals("--plain", StringComparison.OrdinalIgnoreCase));
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine(plainOutput ? "AORUS 5 SE - Live-Monitor (read-only)" : "AORUS 5 SE – Live-Monitor (nur lesend)");
			Console.WriteLine();
			if (!IsAdministrator())
			{
				Console.Error.WriteLine("Windows erlaubt die ACPI-Sensorabfrage nur als Administrator.");
				Console.Error.WriteLine("Bitte tools\\Start-AorusMonitor.ps1 starten oder das Programm als Administrator öffnen.");
				Environment.ExitCode = 1;
				return;
			}
			try
			{
				using IAorusTelemetryReader reader = new GigabyteWmiTelemetryReader();
				DeviceCompatibility compatibility = reader.CheckCompatibility();
				if (!compatibility.IsSupported)
				{
					Console.Error.WriteLine("Sicherheitsstopp: " + compatibility.Message);
					Environment.ExitCode = 2;
					return;
				}
				CancellationTokenSource cancellation = new CancellationTokenSource();
				try
				{
					ConsoleCancelEventHandler cancelHandler = delegate(object? _, ConsoleCancelEventArgs eventArgs)
					{
						eventArgs.Cancel = true;
						cancellation.Cancel();
					};
					Console.CancelKeyPress += cancelHandler;
					try
					{
						for (int sample = 1; sample <= sampleLimit; sample++)
						{
							if (cancellation.IsCancellationRequested)
							{
								break;
							}
							TelemetrySnapshot snapshot = reader.ReadAsync(cancellation.Token).GetAwaiter().GetResult();
							string timestamp = snapshot.CapturedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
							if (!plainOutput)
							{
								TryClearConsole();
								Console.WriteLine("AORUS 5 SE – Live-Monitor (nur lesend)");
								Console.WriteLine("BIOS FB0F | Beenden mit Strg+C");
								Console.WriteLine();
								Console.WriteLine($"CPU-Temperatur       {snapshot.CpuTemperatureCelsius,5} °C");
								Console.WriteLine($"GPU-Temperatur       {snapshot.GpuTemperatureCelsius,5} °C");
								Console.WriteLine($"CPU-Lüfter           {snapshot.CpuFanRpm,5} U/min   ({snapshot.CpuFanDutyPercent} %)");
								Console.WriteLine($"GPU-Lüfter           {snapshot.GpuFanRpm,5} U/min   ({snapshot.GpuFanDutyPercent} %)");
								Console.WriteLine();
								Console.WriteLine($"Letzte Messung: {timestamp} | Intervall: {(double)intervalMilliseconds / 1000.0:F1} s");
							}
							else
							{
								Console.WriteLine($"{timestamp} CPU={snapshot.CpuTemperatureCelsius}C GPU={snapshot.GpuTemperatureCelsius}C CPU-Fan={snapshot.CpuFanRpm}RPM/{snapshot.CpuFanDutyPercent}% GPU-Fan={snapshot.GpuFanRpm}RPM/{snapshot.GpuFanDutyPercent}%");
							}
							if (sample < sampleLimit && cancellation.Token.WaitHandle.WaitOne(intervalMilliseconds))
							{
								break;
							}
						}
					}
					finally
					{
						Console.CancelKeyPress -= cancelHandler;
					}
				}
				finally
				{
					if (cancellation != null)
					{
						((IDisposable)cancellation).Dispose();
					}
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("Live-Monitor konnte nicht lesen: " + ex.Message);
				Environment.ExitCode = 5;
			}
		}
		void RunPictureMatrixWriteTest()
		{
			bool num2 = args.Any((string argument) => argument.Equals("--confirm-picture-matrix-write", StringComparison.OrdinalIgnoreCase));
			int slot = Math.Clamp(ReadPositiveIntArgument("--slot", 1) - 1, 0, 4);
			byte customEffect = (byte)(51 + slot);
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS picture-matrix write test");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature / 65-byte Input and Output report`");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(105, 2, stringBuilder6);
			handler2.AppendLiteral("- Official commands used: getter `0x");
			handler2.AppendFormatted((byte)146, "X2");
			handler2.AppendLiteral("`, setter `0x");
			handler2.AppendFormatted((byte)18, "X2");
			handler2.AppendLiteral("`, effect selector `0x08` selector 0, zone getter `0x88`");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(50, 3, stringBuilder6);
			handler2.AppendLiteral("- Target custom slot: `");
			handler2.AppendFormatted(slot);
			handler2.AppendLiteral("` (effect enum `");
			handler2.AppendFormatted(customEffect);
			handler2.AppendLiteral("`, Custom ");
			handler2.AppendFormatted(slot + 1);
			handler2.AppendLiteral(")");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- Written memory: LED profile storage only. Firmware code, key matrix, macros, BIOS, EC, and battery: **not written**");
			report2.AppendLine("- Report ID `0x5A` (ITE flash channel) touched: **no**");
			report2.AppendLine("- Rollback: the slot is read and saved first, then rewritten and verified in `finally`; the three zone colours are restored the same way");
			report2.AppendLine();
			HidStream stream;
			if (!num2)
			{
				Console.Error.WriteLine("Dieser Test schreibt erstmals mit Kommandobyte 0x12 in den LED-Profilspeicher.");
				Console.Error.WriteLine("Er verlangt zusaetzlich --confirm-picture-matrix-write.");
				report2.AppendLine("- Refused before any device access: `--confirm-picture-matrix-write` was not supplied.");
				WriteReport();
				Environment.ExitCode = 2;
			}
			else
			{
				HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice hidDevice) => GetInterfaceLabel(hidDevice.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && hidDevice.GetMaxFeatureReportLength() == 9 && hidDevice.GetMaxInputReportLength() == 65 && hidDevice.GetMaxOutputReportLength() == 65);
				if (device2 != null)
				{
					stream = device2.Open();
					try
					{
						stream.ReadTimeout = 2500;
						byte[] savedMatrix = null;
						Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
						try
						{
							report2.AppendLine("## Saved original state");
							report2.AppendLine();
							savedMatrix = ReadMatrix(slot);
							int savedNonZero = 512 - ((ReadOnlySpan<byte>)savedMatrix).Count((byte)0);
							stringBuilder6 = report2;
							StringBuilder stringBuilder10 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(42, 3, stringBuilder6);
							handler2.AppendLiteral("- Slot ");
							handler2.AppendFormatted(slot);
							handler2.AppendLiteral(" matrix read: `");
							handler2.AppendFormatted(512);
							handler2.AppendLiteral("` bytes, `");
							handler2.AppendFormatted(savedNonZero);
							handler2.AppendLiteral("` non-zero");
							stringBuilder10.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder11 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 2, stringBuilder6);
							handler2.AppendLiteral("- Slot ");
							handler2.AppendFormatted(slot);
							handler2.AppendLiteral(" SHA-256: `");
							handler2.AppendFormatted(Convert.ToHexString(SHA256.HashData(savedMatrix)));
							handler2.AppendLiteral("`");
							stringBuilder11.AppendLine(ref handler2);
							for (byte zone2 = 1; zone2 <= 3; zone2++)
							{
								byte[] state = QueryFeature(136, zone2);
								originalZones.Add(zone2, state);
								stringBuilder6 = report2;
								StringBuilder stringBuilder12 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
								handler2.AppendLiteral("- Zone ");
								handler2.AppendFormatted(zone2);
								handler2.AppendLiteral(": `#");
								handler2.AppendFormatted(state[3], "X2");
								handler2.AppendFormatted(state[4], "X2");
								handler2.AppendFormatted(state[5], "X2");
								handler2.AppendLiteral("`, brightness `");
								handler2.AppendFormatted(state[6]);
								handler2.AppendLiteral("`");
								stringBuilder12.AppendLine(ref handler2);
							}
							byte[] candidate = new byte[512];
							for (int index = 0; index < 128; index++)
							{
								candidate[index * 4] = 0;
								candidate[index * 4 + 1] = byte.MaxValue;
								candidate[index * 4 + 2] = 0;
								candidate[index * 4 + 3] = 0;
							}
							report2.AppendLine();
							report2.AppendLine("## Written matrix");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(65, 1, stringBuilder6);
							handler2.AppendLiteral("- Pattern: all `");
							handler2.AppendFormatted(128);
							handler2.AppendLiteral("` four-byte slots set to `00 FF 00 00` (pure red)");
							stringBuilder13.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(23, 1, stringBuilder6);
							handler2.AppendLiteral("- Candidate SHA-256: `");
							handler2.AppendFormatted(Convert.ToHexString(SHA256.HashData(candidate)));
							handler2.AppendLiteral("`");
							stringBuilder14.AppendLine(ref handler2);
							WriteMatrix(slot, candidate);
							byte[] verification = ReadMatrix(slot);
							bool matrixVerified = ((ReadOnlySpan<byte>)verification.AsSpan()).SequenceEqual((ReadOnlySpan<byte>)candidate);
							int verifiedNonZero = 512 - ((ReadOnlySpan<byte>)verification).Count((byte)0);
							stringBuilder6 = report2;
							StringBuilder stringBuilder15 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(22, 1, stringBuilder6);
							handler2.AppendLiteral("- Readback SHA-256: `");
							handler2.AppendFormatted(Convert.ToHexString(SHA256.HashData(verification)));
							handler2.AppendLiteral("`");
							stringBuilder15.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder16 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(32, 2, stringBuilder6);
							handler2.AppendLiteral("- Readback non-zero bytes: `");
							handler2.AppendFormatted(verifiedNonZero);
							handler2.AppendLiteral(" / ");
							handler2.AppendFormatted(512);
							handler2.AppendLiteral("`");
							stringBuilder16.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder17 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
							handler2.AppendLiteral("- Exact readback match: **");
							handler2.AppendFormatted(matrixVerified ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder17.AppendLine(ref handler2);
							Console.WriteLine(matrixVerified ? $"Die Picture-Matrix in Slot {slot} wurde geschrieben und exakt zurueckgelesen." : $"Die Picture-Matrix wurde geschrieben, aber NICHT exakt zurueckgelesen (nur {verifiedNonZero} von {512} Byte ungleich null).");
							byte[] activation = new byte[9];
							activation[1] = 8;
							activation[2] = 0;
							activation[3] = customEffect;
							activation[4] = 5;
							activation[5] = 50;
							activation[6] = 0;
							activation[7] = 1;
							activation[8] = CalculateGigabyteChecksum(activation);
							stream.SetFeature(activation);
							Thread.Sleep(750);
							byte[] globalReadback = QueryFeature(136, 0);
							report2.AppendLine();
							report2.AppendLine("## Custom effect activation");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder18 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder6);
							handler2.AppendLiteral("- Request: `");
							handler2.AppendFormatted(Convert.ToHexString(activation));
							handler2.AppendLiteral("`");
							stringBuilder18.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder19 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder6);
							handler2.AppendLiteral("- Global readback: `");
							handler2.AppendFormatted(Convert.ToHexString(globalReadback));
							handler2.AppendLiteral("`");
							stringBuilder19.AppendLine(ref handler2);
							stringBuilder6 = report2;
							StringBuilder stringBuilder20 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(70, 5, stringBuilder6);
							handler2.AppendLiteral("- Decoded: effect `");
							handler2.AppendFormatted(globalReadback[3]);
							handler2.AppendLiteral("`, speed `");
							handler2.AppendFormatted(globalReadback[4]);
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(globalReadback[5]);
							handler2.AppendLiteral("`, colour `");
							handler2.AppendFormatted(globalReadback[6]);
							handler2.AppendLiteral("`, direction `");
							handler2.AppendFormatted(globalReadback[7]);
							handler2.AppendLiteral("`");
							stringBuilder20.AppendLine(ref handler2);
							Console.WriteLine();
							Console.WriteLine($"Custom {slot + 1} (Effekt {customEffect}) ist angefordert. Bitte jetzt auf die Tastatur schauen.");
							Console.WriteLine("Erwartung, falls der Pfad funktioniert: alle Tasten leuchten rot, unabhaengig von den Zonenfarben.");
							Console.Write("Beobachtung: ");
							string observation = Console.ReadLine()?.Trim() ?? string.Empty;
							report2.AppendLine();
							report2.AppendLine("## Owner observation");
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder21 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
							handler2.AppendLiteral("- ");
							handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung eingegeben)" : observation));
							stringBuilder21.AppendLine(ref handler2);
						}
						catch (Exception ex)
						{
							Console.Error.WriteLine("Testfehler: " + ex.Message);
							report2.AppendLine();
							stringBuilder6 = report2;
							StringBuilder stringBuilder22 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
							handler2.AppendLiteral("- Test error: ");
							handler2.AppendFormatted(Escape(ex.Message));
							stringBuilder22.AppendLine(ref handler2);
							Environment.ExitCode = 5;
						}
						finally
						{
							report2.AppendLine();
							report2.AppendLine("## Rollback");
							report2.AppendLine();
							if (savedMatrix != null)
							{
								try
								{
									WriteMatrix(slot, savedMatrix);
									bool exact = ((ReadOnlySpan<byte>)ReadMatrix(slot).AsSpan()).SequenceEqual((ReadOnlySpan<byte>)savedMatrix);
									stringBuilder6 = report2;
									StringBuilder stringBuilder23 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(43, 2, stringBuilder6);
									handler2.AppendLiteral("- Slot ");
									handler2.AppendFormatted(slot);
									handler2.AppendLiteral(" matrix rewritten, exact match: **");
									handler2.AppendFormatted(exact ? "yes" : "no");
									handler2.AppendLiteral("**");
									stringBuilder23.AppendLine(ref handler2);
									if (!exact)
									{
										Environment.ExitCode = 6;
									}
								}
								catch (Exception ex2)
								{
									stringBuilder6 = report2;
									StringBuilder stringBuilder24 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder6);
									handler2.AppendLiteral("- MATRIX RESTORE ERROR: ");
									handler2.AppendFormatted(Escape(ex2.Message));
									stringBuilder24.AppendLine(ref handler2);
									Environment.ExitCode = 6;
								}
							}
							else
							{
								report2.AppendLine("- Matrix restore not required; the original slot was never read.");
							}
							foreach (var (zone3, original) in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
							{
								try
								{
									byte[] request = new byte[9];
									request[1] = 8;
									request[2] = zone3;
									request[3] = original[3];
									request[4] = original[4];
									request[5] = original[5];
									request[6] = original[6];
									request[8] = CalculateGigabyteChecksum(request);
									stream.SetFeature(request);
									Thread.Sleep(65);
									byte[] restored = QueryFeature(136, zone3);
									bool exact2 = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
									stringBuilder6 = report2;
									StringBuilder stringBuilder25 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
									handler2.AppendLiteral("- Zone ");
									handler2.AppendFormatted(zone3);
									handler2.AppendLiteral(": `#");
									handler2.AppendFormatted(restored[3], "X2");
									handler2.AppendFormatted(restored[4], "X2");
									handler2.AppendFormatted(restored[5], "X2");
									handler2.AppendLiteral("`, brightness `");
									handler2.AppendFormatted(restored[6]);
									handler2.AppendLiteral("`, exact match: **");
									handler2.AppendFormatted(exact2 ? "yes" : "no");
									handler2.AppendLiteral("**");
									stringBuilder25.AppendLine(ref handler2);
									if (!exact2)
									{
										Environment.ExitCode = 6;
									}
								}
								catch (Exception ex3)
								{
									stringBuilder6 = report2;
									StringBuilder stringBuilder26 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
									handler2.AppendLiteral("- Zone ");
									handler2.AppendFormatted(zone3);
									handler2.AppendLiteral(" restore failed: ");
									handler2.AppendFormatted(Escape(ex3.Message));
									stringBuilder26.AppendLine(ref handler2);
									Environment.ExitCode = 6;
								}
							}
						}
						WriteReport();
						return;
					}
					finally
					{
						if (stream != null)
						{
							((IDisposable)stream).Dispose();
						}
					}
				}
				Console.Error.WriteLine("Die exakt zugelassene Schnittstelle wurde nicht gefunden; es wurde nichts gesendet.");
				report2.AppendLine("- Exact approved interface was not found; no command was sent.");
				WriteReport();
				Environment.ExitCode = 4;
			}
			byte[] QueryFeature(byte command, byte selector)
			{
				byte[] query = new byte[9];
				query[1] = command;
				query[2] = selector;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(65);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			byte[] ReadMatrix(int targetSlot)
			{
				byte[] request2 = new byte[9];
				request2[1] = 146;
				request2[2] = 0;
				request2[3] = (byte)targetSlot;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				byte[] handshake = new byte[9];
				stream.GetFeature(handshake);
				byte[] matrix = new byte[512];
				for (int block = 0; block < 8; block++)
				{
					byte[] input = new byte[65];
					int received = stream.Read(input, 0, input.Length);
					if (received != 65)
					{
						throw new InvalidOperationException($"Matrix block {block + 1} returned {received} bytes instead of {65}.");
					}
					input.AsSpan(1, 64).CopyTo(matrix.AsSpan(block * 64, 64));
					Thread.Sleep(25);
				}
				return matrix;
			}
			void WriteMatrix(int targetSlot, byte[] matrix)
			{
				byte[] request2 = new byte[9];
				request2[1] = 18;
				request2[2] = 0;
				request2[3] = (byte)targetSlot;
				request2[4] = 8;
				request2[5] = 0;
				request2[8] = CalculateGigabyteChecksum(request2);
				stream.SetFeature(request2);
				Thread.Sleep(65);
				for (int block = 0; block < 8; block++)
				{
					byte[] output = new byte[65];
					output[0] = 0;
					matrix.AsSpan(block * 64, 64).CopyTo(output.AsSpan(1, 64));
					stream.Write(output, 0, output.Length);
					Thread.Sleep(100);
				}
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-picture-matrix-write-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine(report2);
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		void RunPowerDrawMonitor()
		{
			int durationSeconds = Math.Clamp(ReadPositiveIntArgument("--seconds", 120), 15, 1800);
			int intervalMilliseconds = Math.Clamp(ReadPositiveIntArgument("--interval-ms", 3000), 1000, 30000);
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Verbrauchsmonitor");
			Console.WriteLine();
			Console.WriteLine($"Laufzeit {durationSeconds} s, Abstand {intervalMilliseconds} ms.");
			Console.WriteLine("Die Entladerate ist nur im AKKUBETRIEB verfuegbar; am Netz meldet Windows 0.");
			Console.WriteLine("Beenden mit Strg+C.");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS system power draw correlation");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Mode: read-only. Passive WMI performance counters only");
			report2.AppendLine("- `nvidia-smi` invoked: **no**, because a single call wakes the discrete GPU and costs about 22 W on this laptop");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(32, 2, stringBuilder6);
			handler2.AppendLiteral("- Duration: `");
			handler2.AppendFormatted(durationSeconds);
			handler2.AppendLiteral("` s, interval `");
			handler2.AppendFormatted(intervalMilliseconds);
			handler2.AppendLiteral("` ms");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Adapter treated as the integrated GPU: LUID `0x0001149C`");
			report2.AppendLine("- Discharge rate is the **total** system draw in milliwatts, not the draw of any single component");
			stringBuilder6 = report2;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(96, 1, stringBuilder6);
			handler2.AppendLiteral("- CPU percentages are normalised across `");
			handler2.AppendFormatted(Environment.ProcessorCount);
			handler2.AppendLiteral("` logical processors, so the total ranges from 0 to 100");
			stringBuilder9.AppendLine(ref handler2);
			report2.AppendLine("- **The monitor influences its own measurement.** Each sample enumerates every process through WMI, which shows up as `WmiPrvSE` load. Interactive sessions and the tool itself are part of the reported draw, so compare samples within one run rather than against an untouched idle machine.");
			report2.AppendLine();
			List<PowerSample> samples = new List<PowerSample>();
			CancellationTokenSource cancellation = new CancellationTokenSource();
			try
			{
				ConsoleCancelEventHandler cancelHandler = delegate(object? _, ConsoleCancelEventArgs eventArgs)
				{
					eventArgs.Cancel = true;
					cancellation.Cancel();
				};
				Console.CancelKeyPress += cancelHandler;
				try
				{
					Stopwatch clock = Stopwatch.StartNew();
					while (clock.Elapsed.TotalSeconds < (double)durationSeconds && !cancellation.IsCancellationRequested)
					{
						PowerSample sample = CapturePowerSample("0x0001149C");
						samples.Add(sample);
						Console.WriteLine($"  {sample.At:HH:mm:ss}  {(double)sample.DischargeMilliwatts / 1000.0,5:F1} W  CPU {sample.CpuPercent,5:F1} %  iGPU {sample.IntegratedGpuPercent,5:F1} %  dGPU {sample.DiscreteGpuPercent,5:F1} %  top: {sample.TopProcesses}");
						if (clock.Elapsed.TotalSeconds < (double)durationSeconds && !cancellation.IsCancellationRequested)
						{
							Thread.Sleep(intervalMilliseconds);
						}
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Monitorfehler: " + ex.Message);
					stringBuilder6 = report2;
					StringBuilder stringBuilder10 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
					handler2.AppendLiteral("- Monitor error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder10.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					Console.CancelKeyPress -= cancelHandler;
				}
				report2.AppendLine("## Samples");
				report2.AppendLine();
				if (samples.Count == 0)
				{
					report2.AppendLine("- No sample was taken.");
				}
				else
				{
					report2.AppendLine("| Time | Draw | CPU | iGPU | dGPU | Top processes by CPU |");
					report2.AppendLine("|---|---|---|---|---|---|");
					foreach (PowerSample sample2 in samples)
					{
						stringBuilder6 = report2;
						StringBuilder stringBuilder11 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(37, 6, stringBuilder6);
						handler2.AppendLiteral("| `");
						handler2.AppendFormatted(sample2.At, "HH:mm:ss");
						handler2.AppendLiteral("` | `");
						handler2.AppendFormatted((double)sample2.DischargeMilliwatts / 1000.0, "F1");
						handler2.AppendLiteral("` W | ");
						handler2.AppendLiteral("`");
						handler2.AppendFormatted(sample2.CpuPercent, "F1");
						handler2.AppendLiteral("` % | `");
						handler2.AppendFormatted(sample2.IntegratedGpuPercent, "F1");
						handler2.AppendLiteral("` % | ");
						handler2.AppendLiteral("`");
						handler2.AppendFormatted(sample2.DiscreteGpuPercent, "F1");
						handler2.AppendLiteral("` % | ");
						handler2.AppendFormatted(Escape(sample2.TopProcesses));
						handler2.AppendLiteral(" |");
						stringBuilder11.AppendLine(ref handler2);
					}
					PowerSample[] onBattery = samples.Where((PowerSample powerSample) => powerSample.DischargeMilliwatts != 0).ToArray();
					report2.AppendLine();
					report2.AppendLine("## Summary");
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder12 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(58, 2, stringBuilder6);
					handler2.AppendLiteral("- Samples: `");
					handler2.AppendFormatted(samples.Count);
					handler2.AppendLiteral("`, of which `");
					handler2.AppendFormatted(onBattery.Length);
					handler2.AppendLiteral("` carried a usable discharge rate");
					stringBuilder12.AppendLine(ref handler2);
					if (onBattery.Length == 0)
					{
						report2.AppendLine("- Every sample reported `0` mW, so the machine was on AC for the whole run. Repeat on battery to obtain power figures; the CPU and GPU columns remain valid.");
					}
					else
					{
						double minimum = (double)onBattery.Min((PowerSample powerSample) => powerSample.DischargeMilliwatts) / 1000.0;
						double maximum = (double)onBattery.Max((PowerSample powerSample) => powerSample.DischargeMilliwatts) / 1000.0;
						double average = onBattery.Average((PowerSample powerSample) => powerSample.DischargeMilliwatts) / 1000.0;
						stringBuilder6 = report2;
						StringBuilder stringBuilder13 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(54, 3, stringBuilder6);
						handler2.AppendLiteral("- Total draw: minimum `");
						handler2.AppendFormatted(minimum, "F1");
						handler2.AppendLiteral("` W, average `");
						handler2.AppendFormatted(average, "F1");
						handler2.AppendLiteral("` W, maximum `");
						handler2.AppendFormatted(maximum, "F1");
						handler2.AppendLiteral("` W");
						stringBuilder13.AppendLine(ref handler2);
						stringBuilder6 = report2;
						StringBuilder stringBuilder14 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(50, 1, stringBuilder6);
						handler2.AppendLiteral("- Spread between quietest and busiest sample: `");
						handler2.AppendFormatted(maximum - minimum, "F1");
						handler2.AppendLiteral("` W");
						stringBuilder14.AppendLine(ref handler2);
						PowerSample peak = onBattery.OrderByDescending((PowerSample powerSample) => powerSample.DischargeMilliwatts).First();
						stringBuilder6 = report2;
						StringBuilder stringBuilder15 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(65, 6, stringBuilder6);
						handler2.AppendLiteral("- Busiest sample `");
						handler2.AppendFormatted(peak.At, "HH:mm:ss");
						handler2.AppendLiteral("` at `");
						handler2.AppendFormatted((double)peak.DischargeMilliwatts / 1000.0, "F1");
						handler2.AppendLiteral("` W with ");
						handler2.AppendLiteral("CPU `");
						handler2.AppendFormatted(peak.CpuPercent, "F1");
						handler2.AppendLiteral("` %, iGPU `");
						handler2.AppendFormatted(peak.IntegratedGpuPercent, "F1");
						handler2.AppendLiteral("` %, dGPU `");
						handler2.AppendFormatted(peak.DiscreteGpuPercent, "F1");
						handler2.AppendLiteral("` %: ");
						handler2.AppendFormatted(Escape(peak.TopProcesses));
						stringBuilder15.AppendLine(ref handler2);
						bool anyDiscrete = samples.Any((PowerSample powerSample) => powerSample.DiscreteGpuPercent > 0.1);
						report2.AppendLine(anyDiscrete ? "- The discrete GPU showed activity in at least one sample, so it was awake during the run." : "- **The discrete GPU showed no activity in any sample.** The observed spread therefore comes from CPU and application load, not from the RTX.");
					}
					report2.AppendLine();
					report2.AppendLine("## Interpretation boundary");
					report2.AppendLine();
					report2.AppendLine("- The discharge rate covers the whole machine: panel, CPU, RAM, storage, radios and every running application.");
					report2.AppendLine("- A GPU engine percentage is a utilisation figure, not a power figure. Zero utilisation does not prove the adapter is powered down, only that nothing is rendering on it.");
					report2.AppendLine("- Per-process CPU values are not normalised; a single process can exceed 100 when it spans several cores.");
					report2.AppendLine("- The integrated adapter is identified by LUID `0x0001149C`, inferred from the desktop compositor running on the internal panel.");
				}
				WriteReport();
			}
			finally
			{
				if (cancellation != null)
				{
					((IDisposable)cancellation).Dispose();
				}
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"power-draw-monitor-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine();
				Console.WriteLine("Report written to: " + outputPath2);
			}
		}
		static void RunSetKeyboardGreen()
		{
			StringBuilder setReport = new StringBuilder();
			setReport.AppendLine("# AORUS keyboard persistent green setting");
			setReport.AppendLine();
			StringBuilder stringBuilder6 = setReport;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			setReport.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			setReport.AppendLine("- Requested color: `#00FF00` on zones 1–3");
			setReport.AppendLine("- Restore after write: **no, explicitly requested by user**");
			setReport.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				setReport.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WritePersistentColorReport(setReport);
			}
			else
			{
				try
				{
					HidStream stream = device2.Open();
					try
					{
						for (byte zone2 = 1; zone2 <= 3; zone2++)
						{
							byte[] before = QueryZone2(zone2);
							SetZone(zone2, 0, byte.MaxValue, 0, before[6]);
							byte[] after = QueryZone2(zone2);
							bool verified = after[3] == 0 && after[4] == byte.MaxValue && after[5] == 0 && after[6] == before[6];
							stringBuilder6 = setReport;
							StringBuilder stringBuilder8 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(60, 9, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone2);
							handler2.AppendLiteral(": before `#");
							handler2.AppendFormatted(before[3], "X2");
							handler2.AppendFormatted(before[4], "X2");
							handler2.AppendFormatted(before[5], "X2");
							handler2.AppendLiteral("`, ");
							handler2.AppendLiteral("after `#");
							handler2.AppendFormatted(after[3], "X2");
							handler2.AppendFormatted(after[4], "X2");
							handler2.AppendFormatted(after[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(after[6]);
							handler2.AppendLiteral("`, ");
							handler2.AppendLiteral("verified **");
							handler2.AppendFormatted(verified ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder8.AppendLine(ref handler2);
						}
					}
					finally
					{
						if (stream != null)
						{
							((IDisposable)stream).Dispose();
						}
					}
					byte[] QueryZone2(byte b)
					{
						byte[] request = new byte[9];
						request[1] = 136;
						request[2] = b;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(65);
						byte[] response = new byte[9];
						stream.GetFeature(response);
						return response;
					}
					void SetZone(byte b, byte red, byte green, byte blue, byte brightness)
					{
						byte[] request = new byte[9];
						request[1] = 8;
						request[2] = b;
						request[3] = red;
						request[4] = green;
						request[5] = blue;
						request[6] = brightness;
						request[8] = CalculateGigabyteChecksum(request);
						stream.SetFeature(request);
						Thread.Sleep(65);
					}
				}
				catch (Exception ex)
				{
					stringBuilder6 = setReport;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder6);
					handler2.AppendLiteral("- Setting failed: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder9.AppendLine(ref handler2);
				}
				WritePersistentColorReport(setReport);
			}
		}
		void RunTemporaryFanProfileTest(string profileSlug)
		{
			string profileName = profileSlug switch
			{
				"quiet" => "Quiet", 
				"gaming" => "Gaming", 
				"maximum" => "Maximum", 
				"dynamic" => "Dynamic", 
				_ => throw new ArgumentOutOfRangeException("profileSlug"), 
			};
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-fan-write", StringComparison.OrdinalIgnoreCase));
			StringBuilder test = new StringBuilder();
			StringBuilder stringBuilder6 = test;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(35, 1, stringBuilder6);
			handler2.AppendLiteral("# AORUS temporary ");
			handler2.AppendFormatted(profileName);
			handler2.AppendLiteral(" fan-profile test");
			stringBuilder7.AppendLine(ref handler2);
			test.AppendLine();
			stringBuilder6 = test;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = test;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(60, 1, stringBuilder6);
			handler2.AppendLiteral("- Requested test: ");
			handler2.AppendFormatted(profileName);
			handler2.AppendLiteral(", five samples, mandatory return to Normal");
			stringBuilder9.AppendLine(ref handler2);
			stringBuilder6 = test;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
			handler2.AppendLiteral("- Explicit write confirmation present: ");
			handler2.AppendFormatted(confirmed ? "yes" : "no");
			stringBuilder10.AppendLine(ref handler2);
			test.AppendLine("- Windows power and GPU power configuration: not changed");
			test.AppendLine();
			if (!confirmed)
			{
				test.AppendLine("- Refused before opening the setter: `--confirm-fan-write` is required.");
				test.AppendLine("- Firmware/EC write methods invoked: **no**");
				WriteTemporaryFanTestReport(test, profileSlug);
				Environment.ExitCode = 2;
				return;
			}
			using IAorusFanController controller = new GigabyteWmiFanController();
			bool quietWasVerified = false;
			FanControlState originalState = null;
			try
			{
				FanControlState before = controller.ReadAsync().GetAwaiter().GetResult();
				originalState = before;
				if (before.FixedStatusRaw != 0 || before.StepStatusRaw != 0 || before.AutoStatusRaw != 0 || before.NvidiaThermalTargetRaw != 0)
				{
					throw new AorusFanControlException("Der Test startet nur aus dem verifizierten Normalzustand.");
				}
				FanProfileChangeResult selected = profileSlug switch
				{
					"quiet" => controller.SetQuietAsync().GetAwaiter().GetResult(), 
					"gaming" => controller.SetGamingAsync().GetAwaiter().GetResult(), 
					"maximum" => controller.SetMaximumAsync().GetAwaiter().GetResult(), 
					"dynamic" => controller.SetDynamicAsync().GetAwaiter().GetResult(), 
					_ => throw new ArgumentOutOfRangeException("profileSlug"), 
				};
				quietWasVerified = true;
				stringBuilder6 = test;
				StringBuilder stringBuilder11 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(12, 1, stringBuilder6);
				handler2.AppendLiteral("## ");
				handler2.AppendFormatted(profileName);
				handler2.AppendLiteral(" readback");
				stringBuilder11.AppendLine(ref handler2);
				test.AppendLine();
				AppendFanState(test, "Before", selected.OriginalState);
				AppendFanState(test, profileName, selected.VerifiedState);
				stringBuilder6 = test;
				StringBuilder stringBuilder12 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 1, stringBuilder6);
				handler2.AppendLiteral("- Curve preserved exactly: ");
				handler2.AppendFormatted(selected.OriginalState.Curve.SequenceEqual(selected.VerifiedState.Curve) ? "yes" : "no");
				stringBuilder12.AppendLine(ref handler2);
				test.AppendLine();
				stringBuilder6 = test;
				StringBuilder stringBuilder13 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(29, 1, stringBuilder6);
				handler2.AppendLiteral("## Telemetry while ");
				handler2.AppendFormatted(profileName);
				handler2.AppendLiteral(" is active");
				stringBuilder13.AppendLine(ref handler2);
				test.AppendLine();
				using IAorusTelemetryReader telemetry = new GigabyteWmiTelemetryReader();
				for (int sample = 1; sample <= 5; sample++)
				{
					TelemetrySnapshot value = telemetry.ReadAsync().GetAwaiter().GetResult();
					stringBuilder6 = test;
					StringBuilder stringBuilder14 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(75, 8, stringBuilder6);
					handler2.AppendLiteral("- Sample ");
					handler2.AppendFormatted(sample);
					handler2.AppendLiteral(" at ");
					handler2.AppendFormatted(value.CapturedAt, "HH:mm:ss");
					handler2.AppendLiteral(": ");
					handler2.AppendLiteral("CPU ");
					handler2.AppendFormatted(value.CpuTemperatureCelsius);
					handler2.AppendLiteral(" °C, GPU ");
					handler2.AppendFormatted(value.GpuTemperatureCelsius);
					handler2.AppendLiteral(" °C, ");
					handler2.AppendLiteral("CPU ");
					handler2.AppendFormatted(value.CpuFanRpm);
					handler2.AppendLiteral(" RPM / raw duty ");
					handler2.AppendFormatted(value.CpuFanDutyPercent);
					handler2.AppendLiteral(", ");
					handler2.AppendLiteral("GPU ");
					handler2.AppendFormatted(value.GpuFanRpm);
					handler2.AppendLiteral(" RPM / raw duty ");
					handler2.AppendFormatted(value.GpuFanDutyPercent);
					stringBuilder14.AppendLine(ref handler2);
					if (sample < 5)
					{
						Thread.Sleep(TimeSpan.FromSeconds(3L));
					}
				}
			}
			catch (Exception ex)
			{
				test.AppendLine();
				test.AppendLine("## Test error");
				test.AppendLine();
				stringBuilder6 = test;
				StringBuilder stringBuilder15 = stringBuilder6;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(2, 1, stringBuilder6);
				handler2.AppendLiteral("- ");
				handler2.AppendFormatted(Escape(ex.Message));
				stringBuilder15.AppendLine(ref handler2);
				Environment.ExitCode = 5;
			}
			finally
			{
				if (quietWasVerified)
				{
					test.AppendLine();
					test.AppendLine("## Mandatory return to Normal");
					test.AppendLine();
					try
					{
						FanProfileChangeResult restored = controller.RestoreAsync(originalState).GetAwaiter().GetResult();
						AppendFanState(test, "Before restore", restored.OriginalState);
						AppendFanState(test, "Verified original", restored.VerifiedState);
						test.AppendLine("- Result: original state restored and verified, including stored fixed speed and GPU duty");
					}
					catch (Exception ex2)
					{
						stringBuilder6 = test;
						StringBuilder stringBuilder16 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(35, 1, stringBuilder6);
						handler2.AppendLiteral("- CRITICAL: Normal restore failed: ");
						handler2.AppendFormatted(Escape(ex2.Message));
						stringBuilder16.AppendLine(ref handler2);
						Environment.ExitCode = 6;
					}
				}
			}
			WriteTemporaryFanTestReport(test, profileSlug);
		}
		static void RunThermalPowerInspection()
		{
			StringBuilder inspection = new StringBuilder();
			inspection.AppendLine("# AORUS thermal, power and GPU capability inspection");
			inspection.AppendLine();
			StringBuilder stringBuilder6 = inspection;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			inspection.AppendLine("- Mode: read-only");
			inspection.AppendLine("- Setter class opened: **no**");
			inspection.AppendLine("- Firmware/EC write methods invoked: **no**");
			inspection.AppendLine();
			string model = GetFirstValue("root\\cimv2", "SELECT Model FROM Win32_ComputerSystem", "Model");
			string bios = GetFirstValue("root\\cimv2", "SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
			inspection.AppendLine("## Compatibility gate");
			inspection.AppendLine();
			stringBuilder6 = inspection;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Model: `");
			handler2.AppendFormatted(Escape(model));
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			stringBuilder6 = inspection;
			StringBuilder stringBuilder9 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 1, stringBuilder6);
			handler2.AppendLiteral("- BIOS: `");
			handler2.AppendFormatted(Escape(bios));
			handler2.AppendLiteral("`");
			stringBuilder9.AppendLine(ref handler2);
			stringBuilder6 = inspection;
			StringBuilder stringBuilder10 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 1, stringBuilder6);
			handler2.AppendLiteral("- Administrator: ");
			handler2.AppendFormatted(IsAdministrator() ? "yes" : "no");
			stringBuilder10.AppendLine(ref handler2);
			if (!model.Equals("AORUS 5 SE", StringComparison.OrdinalIgnoreCase) || !bios.Equals("FB0F", StringComparison.OrdinalIgnoreCase))
			{
				inspection.AppendLine("- Result: refused; this inspection is allowlisted only for `AORUS 5 SE / FB0F`.");
				WriteThermalPowerInspectionReport(inspection);
				Environment.ExitCode = 2;
			}
			else
			{
				inspection.AppendLine("- Result: exact model/BIOS match");
				inspection.AppendLine();
				inspection.AppendLine("## Windows power state");
				inspection.AppendLine();
				AppendCommandOutput(inspection, "Active power scheme", "powercfg.exe", "/getactivescheme");
				AppendRegistryValue(inspection, "SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes", "ActiveOverlayAcPowerScheme");
				AppendRegistryValue(inspection, "SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes", "ActiveOverlayDcPowerScheme");
				inspection.AppendLine();
				inspection.AppendLine("## Windows display and GPU inventory");
				inspection.AppendLine();
				AppendQueryRows(inspection, "root\\cimv2", "SELECT Name, Status, DriverVersion, AdapterCompatibility, PNPDeviceID FROM Win32_VideoController", new string[5] { "Name", "Status", "DriverVersion", "AdapterCompatibility", "PNPDeviceID" });
				AppendQueryRows(inspection, "root\\cimv2", "SELECT Name, Status, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%NVIDIA%'", new string[3] { "Name", "Status", "PNPDeviceID" });
				AppendQueryRows(inspection, "root\\wmi", "SELECT Active, InstanceName FROM WmiMonitorID", new string[2] { "Active", "InstanceName" });
				inspection.AppendLine();
				inspection.AppendLine("## NVIDIA runtime (read-only)");
				inspection.AppendLine();
				string nvidiaSmi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "nvidia-smi.exe");
				if (File.Exists(nvidiaSmi))
				{
					AppendCommandOutput(inspection, "GPU state", nvidiaSmi, "--query-gpu=name,pstate,power.draw,display_active,display_mode,temperature.gpu,fan.speed --format=csv,noheader");
					AppendCommandOutput(inspection, "GPU processes", nvidiaSmi, "--query-compute-apps=pid,process_name,used_memory --format=csv,noheader");
				}
				else
				{
					inspection.AppendLine("- `nvidia-smi.exe`: not found");
				}
				inspection.AppendLine();
				inspection.AppendLine("## Firmware getter state");
				inspection.AppendLine();
				if (!IsAdministrator())
				{
					inspection.AppendLine("- Firmware read refused: administrator rights are required for the ACPI-WMI invocation.");
					WriteThermalPowerInspectionReport(inspection);
					Environment.ExitCode = 1;
				}
				else
				{
					string[] approvedSimpleGetters = new string[36]
					{
						"getCpuTemp", "getGpuTemp1", "getGpuTemp2", "getRpm1", "getRpm2", "GetCPUFanDuty", "GetGPUFanDuty", "GetFixedFanStatus", "GetFixedFanSpeed", "GetFanAdjustStatus",
						"GetAutoFanStatus", "GetStepFanStatus", "GetFanSpeed", "GetNvPowerConfig", "GetNvThermalTarget", "GetPEGorSG", "GetPEG2orSG2", "getAiPowerCtlCapability", "GetDynamicBoostStatus", "GetEcValueBoostStatus",
						"GetSmartCool", "GetSmartTurbo", "GetTurboMode", "GetWhisperMode", "GetTppStatus", "GetSuperQuiet", "GetDeepFan", "GetThermalData", "GetFanHealth", "GetFanPWMStatus",
						"QueryThermalSensor", "GetBatteryTemperature", "GetFan3Duty", "GetFan4Duty", "getRpm3", "getRpm4"
					};
					try
					{
						ManagementClass getClass = new ManagementClass("root\\WMI", "GB_WMIACPI_Get", null);
						try
						{
							getClass.Get();
							Dictionary<string, MethodData> methods = getClass.Methods.Cast<MethodData>().ToDictionary<MethodData, string>((MethodData methodData) => methodData.Name, StringComparer.OrdinalIgnoreCase);
							using ManagementObjectCollection instances = getClass.GetInstances();
							ManagementObject instance = instances.Cast<ManagementObject>().FirstOrDefault();
							try
							{
								if (instance == null)
								{
									inspection.AppendLine("- `GB_WMIACPI_Get` has no live instance.");
									Environment.ExitCode = 3;
									WriteThermalPowerInspectionReport(inspection);
									return;
								}
								stringBuilder6 = inspection;
								StringBuilder stringBuilder11 = stringBuilder6;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder6);
								handler2.AppendLiteral("- Live instance: `");
								handler2.AppendFormatted(Escape(instance.Path.Path));
								handler2.AppendLiteral("`");
								stringBuilder11.AppendLine(ref handler2);
								string[] array2 = approvedSimpleGetters;
								foreach (string methodName in array2)
								{
									if (!methods.TryGetValue(methodName, out var method))
									{
										stringBuilder6 = inspection;
										StringBuilder stringBuilder12 = stringBuilder6;
										handler2 = new StringBuilder.AppendInterpolatedStringHandler(34, 1, stringBuilder6);
										handler2.AppendLiteral("- `");
										handler2.AppendFormatted(methodName);
										handler2.AppendLiteral("`: not exposed by installed MOF");
										stringBuilder12.AppendLine(ref handler2);
									}
									else
									{
										string signature = FormatMethodSignature(method);
										try
										{
											ManagementBaseObject input = getClass.GetMethodParameters(methodName);
											try
											{
												ManagementBaseObject output = instance.InvokeMethod(methodName, input, new InvokeMethodOptions
												{
													Timeout = TimeSpan.FromSeconds(2L)
												});
												try
												{
													stringBuilder6 = inspection;
													StringBuilder stringBuilder13 = stringBuilder6;
													handler2 = new StringBuilder.AppendInterpolatedStringHandler(9, 3, stringBuilder6);
													handler2.AppendLiteral("- `");
													handler2.AppendFormatted(methodName);
													handler2.AppendLiteral("` (");
													handler2.AppendFormatted(Escape(signature));
													handler2.AppendLiteral("): ");
													handler2.AppendFormatted(Escape(FormatManagementValues(output)));
													stringBuilder13.AppendLine(ref handler2);
												}
												finally
												{
													((IDisposable)output)?.Dispose();
												}
											}
											finally
											{
												((IDisposable)input)?.Dispose();
											}
										}
										catch (Exception ex)
										{
											stringBuilder6 = inspection;
											StringBuilder stringBuilder14 = stringBuilder6;
											handler2 = new StringBuilder.AppendInterpolatedStringHandler(17, 3, stringBuilder6);
											handler2.AppendLiteral("- `");
											handler2.AppendFormatted(methodName);
											handler2.AppendLiteral("` (");
											handler2.AppendFormatted(Escape(signature));
											handler2.AppendLiteral("): error (");
											handler2.AppendFormatted(Escape(ex.Message));
											handler2.AppendLiteral(")");
											stringBuilder14.AppendLine(ref handler2);
										}
									}
								}
								inspection.AppendLine();
								inspection.AppendLine("## Repeated thermal samples");
								inspection.AppendLine();
								inspection.AppendLine("RPM values are decoded with the byte order already established by the existing telemetry reader.");
								inspection.AppendLine();
								string[] repeatedMethods = new string[9] { "getCpuTemp", "getGpuTemp1", "getRpm1", "getRpm2", "GetCPUFanDuty", "GetGPUFanDuty", "GetFixedFanSpeed", "GetFanAdjustStatus", "GetFanPWMStatus" };
								if (repeatedMethods.All(methods.ContainsKey))
								{
									for (int sample = 1; sample <= 3; sample++)
									{
										ushort cpuTemperature = InvokeUInt16GetterUnchecked(instance, getClass, "getCpuTemp");
										ushort gpuTemperature = InvokeUInt16GetterUnchecked(instance, getClass, "getGpuTemp1");
										ushort cpuRpmRaw = InvokeUInt16GetterUnchecked(instance, getClass, "getRpm1");
										ushort gpuRpmRaw = InvokeUInt16GetterUnchecked(instance, getClass, "getRpm2");
										ushort cpuDutyRaw = InvokeUInt16GetterUnchecked(instance, getClass, "GetCPUFanDuty");
										ushort gpuDutyRaw = InvokeUInt16GetterUnchecked(instance, getClass, "GetGPUFanDuty");
										ushort cpuRpm = (ushort)((cpuRpmRaw >> 8) | (cpuRpmRaw << 8));
										ushort gpuRpm = (ushort)((gpuRpmRaw >> 8) | (gpuRpmRaw << 8));
										stringBuilder6 = inspection;
										StringBuilder stringBuilder15 = stringBuilder6;
										handler2 = new StringBuilder.AppendInterpolatedStringHandler(152, 13, stringBuilder6);
										handler2.AppendLiteral("- Sample ");
										handler2.AppendFormatted(sample);
										handler2.AppendLiteral(" at ");
										handler2.AppendFormatted(DateTimeOffset.Now, "HH:mm:ss");
										handler2.AppendLiteral(": ");
										handler2.AppendLiteral("CPU ");
										handler2.AppendFormatted(cpuTemperature);
										handler2.AppendLiteral(" °C, GPU ");
										handler2.AppendFormatted(gpuTemperature);
										handler2.AppendLiteral(" °C, ");
										handler2.AppendLiteral("CPU fan raw ");
										handler2.AppendFormatted(cpuRpmRaw);
										handler2.AppendLiteral(" / ");
										handler2.AppendFormatted(cpuRpm);
										handler2.AppendLiteral(" RPM, ");
										handler2.AppendLiteral("GPU fan raw ");
										handler2.AppendFormatted(gpuRpmRaw);
										handler2.AppendLiteral(" / ");
										handler2.AppendFormatted(gpuRpm);
										handler2.AppendLiteral(" RPM, ");
										handler2.AppendLiteral("CPU duty raw ");
										handler2.AppendFormatted(cpuDutyRaw);
										handler2.AppendLiteral(", GPU duty raw ");
										handler2.AppendFormatted(gpuDutyRaw);
										handler2.AppendLiteral(", ");
										handler2.AppendLiteral("fixed-speed raw ");
										handler2.AppendFormatted(InvokeUInt16GetterUnchecked(instance, getClass, "GetFixedFanSpeed"));
										handler2.AppendLiteral(", ");
										handler2.AppendLiteral("fan-adjust raw ");
										handler2.AppendFormatted(InvokeUInt16GetterUnchecked(instance, getClass, "GetFanAdjustStatus"));
										handler2.AppendLiteral(", ");
										handler2.AppendLiteral("fan-pwm raw ");
										handler2.AppendFormatted(InvokeUInt16GetterUnchecked(instance, getClass, "GetFanPWMStatus"));
										stringBuilder15.AppendLine(ref handler2);
										if (sample < 3)
										{
											Thread.Sleep(TimeSpan.FromSeconds(2L));
										}
									}
								}
								else
								{
									string[] missing = repeatedMethods.Where((string key2) => !methods.ContainsKey(key2)).ToArray();
									stringBuilder6 = inspection;
									StringBuilder stringBuilder16 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(51, 1, stringBuilder6);
									handler2.AppendLiteral("- Repeated sampling skipped; missing getter(s): `");
									handler2.AppendFormatted(string.Join("`, `", missing));
									handler2.AppendLiteral("`.");
									stringBuilder16.AppendLine(ref handler2);
								}
								inspection.AppendLine();
								inspection.AppendLine("## Stored 15-point fan curve");
								inspection.AppendLine();
								if (!methods.TryGetValue("GetFanIndexValue", out var curveMethod))
								{
									inspection.AppendLine("- `GetFanIndexValue`: not exposed by installed MOF");
								}
								else
								{
									stringBuilder6 = inspection;
									StringBuilder stringBuilder17 = stringBuilder6;
									handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder6);
									handler2.AppendLiteral("- Signature: `");
									handler2.AppendFormatted(Escape(FormatMethodSignature(curveMethod)));
									handler2.AppendLiteral("`");
									stringBuilder17.AppendLine(ref handler2);
									for (byte index = 0; index < 15; index++)
									{
										try
										{
											ManagementBaseObject input2 = getClass.GetMethodParameters("GetFanIndexValue");
											try
											{
												input2["Index"] = index;
												ManagementBaseObject output2 = instance.InvokeMethod("GetFanIndexValue", input2, new InvokeMethodOptions
												{
													Timeout = TimeSpan.FromSeconds(2L)
												});
												try
												{
													stringBuilder6 = inspection;
													StringBuilder stringBuilder18 = stringBuilder6;
													handler2 = new StringBuilder.AppendInterpolatedStringHandler(10, 2, stringBuilder6);
													handler2.AppendLiteral("- Point ");
													handler2.AppendFormatted(index);
													handler2.AppendLiteral(": ");
													handler2.AppendFormatted(Escape(FormatManagementValues(output2)));
													stringBuilder18.AppendLine(ref handler2);
												}
												finally
												{
													((IDisposable)output2)?.Dispose();
												}
											}
											finally
											{
												((IDisposable)input2)?.Dispose();
											}
										}
										catch (Exception ex2)
										{
											stringBuilder6 = inspection;
											StringBuilder stringBuilder19 = stringBuilder6;
											handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 2, stringBuilder6);
											handler2.AppendLiteral("- Point ");
											handler2.AppendFormatted(index);
											handler2.AppendLiteral(": error (");
											handler2.AppendFormatted(Escape(ex2.Message));
											handler2.AppendLiteral(")");
											stringBuilder19.AppendLine(ref handler2);
										}
									}
								}
							}
							finally
							{
								((IDisposable)instance)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)getClass)?.Dispose();
						}
					}
					catch (Exception ex3)
					{
						stringBuilder6 = inspection;
						StringBuilder stringBuilder20 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(30, 1, stringBuilder6);
						handler2.AppendLiteral("- Firmware inspection failed: ");
						handler2.AppendFormatted(Escape(ex3.Message));
						stringBuilder20.AppendLine(ref handler2);
						Environment.ExitCode = 4;
					}
					WriteThermalPowerInspectionReport(inspection);
				}
			}
		}
		void RunWindowsPowerModeTest()
		{
			bool confirmed = args.Any((string argument) => argument.Equals("--confirm-power-mode-write", StringComparison.OrdinalIgnoreCase));
			StringBuilder test = new StringBuilder();
			test.AppendLine("# Windows power overlay round-trip test");
			test.AppendLine();
			StringBuilder stringBuilder6 = test;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			test.AppendLine("- Scope: Windows power overlay only");
			test.AppendLine("- Gigabyte firmware/EC methods invoked: **no**");
			stringBuilder6 = test;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder6);
			handler2.AppendLiteral("- Explicit write confirmation present: ");
			handler2.AppendFormatted(confirmed ? "yes" : "no");
			stringBuilder8.AppendLine(ref handler2);
			test.AppendLine();
			if (!confirmed)
			{
				test.AppendLine("- Refused before calling the Windows setter: `--confirm-power-mode-write` is required.");
				WriteWindowsPowerModeReport(test);
				Environment.ExitCode = 2;
			}
			else
			{
				WindowsPowerOverlayController controller = new WindowsPowerOverlayController();
				Guid? original = null;
				try
				{
					if (!controller.IsOnAcPower())
					{
						throw new InvalidOperationException("Der Rundlauf wird nur bei angeschlossenem Netzteil ausgeführt.");
					}
					original = controller.ReadActiveForCurrentPowerSource();
					stringBuilder6 = test;
					StringBuilder stringBuilder9 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 2, stringBuilder6);
					handler2.AppendLiteral("- Original AC overlay: `");
					handler2.AppendFormatted(original);
					handler2.AppendLiteral("` (");
					handler2.AppendFormatted(DescribeOverlay(original.Value));
					handler2.AppendLiteral(")");
					stringBuilder9.AppendLine(ref handler2);
					test.AppendLine();
					test.AppendLine("## Round trip");
					test.AppendLine();
					WindowsPowerOverlayMode[] array2 = new WindowsPowerOverlayMode[3]
					{
						WindowsPowerOverlayMode.BestEfficiency,
						WindowsPowerOverlayMode.Balanced,
						WindowsPowerOverlayMode.BestPerformance
					};
					foreach (WindowsPowerOverlayMode mode in array2)
					{
						controller.Set(mode);
						Thread.Sleep(500);
						Guid readback = controller.ReadActiveForCurrentPowerSource();
						Guid expected = mode switch
						{
							WindowsPowerOverlayMode.Balanced => WindowsPowerOverlayController.BalancedGuid, 
							WindowsPowerOverlayMode.BestEfficiency => WindowsPowerOverlayController.BestEfficiencyGuid, 
							WindowsPowerOverlayMode.BestPerformance => WindowsPowerOverlayController.BestPerformanceGuid, 
							_ => throw new ArgumentOutOfRangeException(), 
						};
						stringBuilder6 = test;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 4, stringBuilder6);
						handler2.AppendLiteral("- ");
						handler2.AppendFormatted(mode);
						handler2.AppendLiteral(": expected `");
						handler2.AppendFormatted(expected);
						handler2.AppendLiteral("`, read `");
						handler2.AppendFormatted(readback);
						handler2.AppendLiteral("` — ");
						handler2.AppendFormatted((readback == expected) ? "match" : "MISMATCH");
						stringBuilder10.AppendLine(ref handler2);
						if (readback != expected)
						{
							throw new InvalidOperationException($"Readback mismatch for {mode}.");
						}
					}
				}
				catch (Exception ex)
				{
					test.AppendLine();
					stringBuilder6 = test;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(15, 1, stringBuilder6);
					handler2.AppendLiteral("- Test failed: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder11.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					if (original.HasValue)
					{
						test.AppendLine();
						test.AppendLine("## Restore");
						test.AppendLine();
						try
						{
							controller.Set(original.Value);
							Thread.Sleep(500);
							Guid restored = controller.ReadActiveForCurrentPowerSource();
							stringBuilder6 = test;
							StringBuilder stringBuilder12 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(16, 2, stringBuilder6);
							handler2.AppendLiteral("- Restored `");
							handler2.AppendFormatted(restored);
							handler2.AppendLiteral("` (");
							handler2.AppendFormatted(DescribeOverlay(restored));
							handler2.AppendLiteral(")");
							stringBuilder12.AppendLine(ref handler2);
							stringBuilder6 = test;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 1, stringBuilder6);
							handler2.AppendLiteral("- Exact original restored: ");
							handler2.AppendFormatted((restored == original.Value) ? "yes" : "no");
							stringBuilder13.AppendLine(ref handler2);
							if (restored != original.Value)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = test;
							StringBuilder stringBuilder14 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder6);
							handler2.AppendLiteral("- CRITICAL: restore failed: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder14.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
				}
				WriteWindowsPowerModeReport(test);
			}
		}
		static void RunZoneBrightnessSweep()
		{
			byte[] levels = new byte[10] { 0, 25, 50, 51, 60, 75, 100, 150, 200, 255 };
			Console.OutputEncoding = Encoding.UTF8;
			Console.WriteLine("AORUS 5 SE - Sweep des Zonen-Helligkeitsbytes");
			Console.WriteLine();
			Console.WriteLine("Gigabytes Oberfläche sendet nie mehr als 50, weil sie 0-100 Prozent halbiert.");
			Console.WriteLine("Alles oberhalb von 50 ist deshalb ungetestet. Die Farbe bleibt konstant weiss,");
			Console.WriteLine("nur das Helligkeitsbyte aendert sich.");
			Console.WriteLine("Enter = weiter | Text = Beobachtung speichern | /stop = beenden");
			Console.WriteLine();
			StringBuilder report2 = new StringBuilder();
			report2.AppendLine("# AORUS zone brightness byte sweep");
			report2.AppendLine();
			StringBuilder stringBuilder6 = report2;
			StringBuilder stringBuilder7 = stringBuilder6;
			StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder6);
			handler2.AppendLiteral("- Created: ");
			handler2.AppendFormatted(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss zzz");
			stringBuilder7.AppendLine(ref handler2);
			report2.AppendLine("- Exact target gate: `VID 1044 / PID 7A41 / MI_03 / 9-byte Feature report`");
			report2.AppendLine("- Commands used: zone setter `0x08` selector 1-3 and zone getter `0x88` only");
			report2.AppendLine("- Global effect command, picture matrix, WMI, and EC: **not used**");
			report2.AppendLine("- Colour held constant at `#FFFFFF` on all three zones; only the brightness byte varies");
			stringBuilder6 = report2;
			StringBuilder stringBuilder8 = stringBuilder6;
			handler2 = new StringBuilder.AppendInterpolatedStringHandler(19, 1, stringBuilder6);
			handler2.AppendLiteral("- Levels tested: `");
			handler2.AppendFormatted(string.Join("`, `", levels));
			handler2.AppendLiteral("`");
			stringBuilder8.AppendLine(ref handler2);
			report2.AppendLine("- Context: the earlier boundary test covered only 0, 1, 25, 49, and 50 and concluded the byte is an off/on gate");
			report2.AppendLine();
			HidDevice device2 = DeviceList.Local.GetHidDevices(4164, 31297).SingleOrDefault((HidDevice candidate) => GetInterfaceLabel(candidate.DevicePath).Equals("MI_03", StringComparison.OrdinalIgnoreCase) && candidate.GetMaxFeatureReportLength() == 9);
			if (device2 == null)
			{
				Console.Error.WriteLine("Die exakt zugelassene RGB-Schnittstelle wurde nicht gefunden.");
				report2.AppendLine("- Exact approved RGB feature collection was not found; no packet was sent.");
				WriteReport();
				Environment.ExitCode = 4;
				return;
			}
			HidStream stream = device2.Open();
			try
			{
				Dictionary<byte, byte[]> originalZones = new Dictionary<byte, byte[]>();
				byte[] value;
				try
				{
					report2.AppendLine("## Captured original zone state");
					report2.AppendLine();
					for (byte zone2 = 1; zone2 <= 3; zone2++)
					{
						byte[] state = QueryZone2(zone2);
						originalZones.Add(zone2, state);
						stringBuilder6 = report2;
						StringBuilder stringBuilder9 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 5, stringBuilder6);
						handler2.AppendLiteral("- Zone ");
						handler2.AppendFormatted(zone2);
						handler2.AppendLiteral(": `#");
						handler2.AppendFormatted(state[3], "X2");
						handler2.AppendFormatted(state[4], "X2");
						handler2.AppendFormatted(state[5], "X2");
						handler2.AppendLiteral("`, brightness `");
						handler2.AppendFormatted(state[6]);
						handler2.AppendLiteral("`");
						stringBuilder9.AppendLine(ref handler2);
					}
					report2.AppendLine();
					report2.AppendLine("## Levels");
					report2.AppendLine();
					report2.AppendLine("| Requested | Stored readback | Owner observation |");
					report2.AppendLine("|---|---|---|");
					value = levels;
					foreach (byte level in value)
					{
						for (byte zone3 = 1; zone3 <= 3; zone3++)
						{
							WriteZone2(zone3, byte.MaxValue, byte.MaxValue, byte.MaxValue, level);
							Thread.Sleep(65);
						}
						byte[] readback = QueryZone2(1);
						Console.WriteLine($"Helligkeitsbyte {level} gesetzt, gespeichert gelesen als {readback[6]}.");
						Console.Write("  Beobachtung: ");
						string observation = Console.ReadLine()?.Trim() ?? string.Empty;
						stringBuilder6 = report2;
						StringBuilder stringBuilder10 = stringBuilder6;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 3, stringBuilder6);
						handler2.AppendLiteral("| `");
						handler2.AppendFormatted(level);
						handler2.AppendLiteral("` | `");
						handler2.AppendFormatted(readback[6]);
						handler2.AppendLiteral("` | ");
						handler2.AppendFormatted(Escape((observation.Length == 0) ? "(keine Beschreibung)" : observation));
						handler2.AppendLiteral(" |");
						stringBuilder10.AppendLine(ref handler2);
						if (observation.Equals("/stop", StringComparison.OrdinalIgnoreCase))
						{
							Console.WriteLine("Sweep wird beendet.");
							break;
						}
					}
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine("Testfehler: " + ex.Message);
					report2.AppendLine();
					stringBuilder6 = report2;
					StringBuilder stringBuilder11 = stringBuilder6;
					handler2 = new StringBuilder.AppendInterpolatedStringHandler(14, 1, stringBuilder6);
					handler2.AppendLiteral("- Test error: ");
					handler2.AppendFormatted(Escape(ex.Message));
					stringBuilder11.AppendLine(ref handler2);
					Environment.ExitCode = 5;
				}
				finally
				{
					report2.AppendLine();
					report2.AppendLine("## Restoration");
					report2.AppendLine();
					foreach (KeyValuePair<byte, byte[]> item11 in originalZones.OrderBy((KeyValuePair<byte, byte[]> item) => item.Key))
					{
						item11.Deconstruct(out var key2, out value);
						byte zone4 = key2;
						byte[] original = value;
						try
						{
							WriteZone2(zone4, original[3], original[4], original[5], original[6]);
							Thread.Sleep(65);
							byte[] restored = QueryZone2(zone4);
							bool exact = restored[3] == original[3] && restored[4] == original[4] && restored[5] == original[5] && restored[6] == original[6];
							stringBuilder6 = report2;
							StringBuilder stringBuilder12 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(46, 6, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(": `#");
							handler2.AppendFormatted(restored[3], "X2");
							handler2.AppendFormatted(restored[4], "X2");
							handler2.AppendFormatted(restored[5], "X2");
							handler2.AppendLiteral("`, brightness `");
							handler2.AppendFormatted(restored[6]);
							handler2.AppendLiteral("`, exact match: **");
							handler2.AppendFormatted(exact ? "yes" : "no");
							handler2.AppendLiteral("**");
							stringBuilder12.AppendLine(ref handler2);
							if (!exact)
							{
								Environment.ExitCode = 6;
							}
						}
						catch (Exception ex2)
						{
							stringBuilder6 = report2;
							StringBuilder stringBuilder13 = stringBuilder6;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder6);
							handler2.AppendLiteral("- Zone ");
							handler2.AppendFormatted(zone4);
							handler2.AppendLiteral(" restore failed: ");
							handler2.AppendFormatted(Escape(ex2.Message));
							stringBuilder13.AppendLine(ref handler2);
							Environment.ExitCode = 6;
						}
					}
					Console.WriteLine("Die vorherigen drei RGB-Zonen wurden wiederhergestellt.");
				}
				WriteReport();
			}
			finally
			{
				if (stream != null)
				{
					((IDisposable)stream).Dispose();
				}
			}
			byte[] QueryZone2(byte b)
			{
				byte[] query = new byte[9];
				query[1] = 136;
				query[2] = b;
				query[8] = CalculateGigabyteChecksum(query);
				stream.SetFeature(query);
				Thread.Sleep(10);
				byte[] response = new byte[9];
				stream.GetFeature(response);
				return response;
			}
			void WriteReport()
			{
				string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
				Directory.CreateDirectory(text2);
				string outputPath2 = Path.Combine(text2, $"keyboard-zone-brightness-sweep-{DateTime.Now:yyyyMMdd-HHmmss}.md");
				File.WriteAllText(outputPath2, report2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Console.WriteLine("Report written to: " + outputPath2);
			}
			void WriteZone2(byte b, byte red, byte green, byte blue, byte brightness)
			{
				byte[] request = new byte[9];
				request[1] = 8;
				request[2] = b;
				request[3] = red;
				request[4] = green;
				request[5] = blue;
				request[6] = brightness;
				request[8] = CalculateGigabyteChecksum(request);
				stream.SetFeature(request);
			}
		}
		static byte Scale(byte value, double factor)
		{
			return (byte)Math.Clamp(Math.Round((double)(int)value * factor), 0.0, 255.0);
		}
		static (byte r, byte g, byte b)[] Travelling(double elapsed, double secondsPerZone, (byte r, byte g, byte b) lit, (byte r, byte g, byte b) dim, bool pingPong)
		{
			int step = (int)(elapsed / secondsPerZone);
			int active = (pingPong ? Math.Abs(2 - step % 4) : (step % 3));
			(byte, byte, byte)[] frame = new(byte, byte, byte)[3];
			for (int i = 0; i < 3; i++)
			{
				frame[i] = ((i == active) ? lit : dim);
			}
			return frame;
		}
		static void TryClearConsole()
		{
			try
			{
				Console.Clear();
			}
			catch (IOException)
			{
			}
		}
		static string TryRead(Func<string> operation)
		{
			try
			{
				return operation();
			}
			catch (Exception ex)
			{
				return "unavailable (" + ex.Message + ")";
			}
		}
		static (byte r, byte g, byte b)[] Uniform(byte red, byte green, byte blue)
		{
			return new(byte, byte, byte)[3]
			{
				(red, green, blue),
				(red, green, blue),
				(red, green, blue)
			};
		}
		static void WriteBatteryChangeReport(StringBuilder changeReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"battery-change-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, changeReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(changeReport);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteBatteryInspectionReport(StringBuilder batteryReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"battery-inspection-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, batteryReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(batteryReport);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteBrightnessCycleReport(StringBuilder cycleReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-brightness-cycle-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, cycleReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(cycleReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		static void WriteCurveTestReport(StringBuilder test)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"fan-curve-write-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(test);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteFanChangeReport(StringBuilder change)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"fan-normal-change-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, change.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(change);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteFixedScaleReport(StringBuilder test)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"fan-fixed-scale-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(test);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteInteractiveKeyboardEffectReport(StringBuilder interactiveReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-effect-interactive-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, interactiveReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine("Bericht gespeichert: " + outputPath2);
		}
		static void WriteKeyboardEffectBatchReport(StringBuilder batchReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-effect-batch1-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, batchReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(batchReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		static void WriteKeyboardEffectTestReport(StringBuilder effectReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-effect-breathing-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, effectReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(effectReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		static void WriteKeyboardMatrixReport(StringBuilder matrixReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-matrix-read-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, matrixReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(matrixReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		static void WriteKeyboardRgbReport(StringBuilder rgbReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-rgb-query-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, rgbReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(rgbReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		static void WritePersistentColorReport(StringBuilder setReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-set-green-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, setReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(setReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
		static void WriteTemporaryFanTestReport(StringBuilder test, string profileSlug)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"fan-{profileSlug}-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(test);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteThermalPowerInspectionReport(StringBuilder inspection)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"thermal-power-inspection-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, inspection.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(inspection);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteWindowsPowerModeReport(StringBuilder test)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string reportPath = Path.Combine(text2, $"windows-power-overlay-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(reportPath, test.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(test);
			Console.WriteLine("Report written to: " + reportPath);
		}
		static void WriteZone(HidStream stream, byte b, byte red, byte green, byte blue, byte brightness)
		{
			byte[] request = new byte[9];
			request[1] = 8;
			request[2] = b;
			request[3] = red;
			request[4] = green;
			request[5] = blue;
			request[6] = brightness;
			request[8] = CalculateGigabyteChecksum(request);
			stream.SetFeature(request);
		}
		static void WriteZoneTestReport(StringBuilder testReport)
		{
			string text2 = Path.Combine(FindRepositoryRoot(), "research", "runs");
			Directory.CreateDirectory(text2);
			string outputPath2 = Path.Combine(text2, $"keyboard-zone-write-test-{DateTime.Now:yyyyMMdd-HHmmss}.md");
			File.WriteAllText(outputPath2, testReport.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Console.WriteLine(testReport);
			Console.WriteLine("Report written to: " + outputPath2);
		}
	}
}
