using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Sandreas.Files;
using tonehub.Metadata;
using ILogger = Serilog.ILogger;

namespace tonehub.Services.FileIndexer;

public class FileWatcher
{
    private const int BatchSize = 10;
    private CancellationTokenSource _cts;
    private readonly FileWalker _fw;
    public readonly string Location;
    private readonly Func<IFileInfo, bool>  _whereFilterFunc;

    private readonly BufferBlock<IFileInfo> _queue = new();
    private readonly Func<IEnumerable<IFileInfo>, Task> _processAction;

    public FileWatcher(FileWalker fw, string location, Func<IEnumerable<IFileInfo>, Task> processAction, Func<IFileInfo, bool>  whereFilterFunc)
    {
        _cts = new CancellationTokenSource();
        _fw = fw;
        Location = location;
        _processAction = processAction;
        _whereFilterFunc = whereFilterFunc;
    }

    public async Task StartAsync()
    {
        Task.Run(InitialFullScan, _cts.Token);
        // todo: await CreateFileWatcher()
        await ConsumeFiles();
    }
    
    public void Cancel()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
    }
    private async Task InitialFullScan()
    {
        var files = _fw.WalkRecursive(Location).SelectFileInfo().Where(_whereFilterFunc);
        foreach (var file in files)
        {
            _queue.Post(file);
            /*
            while(_queue.Count >= BatchSize)
            {
                await Task.Delay(10);
            }
            */
        }
        _queue.Complete();
        await Task.CompletedTask;
    }

    private async Task ConsumeFiles()
    {
        while (await _queue.OutputAvailableAsync())
        {
            var batch = new List<IFileInfo>();
            while(_queue.TryReceive(out var item) && batch.Count < 10)
            {
                batch.Add(item);
            }
            if (batch.Count > 0)
            {
                await _processAction.Invoke(batch);
            }
        }
    }
}