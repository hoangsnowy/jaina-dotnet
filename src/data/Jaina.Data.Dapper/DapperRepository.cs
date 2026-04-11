using System.Data;
using Dapper;

namespace Jaina.Data.Dapper;

public abstract class DapperRepository : IRepository
{
    protected const int CommandTimeout = 180;
    protected IDbConnection DbConnection { get; }

    protected DapperRepository(IDbConnection dbConnection)
    {
        DbConnection = dbConnection;
    }

    private void EnsureOpen()
    {
        if (DbConnection.State is ConnectionState.Closed or ConnectionState.Broken)
            DbConnection.Open();
    }

    protected async Task<TDto?> GetSingleAsync<TDto>(
        string query,
        Dictionary<string, object>? inputParameters = null,
        Dictionary<string, object>? outputParameters = null,
        int commandTimeout = CommandTimeout,
        CommandType commandType = CommandType.StoredProcedure)
    {
        try
        {
            EnsureOpen();
            var args = new DynamicParameters();
            AddParams(args, inputParameters, ParameterDirection.Input);
            AddParams(args, outputParameters, ParameterDirection.InputOutput);

            var results = await DbConnection.QueryAsync<TDto>(query, args, commandTimeout: commandTimeout, commandType: commandType).ConfigureAwait(false);
            ReadOutputParams(args, outputParameters);
            return results.SingleOrDefault();
        }
        finally
        {
            DbConnection.Close();
        }
    }

    protected async Task<IEnumerable<TDto>> GetListAsync<TDto>(
        string query,
        Dictionary<string, object>? inputParameters = null,
        Dictionary<string, object>? outputParameters = null,
        int commandTimeout = CommandTimeout,
        CommandType commandType = CommandType.StoredProcedure)
    {
        try
        {
            EnsureOpen();
            var args = new DynamicParameters();
            AddParams(args, inputParameters, ParameterDirection.Input);
            AddParams(args, outputParameters, ParameterDirection.InputOutput);

            var results = await DbConnection.QueryAsync<TDto>(query, args, commandTimeout: commandTimeout, commandType: commandType).ConfigureAwait(false);
            ReadOutputParams(args, outputParameters);
            return results;
        }
        finally
        {
            DbConnection.Close();
        }
    }

    private static void AddParams(DynamicParameters args, Dictionary<string, object>? parameters, ParameterDirection direction)
    {
        if (parameters is null) return;
        foreach (var (key, value) in parameters)
            args.Add(key, value, direction: direction);
    }

    private static void ReadOutputParams(DynamicParameters args, Dictionary<string, object>? parameters)
    {
        if (parameters is null) return;
        foreach (var key in parameters.Keys.ToList())
            parameters[key] = args.Get<object>(key);
    }
}
