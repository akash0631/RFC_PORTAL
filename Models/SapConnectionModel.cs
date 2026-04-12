using System;

namespace RFC_PORTAL.Models
{
    public class SapConnectionModel
    {
        public int Id { get; set; }
        public string ConnectionName { get; set; }
        public string AppServerHost { get; set; }
        public string Client { get; set; }
        public string SystemNumber { get; set; }
        public string RfcUser { get; set; }
        public string RfcPasswordEnc { get; set; }
        public string Language { get; set; }
        public bool IsActive { get; set; }
        public string Environment { get; set; }
        public DateTime? CreatedDt { get; set; }
        public DateTime? UpdatedDt { get; set; }
    }

    public class RfcConfigAuditModel
    {
        public int Id { get; set; }
        public int? RfcId { get; set; }
        public string TableName { get; set; }
        public int? RecordId { get; set; }
        public string Action { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string ChangedBy { get; set; }
        public DateTime? ChangedDt { get; set; }
    }
}
