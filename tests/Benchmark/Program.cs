// This is a simple benchmark to get some internal metrics

using MaxLib.WebServer.Benchmark.Profiles;
using MaxLib.WebServer.Benchmark.Rendering;

var profile = new ProfileStats();
profile.Entries[new ProfileEntryId("test", "foo", 1)] = new Stat(
    min: TimeSpan.FromSeconds(1.000),
    max: TimeSpan.FromSeconds(3.000),
    avg: TimeSpan.FromSeconds(1.678),
    mean: TimeSpan.FromSeconds(2.578)
);

var profile2 = new ProfileStats();
profile2.Entries[new ProfileEntryId("test", "foo", 1)] = new Stat(
    min: TimeSpan.FromSeconds(0.1000),
    max: TimeSpan.FromSeconds(0.3000),
    avg: TimeSpan.FromSeconds(0.1678),
    mean: TimeSpan.FromSeconds(0.2578)
);
profile2.Entries[new ProfileEntryId("test", "foo", 2)] = new Stat(
    min: TimeSpan.FromSeconds(11.000),
    max: TimeSpan.FromSeconds(13.000),
    avg: TimeSpan.FromSeconds(11.678),
    mean: TimeSpan.FromSeconds(12.578)
);

var renderer = new TableTextRenderer();
renderer.Render(Console.Out, profile.ToEntryTable());
Console.WriteLine();
renderer.Render(Console.Out, profile.ToEntryTable(profile2));

using var stream = new FileStream("test.html", FileMode.OpenOrCreate);
using var writer = new StreamWriter(stream);
var render2 = new TableHtmlRenderer();
render2.Render(writer, profile.ToEntryTable());
render2.Render(writer, profile.ToEntryTable(profile2));
writer.Flush();
stream.SetLength(stream.Position);
