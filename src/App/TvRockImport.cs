using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TsScheduler;

internal sealed record TunerProfile(int Number, string ApplicationPath, string ApplicationName, string Arguments, string RecordingPath);
internal sealed record Reservation(DateTimeOffset Start, DateTimeOffset End, string Station, string Title, int ServiceId);
internal sealed record ImportedConfiguration(IReadOnlyList<TunerProfile> Tuners, IReadOnlyList<Reservation> Reservations);

internal static partial class TvRockImport
{
    public static ImportedConfiguration Load(string folder)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(932);
        var tuners = LoadTuners(Path.Combine(folder, "dtv.ini"), encoding);
        var reservations = LoadReservations(Path.Combine(folder, "tvrock.sch"), encoding);
        return new ImportedConfiguration(tuners, reservations);
    }

    private static IReadOnlyList<TunerProfile> LoadTuners(string path, Encoding encoding)
    {
        if (!File.Exists(path)) return [];
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? current = null;
        foreach (var rawLine in File.ReadLines(path, encoding))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = line[1..^1];
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[name] = current;
                continue;
            }
            var separator = line.IndexOf('=');
            if (current is not null && separator > 0)
                current[line[..separator]] = line[(separator + 1)..];
        }

        return sections
            .Where(pair => TunerSectionPattern().IsMatch(pair.Key))
            .Select(pair =>
            {
                var number = int.Parse(TunerSectionPattern().Match(pair.Key).Groups[1].Value, CultureInfo.InvariantCulture);
                var values = pair.Value;
                values.TryGetValue("APPPATH", out var appPath);
                values.TryGetValue("APPNAME1", out var appName);
                values.TryGetValue("APPOPT1", out var arguments);
                values.TryGetValue("RECPATH", out var recordingPath);
                return new TunerProfile(number, appPath ?? "", appName ?? "", arguments ?? "", recordingPath ?? "");
            })
            .Where(tuner => !string.IsNullOrWhiteSpace(tuner.ApplicationName))
            .OrderBy(tuner => tuner.Number)
            .ToList();
    }

    private static IReadOnlyList<Reservation> LoadReservations(string path, Encoding encoding)
    {
        if (!File.Exists(path)) return [];
        var reservations = new List<Reservation>();
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path, encoding))
        {
            var match = ScheduleFieldPattern().Match(line);
            if (!match.Success) continue;
            var key = match.Groups[1].Value;
            if (key == "START" && fields.Count > 0) AddReservation(fields, reservations);
            if (key == "START") fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            fields[key] = match.Groups[2].Value;
        }
        AddReservation(fields, reservations);
        return reservations.OrderBy(reservation => reservation.Start).ToList();
    }

    private static void AddReservation(IReadOnlyDictionary<string, string> fields, ICollection<Reservation> reservations)
    {
        if (!fields.TryGetValue("START", out var startText) || !long.TryParse(startText, out var startUnix)) return;
        if (!fields.TryGetValue("END", out var endText) || !long.TryParse(endText, out var endUnix)) return;
        fields.TryGetValue("STATION", out var station);
        fields.TryGetValue("TITLE", out var title);
        fields.TryGetValue("SERVICEID", out var serviceText);
        _ = int.TryParse(serviceText, out var serviceId);
        reservations.Add(new Reservation(DateTimeOffset.FromUnixTimeSeconds(startUnix).ToLocalTime(), DateTimeOffset.FromUnixTimeSeconds(endUnix).ToLocalTime(), station ?? "", title ?? "", serviceId));
    }

    [GeneratedRegex("^TUNER(\\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TunerSectionPattern();

    [GeneratedRegex("^\\d+ ([A-Z]+) (.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ScheduleFieldPattern();
}
