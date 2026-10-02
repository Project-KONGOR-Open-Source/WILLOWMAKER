namespace WILLOWMAKER.Tests.Services.Synchronisation;

/// <summary>
///     Verifies the synchronisation engine's local reconciliation behaviour: up-front partial cleanup, mirror deletion, target exclusions, and up-to-date detection.
///     The scenarios are constructed so that no downloads are required, which keeps the tests free of network access.
/// </summary>
public sealed class ContentBrokerTests
{
    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static Manifest CreateManifest(IReadOnlyDictionary<string, ManifestEntry>? files = null, IReadOnlyList<string>? excludeFromTarget = null) => new ()
    {
        Version = "test",
        HashAlgorithm = "SHA-256",
        ExcludeFromSource = [],
        ExcludeFromTarget = excludeFromTarget ?? [],
        Files = files ?? new Dictionary<string, ManifestEntry>()
    };

    // "Progress<T>" Posts Its Callbacks Asynchronously, So Events Could Arrive After The Synchronisation Returns; Recording Synchronously Keeps The Order Deterministic
    private sealed class RecordingProgress : IProgress<SynchronisationEvent>
    {
        public List<SynchronisationEvent> Events { get; } = [];

        public void Report(SynchronisationEvent value) => Events.Add(value);
    }

    [Test]
    public async Task A_Leftover_Partial_File_Is_Deleted_Before_The_Plan_Is_Reported()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string partialPath = Path.Combine(directory, "leftover.partial");

            File.WriteAllText(partialPath, "interrupted download");

            RecordingProgress recorder = new ();

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(), "was", directory, progress: recorder);

            int deletedIndex = recorder.Events.FindIndex(synchronisationEvent => synchronisationEvent.Kind is SynchronisationEventKind.Deleted);
            int planIndex    = recorder.Events.FindIndex(synchronisationEvent => synchronisationEvent.Kind is SynchronisationEventKind.PlanReady);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(partialPath)).IsFalse();
                await Assert.That(deletedIndex).IsGreaterThanOrEqualTo(0);
                await Assert.That(deletedIndex).IsLessThan(planIndex);
                await Assert.That(recorder.Events[planIndex].Plan).IsNotNull();
                await Assert.That(recorder.Events[planIndex].Plan?.FilesToDelete).IsEqualTo(1);
                await Assert.That(summary.FilesDeleted).IsEqualTo(1);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Local_File_Not_In_The_Manifest_Is_Deleted()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string stalePath = Path.Combine(directory, "stale.txt");

            File.WriteAllText(stalePath, "no longer distributed");

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(), "was", directory);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(stalePath)).IsFalse();
                await Assert.That(summary.FilesDeleted).IsEqualTo(1);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task A_Local_File_Matching_A_Target_Exclusion_Is_Preserved()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string protectedPath = Path.Combine(directory, "COMPEL.json");

            File.WriteAllText(protectedPath, "operator configuration");

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(excludeFromTarget: [ "COMPEL*" ]), "was", directory);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(protectedPath)).IsTrue();
                await Assert.That(summary.FilesDeleted).IsEqualTo(0);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task An_Up_To_Date_File_Is_Neither_Downloaded_Nor_Deleted()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            byte[] content = Encoding.UTF8.GetBytes("distributed content");

            string localPath = Path.Combine(directory, "data.bin");

            File.WriteAllBytes(localPath, content);

            Dictionary<string, ManifestEntry> files = new ()
            {
                ["data.bin"] = new ManifestEntry { Size = content.Length, Hash = Convert.ToHexStringLower(SHA256.HashData(content)) }
            };

            SynchronisationSummary summary = await ContentBroker.Synchronise(CreateManifest(files), "was", directory);

            using (Assert.Multiple())
            {
                await Assert.That(File.Exists(localPath)).IsTrue();
                await Assert.That(summary.FilesUpToDate).IsEqualTo(1);
                await Assert.That(summary.FilesDownloaded).IsEqualTo(0);
                await Assert.That(summary.FilesDeleted).IsEqualTo(0);
                await Assert.That(summary.FilesFailed).IsEqualTo(0);
            }
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
