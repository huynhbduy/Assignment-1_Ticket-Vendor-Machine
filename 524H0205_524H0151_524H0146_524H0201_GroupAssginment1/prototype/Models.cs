namespace SmartTicketing;

public enum PaymentMethod { Card, QrWallet }
public enum OrderStatus { Pending, Paid, Declined, ReconciliationRequired }
public sealed record Destination(Guid Id, string Name, long FareVnd);
public sealed record PurchaseResult(Guid OrderId, Guid DestinationId, string Destination, int Quantity, long TotalVnd,
    PaymentMethod Method, OrderStatus Status, string TicketCodes, DateTime CreatedAt);
