using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;

namespace MailingList
{
    #region Query
    class Query
    {
        private string query_sql;

        public Query(string query)
        {
            query_sql = query;
            query_sql = query_sql.Replace(", )", ")");
            query_sql = query_sql.Replace(",)", ")");
        }

        public bool ExecuteQuery(DatabaseConnection database)
        {
            try
            {
                MySqlCommand cmd = new MySqlCommand(query_sql, database.GetSqlConnection());
                Message.ShowMessage(query_sql, Verbosity_Level.E_Debug);
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex)
            {
                if (ex.Message.StartsWith("Duplicate entry"))
                    Message.ShowMessage(ex.Message, Verbosity_Level.E_Warning);
                else
                    Console.WriteLine(ex.Message);
                return false;
            }
            return true;
        }

        public bool ExecuteQuery(DatabaseConnection database, out MySqlDataReader reader)
        {
            reader = null;
            try
            {
                MySqlCommand cmd = new MySqlCommand(query_sql, database.GetSqlConnection());
                Message.ShowMessage(query_sql, Verbosity_Level.E_Debug);
                reader = cmd.ExecuteReader();
            }
            catch (MySqlException ex)
            {
                Message.ShowMessage("Error: {0}", Verbosity_Level.E_Error, ex.Message);
                return false;
            }
            return true;
        }
    }
    #endregion

    #region QueryCreate
    class QueryCreateTable
    {
        string query_sql;
               
        public QueryCreateTable(string table_name)
        { 
            query_sql = "CREATE TABLE IF NOT EXISTS " + table_name + " ";
        }

        public QueryCreateTable Fields(params string[] fields)
        {
            query_sql += "(";
            foreach (string field in fields)
                query_sql += field + ", ";

            return this;
        }

        public QueryCreateTable PrimaryKey(string primary_key)
        {
            query_sql += "PRIMARY KEY (" + primary_key + ")";
            return this;
        }

        public Query Build()
        {
            query_sql += ") ENGINE=InnoDB DEFAULT CHARSET=ascii";
            return new Query(query_sql);
        }
    }
    #endregion

    #region QuerySelect
    class QuerySelectFromTable
    {
        string table_name;
        string count;
        string like;
        string[] fields;
        string[] where_column_name;
        string[] where_value;

        public QuerySelectFromTable(string table_name)
        {
            this.table_name = table_name;       
        }

        public QuerySelectFromTable Count(string count)
        {
            this.count = count;
            return this;
        }

        public QuerySelectFromTable Fields(params string[] fields)
        {
            this.fields = fields;
            return this;
        }

        public QuerySelectFromTable WhereFields(params string[] column_names)
        {
            where_column_name = column_names;
            return this;
        }

        public QuerySelectFromTable WhereValues(params string[] values)
        {
            where_value = values;
            return this;
        }

        public QuerySelectFromTable Like(string pattern)
        {
            like = pattern;
            return this;
        }

        public Query Build()
        {
            string query_sql = "SELECT ";
            if (count != null)
                query_sql += "COUNT(" + count + ") ";
            else
            {
                query_sql += "(";
                foreach (string field in fields)
                    query_sql += field + ", ";
                query_sql += ") ";
            }
            query_sql += "FROM " + table_name + " ";
            if (where_column_name != null)
                query_sql += "WHERE " + where_column_name[0] + " ";
            if(where_value != null)
                query_sql += "= '" + where_value[0] + "' ";
            if (like != null)
                query_sql += "LIKE " + like;
            return new Query(query_sql);
        }
    }
    #endregion

    #region QueryInsert
    class QueryInsertIntoTable
    {
        string query_sql;

        public QueryInsertIntoTable(string table_name)
        {
            query_sql = "INSERT INTO " + table_name + " ";
        }
        
        public QueryInsertIntoTable Fields(params string[] fields)
        {
            query_sql += "(";
            foreach(string field in fields)
                query_sql += field + ", ";
            query_sql += ")";
            
            return this;
        }

        public QueryInsertIntoTable Values(params string[] values)
        {
            query_sql += " VALUES (";
            foreach (string value in values)
                query_sql += "'" + value + "', ";
            query_sql += ")";

            return this;
        }

        public Query Build()
        {
            return new Query(query_sql); 
        }
    }
    #endregion

    #region QueryUpdate
    class QueryUpdateFromTable
    {
        string table_name;
        string set;
        string[] where_column_name;
        string[] where_value;

        public QueryUpdateFromTable(string table_name)
        {
            this.table_name = table_name;
        }

        public QueryUpdateFromTable WhereFields(params string[] column_names)
        {
            where_column_name = column_names;
            return this;
        }

        public QueryUpdateFromTable WhereValues(params string[] values)
        {
            where_value = values;
            return this;
        }

        public QueryUpdateFromTable Set(string column_name, string value)
        {
            set = "SET " + column_name + "='" + value + "'";
            return this;
        }

        public Query Build()
        {
            string query_sql = "UPDATE " + table_name + " ";
            query_sql += set + " ";
            if (where_column_name != null && where_value != null)
            {
                query_sql += "WHERE ";
                query_sql += where_column_name[0] + "='" + where_value[0] + "'";
            }
            return new Query(query_sql);
        }
    }
    #endregion

    #region QueryDelete
    class QueryDeleteFromTable
    {
        string query_sql;

        public QueryDeleteFromTable(string table_name)
        {
            query_sql = "DELETE FROM " + table_name + " WHERE ";
        }

        public QueryDeleteFromTable Where(string column_name, string value)
        {
            query_sql += column_name + "='" + value + "'";
            return this;
        }

        public Query Build()
        {
            return new Query(query_sql);
        }
    }
    #endregion
}
