using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Sandreas.Files;
using tonehub.Metadata;

namespace tonehub.Services.FileIndexer;

public class FileWatcher<T>
{
    private CancellationTokenSource _cts;
    private readonly FileWalker _fw;
    public readonly string Location;
    private readonly Func<IFileInfo,bool>  _whereFilterFunc;

    private readonly BufferBlock<IFileInfo> _queue = new();
    private readonly Func<IEnumerable<T>, Task> _processAction;

    public FileWatcher(FileWalker fw, string location, Func<IEnumerable<T>, Task> processAction, Func<IFileInfo, bool> whereFilterFunc)
    {
        _cts = new CancellationTokenSource();
        _fw = fw;
        Location = location;
        _processAction = processAction;
        _whereFilterFunc = whereFilterFunc;
    }

    public void Start()
    {
        Task.Run(ProduceFiles, _cts.Token);
        Task.Run(ConsumeFiles, _cts.Token);
    }
    
    public void Cancel()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();
    }
    private Task ProduceFiles()
    {
        var files = _fw.WalkRecursive(Location).SelectFileInfo().Where(_whereFilterFunc);
        foreach (var file in files)
        {
            _queue.Post(file);
        }
        _queue.Complete();
        return Task.CompletedTask;
    }

    private async Task ConsumeFiles()
    {
        while (await _queue.OutputAvailableAsync())
        {
            if (_queue.TryReceiveAll(out var items))
            {
                await _processAction.Invoke((IEnumerable<T>)items);
            }
        }
    }
}