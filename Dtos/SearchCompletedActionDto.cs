using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomCodeSystem.Dtos;

public sealed class SearchCompletedActionsResponseDto
{
    public List<SearchCompletedActionDto>? SearchCompletedActions { get; set; }
}

public sealed class SearchCompletedActionDto
{
    public int Id { get; set; }
    public DateTime? ConnectDateTime { get; set; }

    public string? WorkerId { get; set; }
    public string? WorkerName { get; set; }
    public string? WorkerSurname { get; set; }

    public string? OperationCode { get; set; }
    public string? OperationDescription { get; set; }

    public string? BatchNumber { get; set; }
    public string? Documentation { get; set; }

    public string? OperationalNumber { get; set; }
    public string? Imei { get; set; }
    public string? SerialNumber { get; set; }

    public bool? Completed { get; set; }
    public bool? Pass { get; set; }

    public string? Comment { get; set; }
    public int? TestId { get; set; }
}
