using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Threading.Tasks.Dataflow;
using Sandreas.Files;
using tonehub.Metadata;

namespace tonehub.Services.FileIndexer;

public class FileWatcher
{
    private CancellationTokenSource _cts;
    private readonly FileWalker _fw;
    public readonly string Location;
    private readonly IFileLoader _tagLoader;

    private readonly BufferBlock<IFileInfo> _queue = new();

    public FileWatcher(FileWalker fw, IFileLoader tagLoader, string location)
    {
        _cts = new CancellationTokenSource();
        _fw = fw;
        _tagLoader = tagLoader;
        Location = location;
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
        var files = _fw.WalkRecursive(Location).SelectFileInfo().Where(_tagLoader.Supports);
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
                _processFiles(items);
            }
        }
    }

    private void _processFiles(IList<IFileInfo> items)
    {
        
    }

}