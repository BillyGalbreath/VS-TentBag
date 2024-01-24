using System.Collections.Concurrent;
using System.IO;
using Vintagestory.API.Config;

namespace TentBag.Configuration;

public class FileWatcher {
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, bool> _queue = new();

    public FileWatcher() {
        _watcher = new FileSystemWatcher(GamePaths.ModConfig);

        _watcher.Filter = $"{TentBag.Instance.Mod.Info.ModID}.yml";
        _watcher.IncludeSubdirectories = false;
        _watcher.EnableRaisingEvents = true;

        _watcher.Changed += Changed;
        _watcher.Created += Changed;
        _watcher.Deleted += Changed;
        _watcher.Renamed += Changed;
        _watcher.Error += Error;
    }

    private void Changed(object sender, FileSystemEventArgs e) {
        QueueReload(e.ChangeType);
    }

    private void Error(object sender, ErrorEventArgs e) {
        TentBag.Instance.Mod.Logger.Error(e.GetException().ToString());
        QueueReload();
    }

    /// <summary>
    /// My workaround for <a href='https://github.com/dotnet/runtime/issues/24079'>dotnet#24079</a>.
    /// </summary>
    /// <param name="change">The <see cref='System.IO.FileSystemWatcher'>FileSystemWatcher</see>'s change type.</param>
    private void QueueReload(WatcherChangeTypes? change = null) {
        // we need a key for the dict
        string changeType = change?.ToString().ToLowerInvariant() ?? "null";

        if (!_queue.TryAdd(changeType, true)) {
            // already queued for reload
            return;
        }

        // wait 100ms for other changes to process and then reload config
        TentBag.Instance.Api?.Event.RegisterCallback(_ => {
            if (!string.IsNullOrEmpty(changeType)) {
                TentBag.Instance.Mod.Logger.Event($"Detected the config was {changeType}");
            }

            Config.Reload();

            // wait 100ms more to remove this change from the queue since the reload triggers another write
            TentBag.Instance.Api.Event.RegisterCallback(_ => _queue.TryRemove(changeType, out bool _), 100);
        }, 100);
    }

    public void Dispose() {
        _watcher.Changed -= Changed;
        _watcher.Created -= Changed;
        _watcher.Deleted -= Changed;
        _watcher.Renamed -= Changed;
        _watcher.Error -= Error;

        _watcher.Dispose();
    }
}
