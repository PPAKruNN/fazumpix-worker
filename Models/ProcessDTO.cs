namespace FazUmPix.DTOs;
using System.ComponentModel.DataAnnotations;

public class ProcessPaymentDTO
{
    public required string ProcessURL { get; set; }
    public required string AcknowledgeURL { get; set; }
    public required uint PaymentId { get; set; }
    public required CreatePaymentInputDTO Data { get; set; }
}

public class CreatePaymentInputDTO
{
    public required OriginDTO Origin { get; set; }
    public required Destiny Destiny { get; set; }
    public required long Amount { get; set; }
    public string? Description { get; set; }
}

public class Destiny
{
    public required PixKeyDTO Key { get; set; }
}

public class AccountDTO
{
    [Required]
    public required string Number { get; set; }
    [Required]
    public required string Agency { get; set; }
}

public class UserCPFDTO
{
    [Required]
    [StringLength(11)]
    public required string CPF { get; set; }
}

public class OriginDTO
{
    public required UserCPFDTO User { get; set; }
    public required AccountDTO Account { get; set; }
}

public class PixKeyDTO
{
    [Required]
    [RegularExpression("Random|CPF|Phone|Email")]
    public required string Type { get; set; }
    [Required]
    public required string Value { get; set; }
}