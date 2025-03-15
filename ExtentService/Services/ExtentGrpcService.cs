using Dfs.Extentservice;
using ExtentService.Configs;
using Grpc.Core;

namespace ExtentService.Services;

public class ExtentGrpcService : Dfs.Extentservice.ExtentService.ExtentServiceBase
{
    private readonly IHostApplicationLifetime _lifetime; 
    private readonly ServerConfiguration _serverConfiguration;
    
    public ExtentGrpcService(IHostApplicationLifetime lifetime, ServerConfiguration serverConfiguration)
    {
        _serverConfiguration = serverConfiguration;
        _lifetime = lifetime;
        
        if (!Directory.Exists(_serverConfiguration.ServerRootPath))
            if (_serverConfiguration.ServerRootPath != null)
                Directory.CreateDirectory(_serverConfiguration.ServerRootPath);
    }

    public override Task<GetResponse> get(GetRequest request, ServerCallContext context)
    {
        Console.WriteLine("Get request: " + request.FileName);
        
        var fullPath = Path.Join(_serverConfiguration.ServerRootPath, request.FileName.Replace("/", "\\"));
        
        if (Directory.Exists(fullPath))
        {
            var directoryContents = Directory.EnumerateFileSystemEntries(fullPath).Select(Path.GetFileName);
            var result = string.Join("\n", directoryContents);
            return Task.FromResult(new GetResponse 
            { 
                FileData = Google.Protobuf.ByteString.CopyFromUtf8(result) 
            });
        }
        
        if (File.Exists(fullPath))
        {
            var fileBytes = File.ReadAllBytes(fullPath);
            return Task.FromResult(new GetResponse 
            { 
                FileData = Google.Protobuf.ByteString.CopyFrom(fileBytes) 
            });
        }
        
        return Task.FromResult(new GetResponse ());
    }

    public override Task<PutResponse> put(PutRequest request, ServerCallContext context)
    {
        Console.WriteLine("Put request: " + request.FileName + ", HasFileData: " + request.HasFileData);
        
        var path = Path.Join(_serverConfiguration.ServerRootPath, request.FileName.Replace("/", "\\"));
        
        switch (request.FileName)
        {
            case var fileName when fileName.EndsWith('/') && request.HasFileData:
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                break;
            
            case var fileName when fileName.EndsWith('/') && !request.HasFileData:
                if (Directory.Exists(path))
                    Directory.Delete(path);
                break;
            
            case var fileName when !fileName.EndsWith('/') && request.HasFileData:
                File.WriteAllBytes(path, request.FileData.ToByteArray());
                break;
            
            case var fileName when !fileName.EndsWith('/') && !request.HasFileData:
                if (File.Exists(path))
                    File.Delete(path);
                break;
        }
        
        return Task.FromResult(new PutResponse { Success = true });
    }

    public override Task<StopResponse> stop(StopRequest request, ServerCallContext context)
    {
        _lifetime.StopApplication();

        return Task.FromResult(new StopResponse());
    }
}