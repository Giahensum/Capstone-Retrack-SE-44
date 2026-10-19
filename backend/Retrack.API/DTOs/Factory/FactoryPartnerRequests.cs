namespace Retrack.API.DTOs.Factory;

public class PartnerStatusRequest { public Retrack.API.Models.Enums.PartnershipStatus Status { get; set; } }

public class PartnershipContractRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(10000, MinimumLength = 50)]
    public string Terms { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Range(1, 120)]
    public int ValidMonths { get; set; } = 12;

    public bool ConfirmElectronicSignature { get; set; }
}

public class ContractTerminationRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(2000, MinimumLength = 10)]
    public string Reason { get; set; } = "";
}

public class ContractTerminationDecisionRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(ACCEPT|REJECT)$")]
    public string Decision { get; set; } = "";
}
