namespace RFC_PORTAL.Models
{
    public class RfcSelectOptionModel
    {
        public int Id { get; set; }
        public int RfcId { get; set; }
        public string TableName { get; set; }
        public string SignValue { get; set; }
        public string OptionValue { get; set; }
        public string ValueSource { get; set; }
        public string StaticValue { get; set; }
        public string SourceQuery { get; set; }
        public string LoopColumn { get; set; }
    }
}
