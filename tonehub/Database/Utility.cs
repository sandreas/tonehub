using System.Web;

namespace tonehub.Database;

public class Utility
{
    public static string UriToConnectionString(Uri connectionUri)
    {
        return connectionUri.Scheme switch
        {
            "sqlite" => BuildSqliteConnectionString(connectionUri),
            "sqlsrv" => BuildSqlServerConnectionString(connectionUri),
            "pgsql" => BuildPostgreSqlConnectionString(connectionUri),
            _ => ""
        };
    }

    private static string BuildPostgreSqlConnectionString(Uri uri)
    {
        
        var userInfo = Uri.UnescapeDataString(uri.UserInfo).Split(":");
        var port = uri.Port <= 0 ? 5432 : uri.Port;
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var host = uri.Host;
        var db = uri.AbsolutePath.TrimStart('/');

        var connectionStringParts = new List<string> { $"Server={host}", $"Database={db}" };
        if (username != "")
        {
            connectionStringParts.Add($"Username={username}");
        }
        if (password != "")
        {
            connectionStringParts.Add($"Password={password}");
        }
        
        if(uri.Query.Length > 0)
        {
            var nvc = HttpUtility.ParseQueryString(uri.Query);
            string[] keys = nvc.AllKeys.Where(x => x != null).ToArray()!;
            var dict = keys.ToDictionary(k => k, k => nvc[k]);
            foreach(var (key, value) in dict){
                connectionStringParts.Add($"{key}={value}");
            }
        }
        connectionStringParts.Add($"Port={port}");
        return string.Join(";", connectionStringParts);
    }

    private static string BuildSqliteConnectionString(Uri connectionUri)
    {
        // Data Source=reservoom.db
        // https://www.connectionstrings.com/sqlite/
        var path = connectionUri.Host + connectionUri.LocalPath;
        return $"Data Source={path}";
    }

    /**
     * Build sql server connection string from URI
     */
    private static string BuildSqlServerConnectionString(Uri uri)
    {
        // Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;
        var userInfo = Uri.UnescapeDataString(uri.UserInfo).Split(":");
        var username = userInfo[0];
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var host = uri.Host;
        var db = uri.AbsolutePath.TrimStart('/');

        var connectionStringParts = new List<string> { $"Server={host}", $"Database={db}" };
        if (username != "")
        {
            connectionStringParts.Add($"User Id={username}");
        }

        if (password != "")
        {
            connectionStringParts.Add($"Password={password}");
        }
        

        

        return string.Join(";", connectionStringParts);
    }
}