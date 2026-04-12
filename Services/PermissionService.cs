using System.Collections.Generic;
using System.Linq;

namespace RFC_PORTAL.Services
{
    public class PermissionService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        public UserPermissions GetUserPermissions(string username)
        {
            if (string.IsNullOrEmpty(username))
                return new UserPermissions { Role = "Viewer" };

            var results = _sf.QueryAsList(
                "SELECT * FROM RFC_USER_ROLE WHERE USERNAME = :user AND IS_ACTIVE = TRUE",
                new Dictionary<string, object> { { "user", username } });

            if (results.Count == 0)
            {
                // Default: Viewer role for unknown users
                return new UserPermissions
                {
                    Username = username,
                    Role = "Viewer",
                    CanExecute = false,
                    CanConfigure = false,
                    CanExport = true,
                    CanViewLogs = true
                };
            }

            var row = results[0];
            return new UserPermissions
            {
                Username = username,
                Role = row["ROLE"]?.ToString() ?? "Viewer",
                Department = row["DEPARTMENT"]?.ToString(),
                CanExecute = IsTrue(row, "CAN_EXECUTE"),
                CanConfigure = IsTrue(row, "CAN_CONFIGURE"),
                CanExport = IsTrue(row, "CAN_EXPORT"),
                CanViewLogs = IsTrue(row, "CAN_VIEW_LOGS")
            };
        }

        public bool HasPermission(string username, string action)
        {
            var perms = GetUserPermissions(username);
            switch (action.ToLower())
            {
                case "execute": return perms.CanExecute;
                case "configure": return perms.CanConfigure;
                case "export": return perms.CanExport;
                case "view_logs": return perms.CanViewLogs;
                case "admin": return perms.Role == "Admin";
                default: return true;
            }
        }

        public List<Dictionary<string, object>> GetAllUsers()
        {
            return _sf.QueryAsList("SELECT * FROM RFC_USER_ROLE ORDER BY USERNAME");
        }

        public int CreateUser(string username, string role, string department,
            bool canExecute, bool canConfigure, bool canExport, bool canViewLogs, string createdBy)
        {
            var sql = @"INSERT INTO RFC_USER_ROLE
                (USERNAME, ROLE, DEPARTMENT, CAN_EXECUTE, CAN_CONFIGURE, CAN_EXPORT, CAN_VIEW_LOGS, CREATED_BY)
                VALUES (:user, :role, :dept, :exec, :config, :export, :logs, :created)";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "user", username }, { "role", role }, { "dept", department },
                { "exec", canExecute }, { "config", canConfigure },
                { "export", canExport }, { "logs", canViewLogs },
                { "created", createdBy }
            });
        }

        public int UpdateUser(int id, string role, string department,
            bool canExecute, bool canConfigure, bool canExport, bool canViewLogs)
        {
            var sql = @"UPDATE RFC_USER_ROLE SET
                ROLE=:role, DEPARTMENT=:dept, CAN_EXECUTE=:exec, CAN_CONFIGURE=:config,
                CAN_EXPORT=:export, CAN_VIEW_LOGS=:logs, UPDATED_DT=CURRENT_TIMESTAMP()
                WHERE ID=:id";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "role", role }, { "dept", department },
                { "exec", canExecute }, { "config", canConfigure },
                { "export", canExport }, { "logs", canViewLogs },
                { "id", id }
            });
        }

        public int DeactivateUser(int id)
        {
            return _sf.ExecuteNonQuery("UPDATE RFC_USER_ROLE SET IS_ACTIVE=FALSE WHERE ID=:id",
                new Dictionary<string, object> { { "id", id } });
        }

        private static bool IsTrue(Dictionary<string, object> row, string key)
        {
            if (!row.ContainsKey(key)) return false;
            var val = row[key];
            if (val is bool) return (bool)val;
            return val?.ToString()?.ToLower() == "true";
        }
    }

    public class UserPermissions
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public string Department { get; set; }
        public bool CanExecute { get; set; }
        public bool CanConfigure { get; set; }
        public bool CanExport { get; set; }
        public bool CanViewLogs { get; set; }
    }
}
