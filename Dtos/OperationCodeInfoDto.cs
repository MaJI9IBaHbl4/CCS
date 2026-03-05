using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomCodeSystem.Dtos;

public sealed class OperationCodeInfoDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public bool DoesRequireLot { get; set; }
    public string? Description { get; set; }
    public string? MandatoryOperations { get; set; }
    public bool Repeatable { get; set; }
    public bool Disabled { get; set; }
    public int Quota { get; set; }
    public bool Additional { get; set; }
}
