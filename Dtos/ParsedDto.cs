using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomCodeSystem.Dtos;
public class ParsedDto
{
    public Guid RowId { get; set; }
    public string? SN { get; set; }
    public string? IMEI { get; set; }
    public int Block { get; set; }   // 1-8
    public string? OperationalNumber { get; set; }
}