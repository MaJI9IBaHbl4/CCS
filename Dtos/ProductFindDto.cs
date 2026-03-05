using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomCodeSystem.Dtos
{
    public sealed class ProductFindDto
    {
        public string? Id { get; set; }
        public string? Imei { get; set; }
        public string? SerialNumber { get; set; }
        public string? OperationalNumber { get; set; }
        public string? ProductCode { get; set; }
        public string? FirmwareVersion { get; set; }
        public string? ConfigurationMetadata { get; set; }
        public string? Batch { get; set; }
        public string? BoxId { get; set; }

        public bool? Disassembled { get; set; }
        public int? PcbCount { get; set; }
        public long? ProductPrimaryId { get; set; }
    }
}
