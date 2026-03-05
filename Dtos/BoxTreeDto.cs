namespace CustomCodeSystem.Dtos
{

    public sealed class BoxTreeInfoDto
    {
        public string? Id { get; set; }
        public string? ResultById { get; set; }
        public string? Batch { get; set; }
        public string? Code { get; set; }
        public int? BoxNumber { get; set; }
        public int? Size { get; set; }

        public List<BoxTreeProductDto> Products { get; set; } = new();
    }

    public sealed class BoxTreeProductDto
    {
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? SerialNumber { get; set; }
        public string? OperationalNumber { get; set; }
        public string? CaseSerialNumber { get; set; }
        public string? ConfigurationMetadata { get; set; }
    }

}
