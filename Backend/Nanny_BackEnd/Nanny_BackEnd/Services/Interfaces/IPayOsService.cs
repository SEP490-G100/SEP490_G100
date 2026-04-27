using System;
using System.Threading.Tasks;
using Nanny_BackEnd.DTOs.Subscription;

namespace Nanny_BackEnd.Services.Interfaces;

public interface IPayOsService
{
    bool isConfigured();
    Task<PayOsPaymentInstruction> createPaymentInstruction(
        Guid transactionId,
        int orderCode,
        decimal amount,
        string planName);
    Task<PayOsPaymentInstruction?> getPaymentInstruction(int orderCode);
    Task<PayOsPaymentStatusResult?> getPaymentStatus(int orderCode);
    bool isWebhookValid(PayOsWebhookRequest request);
    string buildSuccessUrl(Guid transactionId);
    string buildCancelUrl(Guid transactionId);
    string buildCheckoutQrUrl(string? checkoutUrl);
    string buildRawQrUrl(string? qrPayload);
}
