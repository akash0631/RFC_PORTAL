using System.Collections.Generic;
using System.Linq;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class RfcParamService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        // --- RFC_PARAM ---

        public List<Dictionary<string, object>> GetParamsByRfcId(int rfcId)
        {
            return _sf.QueryAsList("SELECT * FROM RFC_PARAM WHERE RFC_ID=:rfcId ORDER BY SORT_ORDER",
                new Dictionary<string, object> { { "rfcId", rfcId } });
        }

        public int CreateParam(RfcParamModel model)
        {
            var sql = @"INSERT INTO RFC_PARAM
                        (RFC_ID, PARAM_NAME, PARAM_TYPE, DATA_TYPE, DEFAULT_EXPRESSION, IS_REQUIRED, DISPLAY_NAME, SORT_ORDER)
                        VALUES (:rfcId, :name, :type, :dataType, :defaultExpr, :required, :display, :sort)";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "rfcId", model.RfcId }, { "name", model.ParamName },
                { "type", model.ParamType ?? "Scalar" }, { "dataType", model.DataType ?? "String" },
                { "defaultExpr", model.DefaultExpression }, { "required", model.IsRequired },
                { "display", model.DisplayName }, { "sort", model.SortOrder }
            });
        }

        public int UpdateParam(int id, RfcParamModel model)
        {
            var sql = @"UPDATE RFC_PARAM SET
                        PARAM_NAME=:name, PARAM_TYPE=:type, DATA_TYPE=:dataType,
                        DEFAULT_EXPRESSION=:defaultExpr, IS_REQUIRED=:required,
                        DISPLAY_NAME=:display, SORT_ORDER=:sort
                        WHERE ID=:id";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "name", model.ParamName }, { "type", model.ParamType },
                { "dataType", model.DataType }, { "defaultExpr", model.DefaultExpression },
                { "required", model.IsRequired }, { "display", model.DisplayName },
                { "sort", model.SortOrder }, { "id", id }
            });
        }

        public int DeleteParam(int id)
        {
            return _sf.ExecuteNonQuery("DELETE FROM RFC_PARAM WHERE ID=:id",
                new Dictionary<string, object> { { "id", id } });
        }

        // --- RFC_SELECT_OPTION ---

        public List<Dictionary<string, object>> GetSelectOptionsByRfcId(int rfcId)
        {
            return _sf.QueryAsList("SELECT * FROM RFC_SELECT_OPTION WHERE RFC_ID=:rfcId ORDER BY ID",
                new Dictionary<string, object> { { "rfcId", rfcId } });
        }

        public int CreateSelectOption(RfcSelectOptionModel model)
        {
            var sql = @"INSERT INTO RFC_SELECT_OPTION
                        (RFC_ID, TABLE_NAME, SIGN_VALUE, OPTION_VALUE, VALUE_SOURCE, STATIC_VALUE, SOURCE_QUERY, LOOP_COLUMN)
                        VALUES (:rfcId, :table, :sign, :option, :source, :static, :query, :loop)";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "rfcId", model.RfcId }, { "table", model.TableName },
                { "sign", model.SignValue ?? "I" }, { "option", model.OptionValue ?? "CP" },
                { "source", model.ValueSource ?? "Loop" }, { "static", model.StaticValue },
                { "query", model.SourceQuery }, { "loop", model.LoopColumn }
            });
        }

        public int UpdateSelectOption(int id, RfcSelectOptionModel model)
        {
            var sql = @"UPDATE RFC_SELECT_OPTION SET
                        TABLE_NAME=:table, SIGN_VALUE=:sign, OPTION_VALUE=:option,
                        VALUE_SOURCE=:source, STATIC_VALUE=:static,
                        SOURCE_QUERY=:query, LOOP_COLUMN=:loop
                        WHERE ID=:id";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "table", model.TableName }, { "sign", model.SignValue },
                { "option", model.OptionValue }, { "source", model.ValueSource },
                { "static", model.StaticValue }, { "query", model.SourceQuery },
                { "loop", model.LoopColumn }, { "id", id }
            });
        }

        public int DeleteSelectOption(int id)
        {
            return _sf.ExecuteNonQuery("DELETE FROM RFC_SELECT_OPTION WHERE ID=:id",
                new Dictionary<string, object> { { "id", id } });
        }
    }
}
