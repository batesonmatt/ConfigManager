using System.Configuration;
using System.Data.SqlClient;
using System.Data;

namespace ConfigManager.DataLayer
{
    public class StoreDB
    {
        #region Fields

        private readonly string _connectionString = GetConnectionStringByName("Benson");

        #endregion

        #region Methods

        private static string GetConnectionStringByName(string name)
        {
            string connection;
            ConnectionStringSettings settings;

            try
            {
                settings = ConfigurationManager.ConnectionStrings[name];
                connection = settings.ConnectionString;
            }
            catch
            {
                connection = string.Empty;
            }

            return connection;
        }

        private DataTable GetDataTable(string sql, SqlParameter[] parameters)
        {
            DataTable dt = new();

            try
            {
                using SqlConnection con = new(_connectionString);
                using SqlCommand cmd = new(sql, con);

                cmd.CommandType = CommandType.Text;

                if (parameters is not null)
                {
                    foreach (SqlParameter parameter in parameters)
                    {
                        if (parameter is not null)
                        {
                            cmd.Parameters.Add(parameter);
                        }
                    }
                }

                using SqlDataAdapter sda = new(cmd);
                sda.Fill(dt);
            }
            catch
            {
                dt.Clear();
            }

            return dt;
        }

        public bool ValidateOperator(string name, string password)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }

            bool isValid = false;
            SqlParameter[] parameters;
            string sql;
            DataRow record;

            try
            {
                parameters = new SqlParameter[1];
                parameters[0] = new("name", SqlDbType.VarChar, 50) { Value = name };

                sql = "SELECT * FROM Operators WHERE UserName = @name";

                using DataTable operatorData = GetDataTable(sql, parameters);

                if (operatorData is not null && operatorData.Rows is not null && operatorData.Rows.Count > 0)
                {
                    record = operatorData.Rows[0];
                    isValid = record["OperatorPassword"].ToString() == password;
                }
            }
            catch
            {
                isValid = false;
            }

            return isValid;
        }

        #endregion
    }
}
