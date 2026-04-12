namespace RFC_PORTAL.Models
{
    public class RfcParamModel
    {
        public int Id { get; set; }
        public int RfcId { get; set; }
        public string ParamName { get; set; }
        public string ParamType { get; set; }
        public string DataType { get; set; }
        public string DefaultExpression { get; set; }
        public bool IsRequired { get; set; }
        public string DisplayName { get; set; }
        public int SortOrder { get; set; }
    }
}
