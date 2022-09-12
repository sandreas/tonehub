using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Sandreas.Files;

namespace tonehub.Services.FileIndexer;

public class FileWatcher<TId>
{
    private const int BatchSize = 10;
    private CancellationTokenSource _cts;
    private readonly FileWalker _fw;
    public readonly string Location;
    private readonly Func<IFileInfo, bool>  _whereFilterFunc;

    private readonly BufferBlock<IFileInfo> _queue = new();
    private readonly Func<IEnumerable<IFileInfo>, Task> _processAction;

    public TId? SourceId { get; set; } = default;
    
    // todo: replace params with: fs (for watchers), FileSource(id+location), IEnumerable<IFileInfo> allFiles (initialScan)
    public FileWatcher(FileWalker fw, TId sourceId, string location, Func<IFileInfo, bool>  whereFilterFunc)
    {
        _cts = new CancellationTokenSource();
        _fw = fw;
        SourceId = sourceId;
        Location = location;
        _whereFilterFunc = whereFilterFunc;
    }

    public async Task StartAsync()
    {
        Task.Run(InitialFullScan, _cts.Token);
        // todo: await CreateFileWatcher()
        // await ConsumeFiles();
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
            
            // load x times batch size as buffer, then wait until GetNextBatch
            while(_queue.Count >= BatchSize * 5)
            {
                await Task.Delay(10);
            }
        }
        _queue.Complete();
        await Task.CompletedTask;
    }

    /*
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
*/
    public async Task<IEnumerable<IFileInfo>> GetNextBatchAsync()
    {
        var batch = new List<IFileInfo>();
        while (await _queue.OutputAvailableAsync() && _queue.TryReceive(out var item) && batch.Count < BatchSize)
        {
            batch.Add(item);
        }
        return batch;
    }
}