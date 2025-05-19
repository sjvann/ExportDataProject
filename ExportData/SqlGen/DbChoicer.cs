using ExportData.Models.Config;
using ExportData.Models.EnumType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExportData.SqlGen
{
    public static class DbChoicer
    {
        public static ISqlGenerater? ChoiceProverder(ConfigDbControlSection? config)
        {
            ISqlGenerater? sqlProvider;
            switch (config?.DbType)
            {
                case EnumDbType.Sqlite:
                    sqlProvider = new GenSqlForSqlite(config);
                    break;
                case EnumDbType.SqlServer:
                    sqlProvider = new GenSqlForSqlServer(config);
                    break;
                case EnumDbType.MySql:
                    sqlProvider = new GenSqlForMySql(config);
                    break;
                case EnumDbType.Oracle:
                    sqlProvider = new GenSqlForOracle(config);
                    break;
                default:
                    return default;
            }
            return sqlProvider;
        }

    }
}
