using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

// Src: https://brennydoogles.wordpress.com/2010/02/26/using-sqlite-with-csharp/
public class SQLiteDatabase : IDisposable
{
    String dbConnection;
    private SQLiteConnection _conn;

    /// <summary>
    ///     Default Constructor for SQLiteDatabase Class.
    /// </summary>
    public SQLiteDatabase()
    {
        dbConnection = "Data Source=recipes.s3db";
    }

    /// <summary>
    ///     Single Param Constructor for specifying the DB file.
    /// </summary>
    /// <param name="inputFile">The File containing the DB</param>
    public SQLiteDatabase(String inputFile)
    {
        dbConnection = String.Format("Data Source={0}", inputFile);
    }

    /// <summary>
    ///     Single Param Constructor for specifying advanced connection options.
    /// </summary>
    /// <param name="connectionOpts">A dictionary containing all desired options and their values</param>
    public SQLiteDatabase(Dictionary<String, String> connectionOpts)
    {
        String str = "";
        foreach (KeyValuePair<String, String> row in connectionOpts)
        {
            str += String.Format("{0}={1}; ", row.Key, row.Value);
        }
        str = str.Trim().Substring(0, str.Length - 1);
        dbConnection = str;
    }

    protected void Connect()
    {
        _conn = new SQLiteConnection(dbConnection);
        _conn.Open();
    }

    /// <summary>
    ///     Allows the programmer to run a query against the Database.
    /// </summary>
    /// <param name="sql">The SQL to run</param>
    /// <returns>A DataTable containing the result set.</returns>
    public DataTable GetDataTable(string sql, Dictionary<string, object> parameters = null)
    {
        DataTable dt = new DataTable();
        try
        {
            using (SQLiteConnection cnn = new SQLiteConnection(dbConnection))
            {
                cnn.Open();
                using (SQLiteCommand mycommand = new SQLiteCommand(cnn))
                {
                    if (parameters != null)
                    {
                        foreach (KeyValuePair<string, object> param in parameters)
                        {
                            mycommand.Parameters.Add(new SQLiteParameter(param.Key, param.Value));
                        }
                    }

                    mycommand.CommandText = sql;
                    using (SQLiteDataReader reader = mycommand.ExecuteReader())
                    {

                        dt.Load(reader);

                        reader.Close();
                    }
                }

                cnn.Close();
                // Src: https://stackoverflow.com/questions/12532729/sqlite-keeps-the-database-locked-even-after-the-connection-is-closed
                // Src: http://system.data.sqlite.org/index.html/tktview/6434e23a0f63b440?plaintext
                //SQLiteConnection.ClearAllPools();
            }
        }
        catch (Exception e)
        {
            throw new Exception(e.Message);
        }
        return dt;
    }

    /// <summary>
    ///     Allows the programmer to interact with the database for purposes other than a query.
    /// </summary>
    /// <param name="sql">The SQL to be run.</param>
    /// <returns>An Integer containing the number of rows updated.</returns>
    public int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null)
    {
        int rowsUpdated = -1;
        using (SQLiteConnection cnn = new SQLiteConnection(dbConnection))
        {
            cnn.Open();
            using (SQLiteCommand mycommand = new SQLiteCommand(cnn))
            {
                if (parameters != null)
                {
                    foreach (KeyValuePair<string, object> param in parameters)
                    {
                        mycommand.Parameters.Add(new SQLiteParameter(param.Key, param.Value));
                    }
                }

                mycommand.CommandText = sql;
                rowsUpdated = mycommand.ExecuteNonQuery();
            }

            cnn.Close();
            // Src: https://stackoverflow.com/questions/12532729/sqlite-keeps-the-database-locked-even-after-the-connection-is-closed
            // Src: http://system.data.sqlite.org/index.html/tktview/6434e23a0f63b440?plaintext
            //SQLiteConnection.ClearAllPools();
        }

        return rowsUpdated;
    }

    /// <summary>
    ///     Allows the programmer to retrieve single items from the DB.
    /// </summary>
    /// <param name="sql">The query to run.</param>
    /// <returns>A string.</returns>
    public string ExecuteScalar(string sql, Dictionary<string, object> parameters = null)
    {
        object value;
        using (SQLiteConnection cnn = new SQLiteConnection(dbConnection))
        {
            cnn.Open();
            using (SQLiteCommand mycommand = new SQLiteCommand(cnn))
            {
                if (parameters != null)
                {
                    foreach (KeyValuePair<string, object> param in parameters)
                    {
                        mycommand.Parameters.Add(new SQLiteParameter(param.Key, param.Value));
                    }
                }

                mycommand.CommandText = sql;
                value = mycommand.ExecuteScalar();
            }

            cnn.Close();

            // Src: https://stackoverflow.com/questions/12532729/sqlite-keeps-the-database-locked-even-after-the-connection-is-closed
            // Src: http://system.data.sqlite.org/index.html/tktview/6434e23a0f63b440?plaintext
            SQLiteConnection.ClearAllPools();
            if (value != null)
            {
                return value.ToString();
            }
        }

        return "";
    }

    /// <summary>
    ///     Allows the programmer to easily update rows in the DB.
    /// </summary>
    /// <param name="tableName">The table to update.</param>
    /// <param name="data">A dictionary containing Column names and their new values.</param>
    /// <param name="where">The where clause for the update statement.</param>
    /// <returns>A boolean true or false to signify success or failure.</returns>
    public bool Update(String tableName, Dictionary<String, String> data, String where)
    {
        String vals = "";
        Boolean returnCode = true;
        if (data.Count >= 1)
        {
            foreach (KeyValuePair<String, String> val in data)
            {
                vals += String.Format(" {0} = '{1}',", val.Key.ToString(), val.Value.ToString());
            }
            vals = vals.Substring(0, vals.Length - 1);
        }
        try
        {
            this.ExecuteNonQuery(String.Format("update {0} set {1} where {2};", tableName, vals, where));
        }
        catch
        {
            returnCode = false;
        }
        return returnCode;
    }

    /// <summary>
    ///     Allows the programmer to easily delete rows from the DB.
    /// </summary>
    /// <param name="tableName">The table from which to delete.</param>
    /// <param name="where">The where clause for the delete.</param>
    /// <returns>A boolean true or false to signify success or failure.</returns>
    public bool Delete(String tableName, String where)
    {
        Boolean returnCode = true;
        try
        {
            this.ExecuteNonQuery(String.Format("delete from {0} where {1};", tableName, where));
        }
        catch (Exception fail)
        {
            
            returnCode = false;
        }
        return returnCode;
    }

    /// <summary>
    ///     Allows the programmer to easily insert into the DB
    /// </summary>
    /// <param name="tableName">The table into which we insert the data.</param>
    /// <param name="data">A dictionary containing the column names and data for the insert.</param>
    /// <returns>A boolean true or false to signify success or failure.</returns>
    public bool Insert(String tableName, Dictionary<String, String> data)
    {
        String columns = "";
        String values = "";
        Boolean returnCode = true;
        foreach (KeyValuePair<String, String> val in data)
        {
            columns += String.Format(" {0},", val.Key.ToString());
            values += String.Format(" '{0}',", val.Value.Replace("'", "''"));
        }
        columns = columns.Substring(0, columns.Length - 1);
        values = values.Substring(0, values.Length - 1);
        try
        {
            this.ExecuteNonQuery(String.Format("insert into {0}({1}) values ({2});", tableName, columns, values));
        }
        catch (Exception fail)
        {
            returnCode = false;
        }
        return returnCode;
    }

    /// <summary>
    ///     Allows the programmer to easily delete all data from the DB.
    /// </summary>
    /// <returns>A boolean true or false to signify success or failure.</returns>
    public bool ClearDB()
    {
        DataTable tables;
        try
        {
            tables = this.GetDataTable("select NAME from SQLITE_MASTER where type='table' order by NAME;");
            foreach (DataRow table in tables.Rows)
            {
                this.ClearTable(table["NAME"].ToString());
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Allows the user to easily clear all data from a specific table.
    /// </summary>
    /// <param name="table">The name of the table to clear.</param>
    /// <returns>A boolean true or false to signify success or failure.</returns>
    public bool ClearTable(String table)
    {
        try
        {

            this.ExecuteNonQuery(String.Format("delete from {0};", table));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        
    }
}
