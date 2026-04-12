using System.Collections.Generic;
using System.Linq;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class SapConnectionService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        public List<Dictionary<string, object>> GetAll()
        {
            return _sf.QueryAsList("SELECT * FROM RFC_SAP_CONNECTION ORDER BY ENVIRONMENT, CONNECTION_NAME");
        }

        public Dictionary<string, object> GetById(int id)
        {
            var results = _sf.QueryAsList("SELECT * FROM RFC_SAP_CONNECTION WHERE ID=:id",
                new Dictionary<string, object> { { "id", id } });
            return results.FirstOrDefault();
        }

        public int Update(int id, SapConnectionModel model)
        {
            var sql = @"UPDATE RFC_SAP_CONNECTION SET
                        CONNECTION_NAME=:name, APP_SERVER_HOST=:host, CLIENT=:client,
                        SYSTEM_NUMBER=:sysNum, RFC_USER=:user, RFC_PASSWORD_ENC=:pass,
                        LANGUAGE=:lang, IS_ACTIVE=:active, ENVIRONMENT=:env,
                        UPDATED_DT=CURRENT_TIMESTAMP()
                        WHERE ID=:id";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "name", model.ConnectionName }, { "host", model.AppServerHost },
                { "client", model.Client }, { "sysNum", model.SystemNumber },
                { "user", model.RfcUser }, { "pass", model.RfcPasswordEnc },
                { "lang", model.Language ?? "EN" }, { "active", model.IsActive },
                { "env", model.Environment }, { "id", id }
            });
        }

        public int Create(SapConnectionModel model)
        {
            var sql = @"INSERT INTO RFC_SAP_CONNECTION
                        (CONNECTION_NAME, APP_SERVER_HOST, CLIENT, SYSTEM_NUMBER, RFC_USER,
                         RFC_PASSWORD_ENC, LANGUAGE, IS_ACTIVE, ENVIRONMENT)
                        VALUES (:name, :host, :client, :sysNum, :user, :pass, :lang, :active, :env)";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "name", model.ConnectionName }, { "host", model.AppServerHost },
                { "client", model.Client }, { "sysNum", model.SystemNumber },
                { "user", model.RfcUser }, { "pass", model.RfcPasswordEnc },
                { "lang", model.Language ?? "EN" }, { "active", model.IsActive },
                { "env", model.Environment }
            });
        }

        public List<Dictionary<string, object>> GetActiveConnections()
        {
            return _sf.QueryAsList("SELECT ID, CONNECTION_NAME, ENVIRONMENT FROM RFC_SAP_CONNECTION WHERE IS_ACTIVE=TRUE ORDER BY ENVIRONMENT");
        }
    }
}
