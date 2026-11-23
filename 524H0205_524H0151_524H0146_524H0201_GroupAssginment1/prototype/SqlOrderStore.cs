using Microsoft.Data.SqlClient;

namespace SmartTicketing;

public sealed class SqlOrderStore
{
    private readonly string _connectionString;
    public SqlOrderStore(string connectionString) => _connectionString = connectionString;

    public async Task<IReadOnlyList<Destination>> GetDestinationsAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT DestinationId, Name, FareVnd FROM dbo.Destination WHERE IsActive=1 ORDER BY Name";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var results = new List<Destination>();
        while (await reader.ReadAsync(ct))
            results.Add(new Destination(reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2)));
        return results;
    }

    public async Task SavePurchaseAsync(PurchaseResult purchase, CancellationToken ct = default)
    {
        const string orderSql = "INSERT dbo.TicketOrder(OrderId,DestinationId,Quantity,TotalVnd,Status,IdempotencyKey,CreatedAt) " +
            "SELECT @order,@destination,@quantity,@total,@status,@key,@created WHERE NOT EXISTS " +
            "(SELECT 1 FROM dbo.TicketOrder WHERE IdempotencyKey=@key);";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await using (var command = new SqlCommand(orderSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@order", purchase.OrderId);
                command.Parameters.AddWithValue("@destination", purchase.DestinationId);
                command.Parameters.AddWithValue("@quantity", purchase.Quantity);
                command.Parameters.AddWithValue("@total", purchase.TotalVnd);
                command.Parameters.AddWithValue("@status", purchase.Status.ToString());
                command.Parameters.AddWithValue("@key", purchase.OrderId.ToString("N"));
                command.Parameters.AddWithValue("@created", purchase.CreatedAt);
                await command.ExecuteNonQueryAsync(ct);
            }
            await using (var payment = new SqlCommand("INSERT dbo.Payment(PaymentId,OrderId,Method,AmountVnd,Status) VALUES(@id,@order,@method,@amount,@status)", connection, transaction))
            {
                payment.Parameters.AddWithValue("@id", Guid.NewGuid()); payment.Parameters.AddWithValue("@order", purchase.OrderId);
                payment.Parameters.AddWithValue("@method", purchase.Method.ToString()); payment.Parameters.AddWithValue("@amount", purchase.TotalVnd);
                payment.Parameters.AddWithValue("@status", purchase.Status.ToString()); await payment.ExecuteNonQueryAsync(ct);
            }
            foreach (var code in purchase.TicketCodes.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                await using var ticket = new SqlCommand("INSERT dbo.Ticket(TicketId,OrderId,Barcode) VALUES(@id,@order,@barcode)", connection, transaction);
                ticket.Parameters.AddWithValue("@id", Guid.NewGuid()); ticket.Parameters.AddWithValue("@order", purchase.OrderId);
                ticket.Parameters.AddWithValue("@barcode", code); await ticket.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(ct); throw; }
    }
}
