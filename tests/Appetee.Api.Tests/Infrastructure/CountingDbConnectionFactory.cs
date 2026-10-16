using Appetee.Infrastructure.Data;
using MySqlConnector;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Appetee.Api.Tests.Infrastructure;

/// <summary>Counts executed application commands, not setup or provider transaction-control commands.</summary>
internal sealed class CountingDbConnectionFactory(string connectionString) : IDbConnectionFactory
{
    private int _executions;
    public int Executions => Volatile.Read(ref _executions);
    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken ct)
    {
        var inner=new MySqlConnection(connectionString);
        await inner.OpenAsync(ct);
        return new CountingConnection(inner,()=>Interlocked.Increment(ref _executions));
    }

    private sealed class CountingConnection(MySqlConnection inner,Action record) : DbConnection
    {
        [AllowNull] public override string ConnectionString {get=>inner.ConnectionString;set=>inner.ConnectionString=value;}
        public override string Database=>inner.Database;
        public override string DataSource=>inner.DataSource;
        public override string ServerVersion=>inner.ServerVersion;
        public override ConnectionState State=>inner.State;
        public override void ChangeDatabase(string name)=>inner.ChangeDatabase(name);
        public override void Open()=>inner.Open();
        public override Task OpenAsync(CancellationToken ct)=>inner.OpenAsync(ct);
        public override void Close()=>inner.Close();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolation)=>inner.BeginTransaction(isolation);
        protected override DbCommand CreateDbCommand()=>new CountingCommand(inner.CreateCommand(),this,record);
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    }
    private sealed class CountingCommand(MySqlCommand inner,DbConnection owner,Action record) : DbCommand
    {
        [AllowNull] public override string CommandText {get=>inner.CommandText;set=>inner.CommandText=value;}
        public override int CommandTimeout {get=>inner.CommandTimeout;set=>inner.CommandTimeout=value;}
        public override CommandType CommandType {get=>inner.CommandType;set=>inner.CommandType=value;}
        public override bool DesignTimeVisible {get=>inner.DesignTimeVisible;set=>inner.DesignTimeVisible=value;}
        public override UpdateRowSource UpdatedRowSource {get=>inner.UpdatedRowSource;set=>inner.UpdatedRowSource=value;}
        protected override DbConnection? DbConnection {get=>owner;set { }}
        protected override DbTransaction? DbTransaction {get=>inner.Transaction;set=>inner.Transaction=(MySqlTransaction?)value;}
        protected override DbParameterCollection DbParameterCollection=>inner.Parameters;
        public override void Cancel()=>inner.Cancel();
        public override void Prepare()=>inner.Prepare();
        protected override DbParameter CreateDbParameter()=>inner.CreateParameter();
        public override int ExecuteNonQuery(){record();return inner.ExecuteNonQuery();}
        public override object? ExecuteScalar(){record();return inner.ExecuteScalar();}
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior){record();return inner.ExecuteReader(behavior);}
        public override Task<int> ExecuteNonQueryAsync(CancellationToken ct){record();return inner.ExecuteNonQueryAsync(ct);}
        public override Task<object?> ExecuteScalarAsync(CancellationToken ct){record();return inner.ExecuteScalarAsync(ct);}
        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior,CancellationToken ct)
        {record();return await inner.ExecuteReaderAsync(behavior,ct);}
        protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
    }
}
